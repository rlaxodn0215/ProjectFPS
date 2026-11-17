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
    /// This part of the UI Toolkit dialogue database editor window holds the Items/Quests
    /// panel's state so it survives domain reloads.
    /// </summary>
    public partial class DialogueDatabaseEditorWindow
    {

        [Serializable]
        public class ItemsPanelState : AssetPanelState
        {
            public bool compactQuestEntries = true;
            public int selectedQuestEntry = 0; // Zero-based. Used in compact view.
            public List<string> expandedLocalizations = new List<string>(); // Base field titles whose Localized foldout is open.
        }

        [SerializeField]
        private ItemsPanelState itemsPanelState = new ItemsPanelState();
        public ItemsPanelState ItemsState { get { return itemsPanelState; } }

    }

    /// <summary>
    /// Items/Quests category. Items and quests share the database's items list; the
    /// "Is Item" field says which one an asset is. When the template's Use Quest System
    /// option is on, the list shows a Type column and new assets can be quests.
    /// Quests show quest properties and quest entries (subtasks), which are stored as
    /// "Entry #" fields plus an "Entry Count" field.
    /// </summary>
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public class ItemsQuestsPanel : AssetListPanel<Item>
    {

        private const string EntryCountFieldTitle = "Entry Count";
        private const string GroupFieldTitle = "Group";

        private static readonly List<string> QuestBuiltInFieldTitles = new List<string> { "Display Name", "Group", "State", "Trackable", "Track", "Abandonable" };

        public ItemsQuestsPanel() : base("Items/Quests") { }

        protected override string AssetTypeName { get { return "Item/Quest"; } }
        protected override string AssetTypePluralName { get { return "Items/Quests"; } }
        protected override string ListPropertyName { get { return "items"; } }
        protected override DialogueDatabaseEditorWindow.AssetPanelState state { get { return window.ItemsState; } }
        private DialogueDatabaseEditorWindow.ItemsPanelState itemsState { get { return window.ItemsState; } }

        private bool UseQuestSystem { get { return window != null && window.Template.treatItemsAsQuests; } }

        protected override List<Item> GetAssets(DialogueDatabase database) { return database.items; }

        protected override List<Field> GetTemplateFields(Template template, Item item)
        {
            return (item == null || item.IsItem) ? template.itemFields : template.questFields;
        }

        protected override List<string> GetPrimaryFieldTitles(Template template, Item item)
        {
            return (item != null && !item.IsItem) ? template.questPrimaryFieldTitles : template.itemPrimaryFieldTitles;
        }

        protected override string GetRowTypeLabel(Item item)
        {
            if (!UseQuestSystem || item == null) return null;
            return item.IsItem ? "Item" : "Quest";
        }

        protected override bool IsSyncEnabled
        {
            get { return database.syncInfo.syncItems; }
            set { database.syncInfo.syncItems = value; }
        }

        protected override DialogueDatabase SyncDatabase
        {
            get { return database.syncInfo.syncItemsDatabase; }
            set { database.syncInfo.syncItemsDatabase = value; }
        }

        protected override void SyncFromDatabase() { database.SyncItems(); }

        #region Menu and Add

        protected override void PopulateMenu(DropdownMenu menu)
        {
            menu.AppendAction("New Item", a => AddNewItem());
            menu.AppendAction("New Quest", a => AddNewQuest(),
                a => UseQuestSystem ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            menu.AppendAction("Use Quest System", a => ToggleUseQuestSystem(),
                a => UseQuestSystem ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
            menu.AppendAction("Sort/By Name", a => SortAssets(item => item.Name, StringComparer.CurrentCulture));
            menu.AppendAction("Sort/By Group", a => SortAssets(item => item.Group ?? string.Empty, StringComparer.CurrentCulture));
            menu.AppendAction("Sort/By ID", a => SortAssets(item => item.id, Comparer<int>.Default));
            AppendHideFilteredMenuItem(menu);
            AppendSyncMenuItem(menu);
        }

        protected override void OnAddButtonClicked(Button button)
        {
            if (UseQuestSystem)
            {
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("Quest"), false, AddNewQuest);
                menu.AddItem(new GUIContent("Item"), false, AddNewItem);
                menu.DropDown(button.worldBound);
            }
            else
            {
                AddNewItem();
            }
        }

        private void AddNewItem()
        {
            AddNewAsset(window.Template.itemFields, "Item");
        }

        private void AddNewQuest()
        {
            AddNewAsset(window.Template.questFields, "Quest");
        }

        private void ToggleUseQuestSystem()
        {
            // The setting is part of the template, so edit it through the template's SerializedObject.
            var templateSerializedObject = window.TemplateSerializedObject;
            templateSerializedObject.Update();
            var property = templateSerializedObject.FindProperty("template.treatItemsAsQuests");
            property.boolValue = !property.boolValue;
            templateSerializedObject.ApplyModifiedProperties();
            Undo.SetCurrentGroupName("Toggle Use Quest System");
            window.SaveTemplateIfChanged();
            DeferRebuildDetails();
        }

        #endregion

        #region Details

        protected override AIFieldTarget? GetAIFieldTarget(string fieldTitle)
        {
            return AIFieldTarget.ForItem(SelectedAssetID, fieldTitle);
        }

        protected override void AddAssetDetails(VisualElement parent, SerializedProperty itemProperty, SerializedProperty fieldsProperty)
        {
            // Item.IsItem treats a missing "Is Item" field as a quest, so do the same here.
            var isItemProperty = FieldPropertyUtility.FindField(fieldsProperty, DialogueSystemFields.IsItem);
            var isItem = FieldPropertyUtility.StringToBool(FieldPropertyUtility.LookupValue(fieldsProperty, DialogueSystemFields.IsItem));
            if (isItemProperty != null)
            {
                // Switch between item and quest layouts if Is Item changes, such as in All Fields.
                parent.TrackPropertyValue(isItemProperty.FindPropertyRelative(FieldPropertyUtility.ValuePropertyName), property => DeferRebuildDetails());
            }

            if (isItem)
            {
                AddPrimaryFields(parent, fieldsProperty, false);
            }
            else
            {
                AddQuestDetails(parent, fieldsProperty);
            }
        }

        private void AddQuestDetails(VisualElement parent, SerializedProperty fieldsProperty)
        {
            AddDisplayNameFields(parent, fieldsProperty);
            AddGroupFields(parent, fieldsProperty);

            parent.Add(CreateOptionalQuestStatePopup("State", "The starting state of the quest.", fieldsProperty, "State"));

            var isTrackable = FieldPropertyUtility.StringToBool(FieldPropertyUtility.LookupValue(fieldsProperty, "Trackable"));
            parent.Add(CreateOptionalToggle("Trackable", "Tick to mark this quest trackable in a gameplay HUD.", fieldsProperty, "Trackable", value => DeferRebuildDetails()));
            if (isTrackable)
            {
                parent.Add(CreateOptionalToggle("Track on Start", "Tick to show in HUD when the quest becomes active without the player having to toggle tracking on in the quest log window first.", fieldsProperty, "Track"));
            }
            parent.Add(CreateOptionalToggle("Abandonable", "Tick to mark this quest abandonable in the quest window.", fieldsProperty, "Abandonable"));

            var hasEntries = FieldPropertyUtility.FindField(fieldsProperty, EntryCountFieldTitle) != null;
            var hasEntriesToggle = new UnityEngine.UIElements.Toggle("Has Entries (Subtasks)") { value = hasEntries, tooltip = "Tick to add quest entries to this quest." };
            hasEntriesToggle.RegisterValueChangedCallback(evt => SetHasQuestEntries(fieldsProperty, evt.newValue));
            parent.Add(hasEntriesToggle);

            AddPrimaryFields(parent, fieldsProperty, false, title => QuestBuiltInFieldTitles.Contains(title) ||
                title.StartsWith("Description") || title.StartsWith("Success Description") || title.StartsWith("Failure Description"));

            languages = GetQuestLanguages();
            AddQuestTextArea(parent, "Description", "The description when the quest is active.", fieldsProperty);
            AddLocalizedVersions(parent, fieldsProperty, "Description", true);
            AddQuestTextArea(parent, "Success Description", "The description when the quest has been completed successfully. If blank, the Description field is used.", fieldsProperty);
            AddLocalizedVersions(parent, fieldsProperty, "Success Description", true);
            AddQuestTextArea(parent, "Failure Description", "The description when the quest has failed. If blank, the Description field is used.", fieldsProperty);
            AddLocalizedVersions(parent, fieldsProperty, "Failure Description", true);

            if (hasEntries) AddQuestEntries(parent, fieldsProperty);
        }

        /// <summary>Optional quest text area with the AI addon's revise button, if any.</summary>
        private void AddQuestTextArea(VisualElement parent, string title, string tooltip, SerializedProperty fieldsProperty)
        {
            var editor = CreateOptionalTextField(title, tooltip, fieldsProperty, title, true);
            DialogueDatabaseEditorWindow.AddAIButtonToLabelRow(editor, CreateAIReviseTextButton(title));
            parent.Add(editor);
        }

        private void AddGroupFields(VisualElement parent, SerializedProperty fieldsProperty)
        {
            var groupProperty = FieldPropertyUtility.FindField(fieldsProperty, GroupFieldTitle);
            var toggle = new UnityEngine.UIElements.Toggle("Use Groups") { value = groupProperty != null, tooltip = "Tick to organize this quest under a quest group." };
            toggle.RegisterValueChangedCallback(evt =>
            {
                serializedObject.Update();
                if (evt.newValue)
                {
                    FieldPropertyUtility.SetFieldValue(fieldsProperty, GroupFieldTitle, string.Empty, FieldType.Text);
                }
                else
                {
                    FieldPropertyUtility.RemoveField(fieldsProperty, GroupFieldTitle);
                }
                DeferRebuildDetails();
            });
            parent.Add(toggle);
            if (groupProperty != null)
            {
                var groupEditor = FieldPropertyUtility.CreateValueEditor(groupProperty, database, "Group");
                groupEditor.tooltip = "The group this quest belongs to.";
                parent.Add(DialogueDatabaseEditorWindow.WithAIButton(groupEditor, CreateAIReviseTextButton(GroupFieldTitle)));
            }
        }

        #endregion

        #region Localization

        // Localized versions of a field are Localization fields titled "<field> <language>",
        // such as "Description fr" or "Entry 1 fr".

        private static readonly string[] LocalizableQuestFieldPrefixes = { "Description ", "Success Description ", "Failure Description " };
        private static readonly System.Text.RegularExpressions.Regex LocalizedEntryTitleRegex = new System.Text.RegularExpressions.Regex(@"^Entry \d+ (.+)$");

        private List<string> languages = new List<string>();

        /// <summary>
        /// Returns the languages used by localized quest descriptions and entries anywhere in
        /// the database, sorted, so every quest shows the same set of languages.
        /// </summary>
        private List<string> GetQuestLanguages()
        {
            var result = new List<string>();
            if (database == null) return result;
            foreach (var item in database.items)
            {
                if (item.fields == null) continue;
                foreach (var field in item.fields)
                {
                    if (field.type != FieldType.Localization || string.IsNullOrEmpty(field.title)) continue;
                    string language = null;
                    foreach (var prefix in LocalizableQuestFieldPrefixes)
                    {
                        if (field.title.StartsWith(prefix)) language = field.title.Substring(prefix.Length);
                    }
                    if (language == null)
                    {
                        var match = LocalizedEntryTitleRegex.Match(field.title);
                        if (match.Success) language = match.Groups[1].Value;
                    }
                    if (!string.IsNullOrWhiteSpace(language) && !result.Contains(language)) result.Add(language);
                }
            }
            result.Sort(StringComparer.OrdinalIgnoreCase);
            return result;
        }

        private bool IsLocalizedVersion(string title, string baseTitle)
        {
            foreach (var language in languages)
            {
                if (title == baseTitle + " " + language) return true;
            }
            return false;
        }

        /// <summary>
        /// Adds a collapsible "Localized [field] (n)" foldout with an editor for each existing
        /// localized version of the field. Adds nothing if the field has no localized versions.
        /// </summary>
        private void AddLocalizedVersions(VisualElement parent, SerializedProperty fieldsProperty, string baseTitle, bool multiline)
        {
            var localizedFields = new List<KeyValuePair<string, SerializedProperty>>();
            foreach (var language in languages)
            {
                var fieldProperty = FieldPropertyUtility.FindField(fieldsProperty, baseTitle + " " + language);
                if (fieldProperty != null) localizedFields.Add(new KeyValuePair<string, SerializedProperty>(language, fieldProperty));
            }
            if (localizedFields.Count == 0) return;

            var foldout = new Foldout()
            {
                text = string.Format("Localized {0} ({1})", baseTitle, localizedFields.Count),
                value = itemsState.expandedLocalizations.Contains(baseTitle),
                tooltip = string.Format("Translations of {0}.", baseTitle)
            };
            foldout.RegisterValueChangedCallback(evt =>
            {
                if (evt.target != foldout) return;
                itemsState.expandedLocalizations.Remove(baseTitle);
                if (evt.newValue) itemsState.expandedLocalizations.Add(baseTitle);
            });
            foreach (var pair in localizedFields)
            {
                var valueProperty = pair.Value.FindPropertyRelative(FieldPropertyUtility.ValuePropertyName);
                if (multiline)
                {
                    foldout.Add(CreateTextAreaLabel(pair.Key, baseTitle + " " + pair.Key));
                    foldout.Add(FieldPropertyUtility.CreateTextField(null, valueProperty, true));
                }
                else
                {
                    var textField = FieldPropertyUtility.CreateTextField(pair.Key, valueProperty, false);
                    textField.tooltip = baseTitle + " " + pair.Key;
                    foldout.Add(textField);
                }
            }
            parent.Add(foldout);
        }

        #endregion

        #region Quest Entries

        private static string EntryTitle(int entryNumber)
        {
            return "Entry " + entryNumber;
        }

        /// <summary>
        /// True if the title is "Entry #" or starts with "Entry # ". Note "Entry 1 " doesn't match "Entry 10 ...".
        /// </summary>
        private static bool IsEntryField(string title, int entryNumber)
        {
            if (string.IsNullOrEmpty(title)) return false;
            var entryTitle = EntryTitle(entryNumber);
            return title == entryTitle || title.StartsWith(entryTitle + " ");
        }

        private static int GetEntryCount(SerializedProperty fieldsProperty)
        {
            int count;
            return int.TryParse(FieldPropertyUtility.LookupValue(fieldsProperty, EntryCountFieldTitle), out count) ? Mathf.Max(0, count) : 0;
        }

        private static string GetFieldTitle(SerializedProperty fieldsProperty, int index)
        {
            return fieldsProperty.GetArrayElementAtIndex(index).FindPropertyRelative(FieldPropertyUtility.TitlePropertyName).stringValue;
        }

        private void SetHasQuestEntries(SerializedProperty fieldsProperty, bool hasEntries)
        {
            serializedObject.Update();
            if (hasEntries)
            {
                FieldPropertyUtility.SetFieldValue(fieldsProperty, EntryCountFieldTitle, "0", FieldType.Number);
                Undo.SetCurrentGroupName("Add Quest Entries");
            }
            else
            {
                var count = GetEntryCount(fieldsProperty);
                if (count == 0 || EditorUtility.DisplayDialog("Delete all entries?",
                    string.Format("This quest has {0} {1}. Delete {2}?", count, (count == 1) ? "entry" : "entries", (count == 1) ? "it" : "them"), "Delete", "Cancel"))
                {
                    for (int i = fieldsProperty.arraySize - 1; i >= 0; i--)
                    {
                        if (GetFieldTitle(fieldsProperty, i).StartsWith("Entry ")) fieldsProperty.DeleteArrayElementAtIndex(i);
                    }
                    serializedObject.ApplyModifiedProperties();
                    Undo.SetCurrentGroupName("Remove Quest Entries");
                }
            }
            DeferRebuildDetails(); // Also resets the toggle if the user cancelled.
        }

        private void AddQuestEntries(VisualElement parent, SerializedProperty fieldsProperty)
        {
            var section = new VisualElement();
            section.style.marginTop = 8;
            parent.Add(section);

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            var title = new Label("Quest Entries");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.flexGrow = 1;
            header.Add(title);
            var compactToggle = new UnityEngine.UIElements.Toggle("Compact View") { value = itemsState.compactQuestEntries, tooltip = "Show one entry at a time." };
            compactToggle.labelElement.style.minWidth = 0;
            compactToggle.RegisterValueChangedCallback(evt =>
            {
                itemsState.compactQuestEntries = evt.newValue;
                DeferRebuildDetails();
            });
            header.Add(compactToggle);
            section.Add(header);

            var count = GetEntryCount(fieldsProperty);
            itemsState.selectedQuestEntry = Mathf.Clamp(itemsState.selectedQuestEntry, 0, Mathf.Max(0, count - 1));
            var isCompact = itemsState.compactQuestEntries;
            if (isCompact && count > 0)
            {
                var tabs = new UnityEditor.UIElements.Toolbar();
                tabs.style.flexWrap = Wrap.Wrap;
                tabs.style.height = StyleKeyword.Auto;
                for (int i = 0; i < count; i++)
                {
                    var entryIndex = i;
                    var tab = new ToolbarToggle() { text = EntryTitle(i + 1), value = (i == itemsState.selectedQuestEntry) };
                    tab.RegisterValueChangedCallback(evt =>
                    {
                        itemsState.selectedQuestEntry = entryIndex;
                        DeferRebuildDetails();
                    });
                    tabs.Add(tab);
                }
                section.Add(tabs);
            }

            for (int entryNumber = 1; entryNumber <= count; entryNumber++)
            {
                if (isCompact && entryNumber != itemsState.selectedQuestEntry + 1) continue;
                section.Add(CreateQuestEntryBox(fieldsProperty, entryNumber, count));
            }

            var addEntryButton = new Button(() => AddQuestEntry(fieldsProperty)) { text = "Add New Quest Entry", tooltip = "Adds a new quest entry to this quest." };
            addEntryButton.style.marginTop = 4;
            section.Add(addEntryButton);
        }

        private VisualElement CreateQuestEntryBox(SerializedProperty fieldsProperty, int entryNumber, int count)
        {
            var entryTitle = EntryTitle(entryNumber);
            var box = new VisualElement();
            box.style.marginTop = 4;
            box.style.paddingLeft = 4;
            box.style.paddingRight = 4;
            box.style.paddingTop = 2;
            box.style.paddingBottom = 4;
            box.AddToClassList("ds-box");

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            var headerLabel = new Label(entryTitle);
            headerLabel.style.flexGrow = 1;
            headerLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.Add(headerLabel);
            var upButton = new Button(() => SwapQuestEntries(fieldsProperty, entryNumber, entryNumber - 1)) { text = "↑", tooltip = "Move up" };
            upButton.SetEnabled(entryNumber > 1);
            header.Add(upButton);
            var downButton = new Button(() => SwapQuestEntries(fieldsProperty, entryNumber, entryNumber + 1)) { text = "↓", tooltip = "Move down" };
            downButton.SetEnabled(entryNumber < count);
            header.Add(downButton);
            header.Add(new Button(() => DeleteQuestEntry(fieldsProperty, entryNumber)) { text = "-", tooltip = "Delete this entry." });
            box.Add(header);

            var stateTitle = entryTitle + " State";
            box.Add(CreateOptionalQuestStatePopup("State", "The starting state of this entry.", fieldsProperty, stateTitle));
            box.Add(DialogueDatabaseEditorWindow.WithAIButton(CreateOptionalTextField(entryTitle, "The entry's text.", fieldsProperty, entryTitle, false),
                CreateAIReviseTextButton(entryTitle)));
            AddLocalizedVersions(box, fieldsProperty, entryTitle, false);

            // Other "Entry # ..." fields, such as localized text and custom fields:
            var idTitle = entryTitle + " ID";
            for (int i = 0; i < fieldsProperty.arraySize; i++)
            {
                var fieldProperty = fieldsProperty.GetArrayElementAtIndex(i);
                var title = fieldProperty.FindPropertyRelative(FieldPropertyUtility.TitlePropertyName).stringValue;
                if (!IsEntryField(title, entryNumber) || title == entryTitle || title == stateTitle || title == idTitle) continue;
                if (IsLocalizedVersion(title, entryTitle)) continue; // Shown in the Localized foldout above.
                if (FieldPropertyUtility.GetEffectiveTypeString(fieldProperty) == "CustomFieldType_Text")
                {
                    box.Add(CreateTextAreaLabel(title, null));
                    box.Add(FieldPropertyUtility.CreateTextField(null, fieldProperty.FindPropertyRelative(FieldPropertyUtility.ValuePropertyName), true));
                }
                else
                {
                    box.Add(FieldPropertyUtility.CreateValueEditor(fieldProperty, database, title));
                }
            }

            box.Add(CreateAddEntryFieldControls(fieldsProperty, entryNumber));
            return box;
        }

        /// <summary>
        /// "Add New Field To Entry" button, which turns into a name/type row with Create and Cancel.
        /// </summary>
        private VisualElement CreateAddEntryFieldControls(SerializedProperty fieldsProperty, int entryNumber)
        {
            var container = new VisualElement();
            container.style.marginTop = 4;

            var addButton = new Button() { text = "Add New Field To Entry" };
            container.Add(addButton);

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.display = DisplayStyle.None;
            var nameField = new TextField() { tooltip = string.Format("Field name. It will be prefixed with \"{0} \".", EntryTitle(entryNumber)) };
            nameField.style.flexGrow = 1;
            row.Add(nameField);
            var typeNames = CustomFieldTypeService.GetDialogueSystemTypes();
            var publicNames = CustomFieldTypeService.GetDialogueSystemPublicNames();
            Func<string, string> formatType = typeName =>
            {
                var index = Array.IndexOf(typeNames, typeName);
                return (index >= 0 && index < publicNames.Length) ? publicNames[index] : typeName;
            };
            var defaultType = Array.IndexOf(typeNames, "CustomFieldType_Text") >= 0 ? "CustomFieldType_Text" : typeNames[0];
            var typePopup = new PopupField<string>(new List<string>(typeNames), defaultType, formatType, formatType);
            typePopup.style.width = 110;
            row.Add(typePopup);
            var createButton = new Button() { text = "Create" };
            createButton.SetEnabled(false);
            row.Add(createButton);
            row.Add(new Button(() =>
            {
                row.style.display = DisplayStyle.None;
                addButton.style.display = DisplayStyle.Flex;
            }) { text = "Cancel" });
            container.Add(row);

            addButton.clicked += () =>
            {
                addButton.style.display = DisplayStyle.None;
                row.style.display = DisplayStyle.Flex;
                nameField.SetValueWithoutNotify(string.Empty);
                createButton.SetEnabled(false);
                nameField.Focus();
            };
            nameField.RegisterValueChangedCallback(evt => createButton.SetEnabled(!string.IsNullOrWhiteSpace(evt.newValue)));
            createButton.clicked += () =>
            {
                var title = EntryTitle(entryNumber) + " " + nameField.value.Trim();
                serializedObject.Update();
                if (FieldPropertyUtility.FindField(fieldsProperty, title) != null)
                {
                    EditorUtility.DisplayDialog("Field Exists", string.Format("This quest already has a field named '{0}'.", title), "OK");
                    return;
                }
                CustomFieldType customFieldType;
                var storeAsType = CustomFieldTypeService.GetTypesDictionary().TryGetValue(typePopup.value, out customFieldType)
                    ? customFieldType.storeFieldAsType : FieldType.Text;
                FieldPropertyUtility.AddField(fieldsProperty, title, string.Empty, storeAsType, typePopup.value);
                serializedObject.ApplyModifiedProperties();
                Undo.SetCurrentGroupName("Add Quest Entry Field");
                DeferRebuildDetails();
            };
            return container;
        }

        private void AddQuestEntry(SerializedProperty fieldsProperty)
        {
            serializedObject.Update();
            var entryNumber = GetEntryCount(fieldsProperty) + 1;
            var entryTitle = EntryTitle(entryNumber);
            FieldPropertyUtility.SetFieldValue(fieldsProperty, EntryCountFieldTitle, entryNumber.ToString(), FieldType.Number, false);
            FieldPropertyUtility.SetFieldValue(fieldsProperty, entryTitle + " State", "unassigned", FieldType.Text, false);
            FieldPropertyUtility.SetFieldValue(fieldsProperty, entryTitle, string.Empty, FieldType.Text, false);

            // Add localized entry text for each language the quest's Description is localized in:
            var languages = new List<string>();
            for (int i = 0; i < fieldsProperty.arraySize; i++)
            {
                var title = GetFieldTitle(fieldsProperty, i);
                if (!title.StartsWith("Description ")) continue;
                var language = title.Substring("Description ".Length);
                if (!string.IsNullOrWhiteSpace(language) && !languages.Contains(language)) languages.Add(language);
            }
            foreach (var language in languages)
            {
                var localizedTitle = entryTitle + " " + language;
                if (FieldPropertyUtility.FindField(fieldsProperty, localizedTitle) == null)
                {
                    FieldPropertyUtility.AddField(fieldsProperty, localizedTitle, string.Empty, FieldType.Localization);
                }
            }

            // Copy any custom "Entry 1 ..." fields to the new entry, clearing text values:
            if (entryNumber > 1)
            {
                var count = fieldsProperty.arraySize;
                for (int i = 0; i < count; i++)
                {
                    var field = FieldPropertyUtility.ToField(fieldsProperty.GetArrayElementAtIndex(i));
                    if (!field.title.StartsWith("Entry 1 ") || field.title == "Entry 1 State") continue;
                    var suffix = field.title.Substring("Entry 1 ".Length);
                    if (languages.Contains(suffix)) continue;
                    var newTitle = entryTitle + " " + suffix;
                    if (FieldPropertyUtility.FindField(fieldsProperty, newTitle) != null) continue;
                    var value = (field.type == FieldType.Text) ? string.Empty : field.value;
                    FieldPropertyUtility.AddField(fieldsProperty, newTitle, value, field.type, field.typeString);
                }
            }

            serializedObject.ApplyModifiedProperties();
            Undo.SetCurrentGroupName("Add Quest Entry");
            itemsState.selectedQuestEntry = entryNumber - 1;
            DeferRebuildDetails();
        }

        private void DeleteQuestEntry(SerializedProperty fieldsProperty, int entryNumber)
        {
            if (!EditorUtility.DisplayDialog(string.Format("Delete entry {0}?", entryNumber),
                "Are you sure you want to delete this quest entry?", "Delete", "Cancel")) return;
            RemoveQuestEntry(fieldsProperty, entryNumber);
        }

        /// <summary>
        /// Deletes an entry's fields, renumbers the entries after it, and decrements Entry Count.
        /// </summary>
        private void RemoveQuestEntry(SerializedProperty fieldsProperty, int entryNumber)
        {
            serializedObject.Update();
            var count = GetEntryCount(fieldsProperty);

            for (int i = fieldsProperty.arraySize - 1; i >= 0; i--)
            {
                if (IsEntryField(GetFieldTitle(fieldsProperty, i), entryNumber)) fieldsProperty.DeleteArrayElementAtIndex(i);
            }

            // Renumber the entries after the deleted one:
            for (int laterEntry = entryNumber + 1; laterEntry <= count; laterEntry++)
            {
                RenumberEntryFields(fieldsProperty, laterEntry, laterEntry - 1);
            }

            FieldPropertyUtility.SetFieldValue(fieldsProperty, EntryCountFieldTitle, (count - 1).ToString(), FieldType.Number, false);
            serializedObject.ApplyModifiedProperties();
            Undo.SetCurrentGroupName("Delete Quest Entry");

            var deletedIndex = entryNumber - 1;
            if (itemsState.selectedQuestEntry >= deletedIndex) itemsState.selectedQuestEntry = Mathf.Max(0, itemsState.selectedQuestEntry - 1);
            DeferRebuildDetails();
        }

        private void SwapQuestEntries(SerializedProperty fieldsProperty, int entryA, int entryB)
        {
            serializedObject.Update();
            var count = GetEntryCount(fieldsProperty);
            if (!(1 <= entryB && entryB <= count)) return;
            // Titles are computed from the original title, so a single pass swaps both entries.
            for (int i = 0; i < fieldsProperty.arraySize; i++)
            {
                var titleProperty = fieldsProperty.GetArrayElementAtIndex(i).FindPropertyRelative(FieldPropertyUtility.TitlePropertyName);
                var title = titleProperty.stringValue;
                if (IsEntryField(title, entryA))
                {
                    titleProperty.stringValue = EntryTitle(entryB) + title.Substring(EntryTitle(entryA).Length);
                }
                else if (IsEntryField(title, entryB))
                {
                    titleProperty.stringValue = EntryTitle(entryA) + title.Substring(EntryTitle(entryB).Length);
                }
            }
            serializedObject.ApplyModifiedProperties();
            Undo.SetCurrentGroupName("Move Quest Entry");
            itemsState.selectedQuestEntry = entryB - 1;
            DeferRebuildDetails();
        }

        private static void RenumberEntryFields(SerializedProperty fieldsProperty, int fromEntry, int toEntry)
        {
            for (int i = 0; i < fieldsProperty.arraySize; i++)
            {
                var titleProperty = fieldsProperty.GetArrayElementAtIndex(i).FindPropertyRelative(FieldPropertyUtility.TitlePropertyName);
                var title = titleProperty.stringValue;
                if (IsEntryField(title, fromEntry))
                {
                    titleProperty.stringValue = EntryTitle(toEntry) + title.Substring(EntryTitle(fromEntry).Length);
                }
            }
        }

        #endregion

    }

}

#endif
