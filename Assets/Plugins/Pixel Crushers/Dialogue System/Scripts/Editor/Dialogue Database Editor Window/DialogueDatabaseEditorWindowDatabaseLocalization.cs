// Copyright (c) Pixel Crushers. All rights reserved.

#if UNITY_2022_1_OR_NEWER

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;

namespace PixelCrushers.DialogueSystem.DialogueEditor
{

    /// <summary>
    /// This part of the UI Toolkit dialogue database editor window keeps the localization
    /// languages, which the Database panel's Localization section and AI addons share.
    /// </summary>
    public partial class DialogueDatabaseEditorWindow
    {

        // Shared with the IMGUI editor, so both editors use the same languages and extra fields.
        private const string LocalizationLanguagesPrefsKey = "PixelCrushers.DialogueSystem.DialogueEditor.LocalizationLanguages";

        /// <summary>Loads the localization languages from EditorPrefs the first time they're needed.</summary>
        internal void EnsureLocalizationLanguagesLoaded()
        {
            var state = DatabaseState;
            if (state.localizationLanguagesLoaded) return;
            state.localizationLanguagesLoaded = true;
            if (!EditorPrefs.HasKey(LocalizationLanguagesPrefsKey)) return;
            try
            {
                var languages = JsonUtility.FromJson<DialogueDatabaseLocalizationImporter.LocalizationLanguages>(EditorPrefs.GetString(LocalizationLanguagesPrefsKey));
                if (languages != null) state.localizationLanguages = languages;
            }
            catch (ArgumentException)
            {
                // Ignore unreadable prefs and keep the defaults.
            }
        }

        internal void SaveLocalizationLanguages()
        {
            EditorPrefs.SetString(LocalizationLanguagesPrefsKey, JsonUtility.ToJson(DatabaseState.localizationLanguages));
        }

        /// <summary>
        /// Adds the languages that the database has localized fields for to the localization
        /// languages list, saves the list, and returns it.
        /// </summary>
        public List<string> FindLocalizationLanguages()
        {
            EnsureLocalizationLanguagesLoaded();
            var languages = DatabaseState.localizationLanguages.languages;
            if (database == null) return languages;
            ApplyPendingChanges();
            DialogueDatabaseLocalizationExporter.FindLanguages(database, Template, languages);
            SaveLocalizationLanguages();
            var databasePanel = GetPanel(Category.Database) as DatabasePanel;
            if (databasePanel != null) databasePanel.RefreshLocalizationLanguagesList();
            return languages;
        }

    }

    /// <summary>
    /// Database panel's Localization Export/Import section: exports CSV files that translators
    /// fill in for each language, and imports them back. Uses the same files and code as the
    /// IMGUI editor (DialogueDatabaseLocalizationExporter and DialogueDatabaseLocalizationImporter).
    /// </summary>
    public partial class DatabasePanel
    {

        private const float LocalizationLabelWidth = 170;
        private const float LocalizationListItemHeight = 22;
        private const int LocalizationListMaxVisibleItems = 8;
        private const float LocalizationButtonWidth = 110;
        private const float ListButtonWidth = 24;

        private enum LocalizationList { Languages, EntryFields, QuestFields, ItemFields }

        private Dictionary<LocalizationList, ListView> localizationListViews = new Dictionary<LocalizationList, ListView>();
        private VisualElement itemFieldsListContainer = null;
        private UnityEngine.UIElements.Toggle localizationTitleToggle = null;
        private UnityEngine.UIElements.Toggle localizationUseKeyToggle = null;
        private TextField localizationKeyFieldField = null;
        private UnityEngine.UIElements.Toggle localizationAssignKeysToggle = null;
        private UnityEngine.UIElements.Toggle localizationCreateFieldsToggle = null;
        private UnityEngine.UIElements.Toggle localizationIncludeBlankToggle = null;
        private EnumField localizationSortField = null;
        private Button localizationExportButton = null;
        private Button localizationImportButton = null;

        private DialogueDatabaseLocalizationImporter.LocalizationLanguages localizationLanguages
        {
            get { return databaseState.localizationLanguages; }
        }

        #region UI

        // The panel is created before its window is assigned, so the lists and fields are
        // filled from the window's state later, in RefreshLocalizationFields.
        private void AddLocalizationSection(VisualElement parent)
        {
            parent.Add(new HelpBox("Export CSV files for translators to fill in, one set for each language, then import them back. " +
                "Each language exports Actors, Dialogue, Quests and Items files.", UnityEngine.UIElements.HelpBoxMessageType.None));

            parent.Add(CreateLocalizationList(LocalizationList.Languages, "Languages",
                "Language codes to export and import, such as 'fr'. Tick Main on one language to also update the main (original) text from its files when importing."));
            parent.Add(CreateLocalizationList(LocalizationList.EntryFields, "Extra Dialogue Entry Fields", "(Optional) Extra dialogue entry fields to localize."));
            parent.Add(CreateLocalizationList(LocalizationList.QuestFields, "Extra Quest Fields", "(Optional) Extra quest fields to localize."));
            itemFieldsListContainer = CreateLocalizationList(LocalizationList.ItemFields, "Extra Item Fields", "(Optional) Extra item fields to localize.");
            parent.Add(itemFieldsListContainer);

            localizationTitleToggle = AddLocalizationToggle(parent, "Use Conversation Titles", "Export conversation titles instead of IDs. Titles should be unique.",
                (state, value) => state.localizationUseConversationTitle = value);
            localizationUseKeyToggle = AddLocalizationToggle(parent, "Use Key Field", "Tie each dialogue entry row to a key field (e.g., 'Articy Id' or 'Celtx ID') instead of conversation & entry IDs.",
                (state, value) => state.localizationUseKeyField = value);
            localizationKeyFieldField = new TextField("Key Field") { tooltip = "Dialogue entry field that identifies each entry." };
            localizationKeyFieldField.style.marginLeft = 15;
            localizationKeyFieldField.labelElement.style.minWidth = LocalizationLabelWidth - 15;
            localizationKeyFieldField.RegisterValueChangedCallback(evt =>
            {
                if (window == null) return;
                databaseState.localizationKeyField = evt.newValue;
                RefreshLocalizationVisibility();
            });
            parent.Add(localizationKeyFieldField);
            localizationAssignKeysToggle = AddLocalizationToggle(parent, "Assign Values", "When exporting, give entries without a key field value a unique value. You can undo it.",
                (state, value) => state.localizationAssignKeyValues = value);
            localizationAssignKeysToggle.style.marginLeft = 15;
            localizationAssignKeysToggle.labelElement.style.minWidth = LocalizationLabelWidth - 15;
            localizationCreateFieldsToggle = AddLocalizationToggle(parent, "Create New Fields", "When importing, create an extra field's translation even if the asset doesn't have the field.",
                (state, value) => state.localizationCreateNewFields = value);
            localizationIncludeBlankToggle = AddLocalizationToggle(parent, "Include Blank Entries", "Include entries without Dialogue Text or Menu Text in the Dialogue files.",
                (state, value) => state.localizationIncludeBlankEntries = value);

            localizationSortField = new EnumField("Sort Entries", DialogueEntrySortMethod.No) { tooltip = "Order of each conversation's entries in the Dialogue files." };
            localizationSortField.labelElement.style.minWidth = LocalizationLabelWidth;
            localizationSortField.RegisterValueChangedCallback(evt =>
            {
                if (window == null || !(evt.newValue is DialogueEntrySortMethod)) return;
                databaseState.localizationSortMethod = (DialogueEntrySortMethod)evt.newValue;
            });
            parent.Add(localizationSortField);

            var buttonRow = new VisualElement();
            buttonRow.style.flexDirection = FlexDirection.Row;
            buttonRow.style.justifyContent = Justify.FlexEnd;
            buttonRow.style.marginTop = 6;
            var findButton = new Button(FindLocalizationLanguages)
            {
                text = "Find Languages",
                tooltip = "Add the languages that dialogue entries have localized text fields for."
            };
            findButton.style.width = LocalizationButtonWidth;
            buttonRow.Add(findButton);
            localizationExportButton = new Button(ChooseFolderAndExportLocalization) { text = "Export..." };
            localizationExportButton.style.width = LocalizationButtonWidth;
            buttonRow.Add(localizationExportButton);
            localizationImportButton = new Button(ChooseFolderAndImportLocalization) { text = "Import..." };
            localizationImportButton.style.width = LocalizationButtonWidth;
            buttonRow.Add(localizationImportButton);
            parent.Add(buttonRow);

            RefreshLocalizationVisibility();
        }

        private UnityEngine.UIElements.Toggle AddLocalizationToggle(VisualElement parent, string label, string tooltip,
            Action<DialogueDatabaseEditorWindow.DatabasePanelState, bool> setState)
        {
            var toggle = new UnityEngine.UIElements.Toggle(label) { tooltip = tooltip };
            toggle.labelElement.style.minWidth = LocalizationLabelWidth;
            toggle.RegisterValueChangedCallback(evt =>
            {
                if (window == null) return;
                setState(databaseState, evt.newValue);
                RefreshLocalizationVisibility();
            });
            parent.Add(toggle);
            return toggle;
        }

        private List<string> GetLocalizationList(LocalizationList list)
        {
            switch (list)
            {
                case LocalizationList.Languages: return localizationLanguages.languages;
                case LocalizationList.EntryFields: return localizationLanguages.extraEntryFields;
                case LocalizationList.QuestFields: return localizationLanguages.extraQuestFields;
                default: return localizationLanguages.extraItemFields;
            }
        }

        /// <summary>
        /// A heading, a reorderable list of text fields, and add/remove buttons. The Languages
        /// list also has a Main toggle on each row, which works like a radio button.
        /// </summary>
        private VisualElement CreateLocalizationList(LocalizationList listType, string heading, string tooltip)
        {
            var isLanguages = listType == LocalizationList.Languages;
            var container = new VisualElement();
            container.style.marginTop = 6;

            var headingRow = new VisualElement();
            headingRow.style.flexDirection = FlexDirection.Row;
            headingRow.style.alignItems = Align.Center;
            var headingLabel = new Label(heading) { tooltip = tooltip };
            headingLabel.style.flexGrow = 1;
            headingLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            headingLabel.style.marginLeft = 3;
            headingRow.Add(headingLabel);
            if (isLanguages)
            {
                var mainLabel = new Label("Main") { tooltip = "Also update the main (original) text from this language's files when importing." };
                mainLabel.style.marginRight = 4;
                headingRow.Add(mainLabel);
            }
            container.Add(headingRow);

            var listView = new ListView()
            {
                reorderable = true,
                reorderMode = ListViewReorderMode.Simple,
                selectionType = SelectionType.Single,
                showBorder = true,
                fixedItemHeight = LocalizationListItemHeight,
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
                tooltip = tooltip
            };
            listView.AddToClassList("ds-bordered");
            listView.makeItem = () => MakeLocalizationListRow(listType, listView);
            listView.bindItem = (element, index) => BindLocalizationListRow(listType, element, index);
            listView.itemIndexChanged += (from, to) => OnLocalizationListItemMoved(listType, from, to);
            container.Add(listView);
            localizationListViews[listType] = listView;

            var buttons = new VisualElement();
            buttons.style.flexDirection = FlexDirection.Row;
            buttons.style.justifyContent = Justify.FlexEnd;
            var addButton = new Button(() => AddLocalizationListItem(listType)) { text = "+", tooltip = "Add an item." };
            addButton.style.width = ListButtonWidth;
            buttons.Add(addButton);
            var removeButton = new Button(() => RemoveLocalizationListItem(listType)) { text = "-", tooltip = "Remove the selected item, or the last item if none is selected." };
            removeButton.style.width = ListButtonWidth;
            buttons.Add(removeButton);
            container.Add(buttons);
            return container;
        }

        private VisualElement MakeLocalizationListRow(LocalizationList listType, ListView listView)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            var textField = new TextField() { isDelayed = true };
            textField.style.flexGrow = 1;
            textField.RegisterValueChangedCallback(evt =>
            {
                var index = (row.userData is int) ? (int)row.userData : -1;
                var items = GetLocalizationList(listType);
                if (window == null || index < 0 || index >= items.Count) return;
                items[index] = evt.newValue;
                SaveLocalizationLanguages();
                RefreshLocalizationVisibility();
            });
            row.Add(textField);
            if (listType == LocalizationList.Languages)
            {
                var mainToggle = new UnityEngine.UIElements.Toggle() { tooltip = "Also update the main (original) text from this language's files when importing." };
                mainToggle.style.marginRight = 8;
                mainToggle.RegisterValueChangedCallback(evt =>
                {
                    var index = (row.userData is int) ? (int)row.userData : -1;
                    if (window == null || index < 0) return;
                    localizationLanguages.importMainTextIndex = evt.newValue ? index : -1;
                    SaveLocalizationLanguages();
                    listView.RefreshItems(); // Untick the other languages.
                });
                row.Add(mainToggle);
            }
            return row;
        }

        private void BindLocalizationListRow(LocalizationList listType, VisualElement row, int index)
        {
            row.userData = index;
            var items = GetLocalizationList(listType);
            row.Q<TextField>().SetValueWithoutNotify((index < items.Count) ? (items[index] ?? string.Empty) : string.Empty);
            var mainToggle = row.Q<UnityEngine.UIElements.Toggle>();
            if (mainToggle != null) mainToggle.SetValueWithoutNotify(localizationLanguages.importMainTextIndex == index);
        }

        /// <summary>Keeps the Main language the same when the user drags a language to another position.</summary>
        private void OnLocalizationListItemMoved(LocalizationList listType, int from, int to)
        {
            if (listType == LocalizationList.Languages)
            {
                var main = localizationLanguages.importMainTextIndex;
                if (main == from) main = to;
                else if (from < main && main <= to) main--;
                else if (to <= main && main < from) main++;
                localizationLanguages.importMainTextIndex = main;
                localizationListViews[listType].RefreshItems();
            }
            SaveLocalizationLanguages();
        }

        private void AddLocalizationListItem(LocalizationList listType)
        {
            if (window == null) return;
            var items = GetLocalizationList(listType);
            items.Add(string.Empty);
            SaveLocalizationLanguages();
            RefreshLocalizationList(listType);
            var listView = localizationListViews[listType];
            listView.SetSelection(items.Count - 1);
            listView.ScrollToItem(items.Count - 1);
        }

        private void RemoveLocalizationListItem(LocalizationList listType)
        {
            if (window == null) return;
            var items = GetLocalizationList(listType);
            if (items.Count == 0) return;
            var listView = localizationListViews[listType];
            var index = (0 <= listView.selectedIndex && listView.selectedIndex < items.Count) ? listView.selectedIndex : items.Count - 1;
            items.RemoveAt(index);
            if (listType == LocalizationList.Languages)
            {
                var main = localizationLanguages.importMainTextIndex;
                if (main == index) localizationLanguages.importMainTextIndex = -1;
                else if (main > index) localizationLanguages.importMainTextIndex = main - 1;
            }
            listView.ClearSelection();
            SaveLocalizationLanguages();
            RefreshLocalizationList(listType);
        }

        private void RefreshLocalizationList(LocalizationList listType)
        {
            if (!localizationListViews.TryGetValue(listType, out var listView)) return;
            var items = (window != null) ? GetLocalizationList(listType) : new List<string>();
            listView.itemsSource = items;
            var visibleRows = Mathf.Clamp(items.Count, 1, LocalizationListMaxVisibleItems);
            listView.style.height = visibleRows * LocalizationListItemHeight + 2;
            listView.RefreshItems();
            RefreshLocalizationVisibility();
        }

        /// <summary>Shows the window's saved options in the fields, loading the shared language lists the first time.</summary>
        private void RefreshLocalizationFields()
        {
            if (window == null || localizationExportButton == null) return;
            var state = databaseState;
            window.EnsureLocalizationLanguagesLoaded();
            foreach (LocalizationList listType in Enum.GetValues(typeof(LocalizationList)))
            {
                RefreshLocalizationList(listType);
            }
            localizationTitleToggle.SetValueWithoutNotify(state.localizationUseConversationTitle);
            localizationUseKeyToggle.SetValueWithoutNotify(state.localizationUseKeyField);
            localizationKeyFieldField.SetValueWithoutNotify(state.localizationKeyField ?? string.Empty);
            localizationAssignKeysToggle.SetValueWithoutNotify(state.localizationAssignKeyValues);
            localizationCreateFieldsToggle.SetValueWithoutNotify(state.localizationCreateNewFields);
            localizationIncludeBlankToggle.SetValueWithoutNotify(state.localizationIncludeBlankEntries);
            localizationSortField.SetValueWithoutNotify(state.localizationSortMethod);
            RefreshLocalizationVisibility();
        }

        private void RefreshLocalizationVisibility()
        {
            if (localizationExportButton == null) return;
            var hasWindow = window != null && database != null;
            var state = hasWindow ? databaseState : null;
            var useKey = state != null && state.localizationUseKeyField;
            SetShown(localizationKeyFieldField, useKey);
            SetShown(localizationAssignKeysToggle, useKey);
            SetShown(itemFieldsListContainer, hasWindow && database.items.Exists(item => item.IsItem));

            string reason = null;
            if (!hasWindow) reason = "No database.";
            else if (!localizationLanguages.languages.Exists(language => !string.IsNullOrEmpty(language))) reason = "Add a language first.";
            else if (useKey && string.IsNullOrEmpty(state.localizationKeyField)) reason = "Enter a key field, or untick Use Key Field.";
            localizationExportButton.SetEnabled(reason == null);
            localizationImportButton.SetEnabled(reason == null);
            localizationExportButton.tooltip = reason ?? "Choose a folder and export a set of CSV files for each language.";
            localizationImportButton.tooltip = reason ?? "Choose the folder of CSV files and import their translations into this database. You can undo it.";
        }

        private void SaveLocalizationLanguages()
        {
            if (window != null) window.SaveLocalizationLanguages();
        }

        internal void RefreshLocalizationLanguagesList()
        {
            RefreshLocalizationList(LocalizationList.Languages);
        }

        private void FindLocalizationLanguages()
        {
            if (window == null || database == null) return;
            var before = localizationLanguages.languages.Count;
            window.FindLocalizationLanguages();
            var found = localizationLanguages.languages.Count - before;
            if (found == 0)
            {
                EditorUtility.DisplayDialog("Find Languages", "No other languages were found. A language's localized Dialogue Text fields are titled with its language code, such as 'fr'.", "OK");
            }
        }

        #endregion

        #region Export and Import

        private void ChooseFolderAndExportLocalization()
        {
            if (window == null || database == null) return;
            var folder = EditorUtility.OpenFolderPanel("Export Localization Files", localizationLanguages.outputFolder, string.Empty);
            if (string.IsNullOrEmpty(folder)) return;
            var message = ExportLocalizationToFolder(folder);
            if (!string.IsNullOrEmpty(message)) EditorUtility.DisplayDialog("Exported Localization CSV", message, "OK");
        }

        /// <summary>
        /// Exports the localization files. If Use Key Field and Assign Values are ticked, first
        /// gives entries without a key value a new one, as an undoable change. Returns the
        /// message to show, or null if canceled.
        /// </summary>
        private string ExportLocalizationToFolder(string folder)
        {
            if (window == null || database == null || string.IsNullOrEmpty(folder)) return null;
            if (serializedObject != null) serializedObject.ApplyModifiedProperties();
            var state = databaseState;
            localizationLanguages.outputFolder = folder;
            SaveLocalizationLanguages();
            var keyField = state.localizationUseKeyField ? state.localizationKeyField : null;
            var assigned = 0;
            if (keyField != null && state.localizationAssignKeyValues)
            {
                Undo.RegisterCompleteObjectUndo(database, "Assign Localization Keys");
                assigned = DialogueDatabaseLocalizationExporter.AssignMissingKeyValues(database, keyField);
                if (assigned > 0)
                {
                    EditorUtility.SetDirty(database);
                    window.RefreshAfterDirectDatabaseChange();
                }
            }
            var missingKeys = (keyField != null && !state.localizationAssignKeyValues)
                ? DialogueDatabaseLocalizationExporter.CountMissingKeyValues(database, keyField, state.localizationIncludeBlankEntries)
                : 0;
            var options = new DialogueDatabaseLocalizationExporter.Options()
            {
                useConversationTitle = state.localizationUseConversationTitle,
                keyField = keyField,
                includeBlankEntries = state.localizationIncludeBlankEntries,
                sortMethod = state.localizationSortMethod
            };
            bool completed;
            try
            {
                completed = DialogueDatabaseLocalizationExporter.Export(database, folder, localizationLanguages, options,
                    (language, progress) => EditorUtility.DisplayCancelableProgressBar("Exporting Localization CSV", "Exporting CSV files for " + language, progress));
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            if (!completed) return null;
            var message = "Localization files are in " + folder + ". Open these files in a spreadsheet application and add translations. Then import them by using the Import... button.";
            if (assigned > 0) message += string.Format("\n\nAssigned {0} new {1} value{2}.", assigned, keyField, (assigned == 1) ? string.Empty : "s");
            if (missingKeys > 0) message += string.Format("\n\n{0} dialogue entr{1} no {2} value, so {3} can't be imported. Tick Assign Values to give them values.",
                missingKeys, (missingKeys == 1) ? "y has" : "ies have", keyField, (missingKeys == 1) ? "it" : "they");
            return message;
        }

        private void ChooseFolderAndImportLocalization()
        {
            if (window == null || database == null) return;
            var folder = EditorUtility.OpenFolderPanel("Import Localization Files", localizationLanguages.outputFolder, string.Empty);
            if (string.IsNullOrEmpty(folder)) return;
            var result = ImportLocalizationFromFolder(folder);
            if (result == null || result.canceled) return;
            var message = "The CSV files have been imported into your dialogue database. You can undo the import.";
            if (result.warnings > 0) message += string.Format("\n\n{0} problem{1} logged to the Console.", result.warnings, (result.warnings == 1) ? " was" : "s were");
            EditorUtility.DisplayDialog("Imported Localization CSV", message, "OK");
        }

        /// <summary>Imports the localization files as one undoable change.</summary>
        private DialogueDatabaseLocalizationImporter.ImportResult ImportLocalizationFromFolder(string folder)
        {
            if (window == null || database == null || string.IsNullOrEmpty(folder)) return null;
            if (serializedObject != null) serializedObject.ApplyModifiedProperties();
            var state = databaseState;
            localizationLanguages.outputFolder = folder;
            SaveLocalizationLanguages();
            Undo.RegisterCompleteObjectUndo(database, "Import Localization");
            try
            {
                return new DialogueDatabaseLocalizationImporter().ImportLocalizationFilesFromFolder(database, folder, localizationLanguages,
                    state.localizationUseConversationTitle, state.localizationCreateNewFields,
                    state.localizationUseKeyField ? state.localizationKeyField : null,
                    (language, progress) => EditorUtility.DisplayCancelableProgressBar("Importing Localization CSV", "Importing CSV files for " + language, progress));
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.SetDirty(database);
                window.RefreshAfterDirectDatabaseChange();
                if (DialogueEditorWindow.instance != null) DialogueEditorWindow.instance.Reset();
            }
        }

        #endregion

    }

}

#endif
