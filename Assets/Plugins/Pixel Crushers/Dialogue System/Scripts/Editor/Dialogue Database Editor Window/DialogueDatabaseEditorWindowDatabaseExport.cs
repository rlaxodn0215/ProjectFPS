// Copyright (c) Pixel Crushers. All rights reserved.

#if UNITY_2022_1_OR_NEWER

using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;

namespace PixelCrushers.DialogueSystem.DialogueEditor
{

    /// <summary>
    /// Database panel's Export Database section: exports the database to text formats, and
    /// imports voiceover info and proofreading corrections back. Exports never change the
    /// database; steps that need changes, such as sorting entries, run on a temporary copy.
    /// </summary>
    public partial class DatabasePanel
    {

        public enum ExportFormat
        {
            [InspectorName("Chat Mapper XML")] ChatMapperXML,
            [InspectorName("JSON")] JSON,
            [InspectorName("CSV")] CSV,
            [InspectorName("Voiceover Script")] VoiceoverScript,
            [InspectorName("Language Text")] LanguageText,
            [InspectorName("Screenplay")] Screenplay,
            [InspectorName("Proofreading")] Proofreading
        }

        private const float ExportLabelWidth = 180;
        private const float ExportButtonWidth = 100;

        private EnumField exportFormatField = null;
        private HelpBox exportDescriptionBox = null;
        private VisualElement exportAssetToggles = null;
        private UnityEngine.UIElements.Toggle exportActorsToggle = null;
        private UnityEngine.UIElements.Toggle exportItemsToggle = null;
        private UnityEngine.UIElements.Toggle exportLocationsToggle = null;
        private UnityEngine.UIElements.Toggle exportVariablesToggle = null;
        private UnityEngine.UIElements.Toggle exportConversationsToggle = null;
        private UnityEngine.UIElements.Toggle exportCanvasRectToggle = null;
        private UnityEngine.UIElements.Toggle exportConversationsAfterEntriesToggle = null;
        private UnityEngine.UIElements.Toggle exportTitleColumnToggle = null;
        private UnityEngine.UIElements.Toggle exportSpecificActorToggle = null;
        private VisualElement exportActorPopupHost = null;
        private PopupField<int> exportActorPopup = null;
        private UnityEngine.UIElements.Toggle exportOmitHiddenToggle = null;
        private EnumField exportSortField = null;
        private TextField exportInfoField = null;
        private EnumField exportEntrytagField = null;
        private EnumField exportEncodingField = null;
        private Button exportButton = null;
        private Button exportImportButton = null;

        #region UI

        // The panel is created before its window is assigned, so the fields are filled from
        // the window's state later, in RefreshExportFields.
        private void AddExportSection(VisualElement parent)
        {
            exportFormatField = new EnumField("Format", ExportFormat.ChatMapperXML) { tooltip = "File format to export." };
            exportFormatField.labelElement.style.minWidth = ExportLabelWidth;
            exportFormatField.RegisterValueChangedCallback(evt =>
            {
                if (window == null || !(evt.newValue is ExportFormat)) return;
                databaseState.exportFormat = (ExportFormat)evt.newValue;
                RefreshExportVisibility();
            });
            parent.Add(exportFormatField);

            exportDescriptionBox = new HelpBox(string.Empty, UnityEngine.UIElements.HelpBoxMessageType.None);
            exportDescriptionBox.style.marginTop = 4;
            exportDescriptionBox.style.marginBottom = 4;
            parent.Add(exportDescriptionBox);

            exportAssetToggles = new VisualElement();
            exportActorsToggle = AddExportToggle(exportAssetToggles, "Export Actors", "Include actors.", (state, value) => state.exportActors = value);
            exportItemsToggle = AddExportToggle(exportAssetToggles, "Export Items/Quests", "Include items and quests.", (state, value) => state.exportItems = value);
            exportLocationsToggle = AddExportToggle(exportAssetToggles, "Export Locations", "Include locations.", (state, value) => state.exportLocations = value);
            exportVariablesToggle = AddExportToggle(exportAssetToggles, "Export Variables", "Include variables.", (state, value) => state.exportVariables = value);
            exportConversationsToggle = AddExportToggle(exportAssetToggles, "Export Conversations", "Include conversations.", (state, value) => state.exportConversations = value);
            parent.Add(exportAssetToggles);

            exportCanvasRectToggle = AddExportToggle(parent, "Export Canvas Positions", "Export the positions of dialogue entry nodes in the conversation canvas.",
                (state, value) => state.exportCanvasRect = value);
            exportConversationsAfterEntriesToggle = AddExportToggle(parent, "Convs. After Entries", "Put the Conversations section after the DialogueEntries section in the CSV file. Normally the Conversations section is before.",
                (state, value) => state.exportConversationsAfterEntries = value);
            exportTitleColumnToggle = AddExportToggle(parent, "Conv. Title in Sep. Column", "Add a separate column for conversation titles.",
                (state, value) => state.exportConversationTitleSeparateColumn = value);
            exportSpecificActorToggle = AddExportToggle(parent, "Specify Actor", "Export lines only for a specified actor.",
                (state, value) => { state.exportSpecificActor = value; RefreshExportVisibility(); });
            exportActorPopupHost = new VisualElement();
            parent.Add(exportActorPopupHost);
            exportOmitHiddenToggle = AddExportToggle(parent, "Omit Hidden Lines", "Omit entries whose Sequence fields are None() or Continue().",
                (state, value) => state.exportOmitHiddenLines = value);

            exportSortField = new EnumField("Sort Dialogue Entries", DialogueEntrySorter.SortMode.DoNotSort)
            {
                tooltip = "Order of each conversation's entries in the exported file. Sorting doesn't change the database."
            };
            exportSortField.labelElement.style.minWidth = ExportLabelWidth;
            exportSortField.RegisterValueChangedCallback(evt =>
            {
                if (window == null || !(evt.newValue is DialogueEntrySorter.SortMode)) return;
                databaseState.exportSortMode = (DialogueEntrySorter.SortMode)evt.newValue;
            });
            parent.Add(exportSortField);

            exportInfoField = new TextField("Info Field") { tooltip = "Optional dialogue entry field to store info such as whether voiceover was recorded, filename, etc. Import updates this field from the CSV file." };
            exportInfoField.labelElement.style.minWidth = ExportLabelWidth;
            exportInfoField.RegisterValueChangedCallback(evt =>
            {
                if (window == null) return;
                databaseState.exportVoiceoverInfoFieldName = evt.newValue;
                RefreshExportVisibility();
            });
            parent.Add(exportInfoField);

            exportEntrytagField = new EnumField("Entrytag Format", EntrytagFormat.ActorName_ConversationID_EntryID)
            {
                tooltip = "Format of entrytags in the exported file. It should match the Dialogue Manager's Camera & Cutscene Settings > Entrytag Format."
            };
            exportEntrytagField.labelElement.style.minWidth = ExportLabelWidth;
            exportEntrytagField.RegisterValueChangedCallback(evt =>
            {
                if (window == null || !(evt.newValue is EntrytagFormat)) return;
                databaseState.exportEntrytagFormat = (EntrytagFormat)evt.newValue;
                databaseState.exportEntrytagFormatSet = true;
            });
            parent.Add(exportEntrytagField);

            exportEncodingField = new EnumField("Encoding", EncodingType.UTF8) { tooltip = "Text encoding of the exported files." };
            exportEncodingField.labelElement.style.minWidth = ExportLabelWidth;
            exportEncodingField.RegisterValueChangedCallback(evt =>
            {
                if (window == null || !(evt.newValue is EncodingType)) return;
                databaseState.exportEncodingType = (EncodingType)evt.newValue;
            });
            parent.Add(exportEncodingField);

            var buttonRow = new VisualElement();
            buttonRow.style.flexDirection = FlexDirection.Row;
            buttonRow.style.justifyContent = Justify.FlexEnd;
            buttonRow.style.marginTop = 6;
            exportImportButton = new Button(ChooseFileAndImport) { text = "Import..." };
            exportImportButton.style.width = ExportButtonWidth;
            buttonRow.Add(exportImportButton);
            exportButton = new Button(ChooseFileAndExport) { text = "Export...", tooltip = "Choose a file and export the database to it." };
            exportButton.style.width = ExportButtonWidth;
            buttonRow.Add(exportButton);
            parent.Add(buttonRow);

            RefreshExportVisibility();
        }

        private UnityEngine.UIElements.Toggle AddExportToggle(VisualElement parent, string label, string tooltip,
            System.Action<DialogueDatabaseEditorWindow.DatabasePanelState, bool> setState)
        {
            var toggle = new UnityEngine.UIElements.Toggle(label) { tooltip = tooltip };
            toggle.labelElement.style.minWidth = ExportLabelWidth;
            toggle.RegisterValueChangedCallback(evt =>
            {
                if (window == null) return;
                setState(databaseState, evt.newValue);
            });
            parent.Add(toggle);
            return toggle;
        }

        /// <summary>Shows the window's saved export options in the fields.</summary>
        private void RefreshExportFields()
        {
            if (window == null || exportFormatField == null) return;
            var state = databaseState;
            if (!state.exportEntrytagFormatSet)
            {
                var dialogueManager = PixelCrushers.GameObjectUtility.FindFirstObjectByType<DialogueSystemController>();
                if (dialogueManager != null) state.exportEntrytagFormat = dialogueManager.displaySettings.cameraSettings.entrytagFormat;
            }
            exportFormatField.SetValueWithoutNotify(state.exportFormat);
            exportActorsToggle.SetValueWithoutNotify(state.exportActors);
            exportItemsToggle.SetValueWithoutNotify(state.exportItems);
            exportLocationsToggle.SetValueWithoutNotify(state.exportLocations);
            exportVariablesToggle.SetValueWithoutNotify(state.exportVariables);
            exportConversationsToggle.SetValueWithoutNotify(state.exportConversations);
            exportCanvasRectToggle.SetValueWithoutNotify(state.exportCanvasRect);
            exportConversationsAfterEntriesToggle.SetValueWithoutNotify(state.exportConversationsAfterEntries);
            exportTitleColumnToggle.SetValueWithoutNotify(state.exportConversationTitleSeparateColumn);
            exportSpecificActorToggle.SetValueWithoutNotify(state.exportSpecificActor);
            exportOmitHiddenToggle.SetValueWithoutNotify(state.exportOmitHiddenLines);
            exportSortField.SetValueWithoutNotify(state.exportSortMode);
            exportInfoField.SetValueWithoutNotify(state.exportVoiceoverInfoFieldName ?? string.Empty);
            exportEntrytagField.SetValueWithoutNotify(state.exportEntrytagFormat);
            exportEncodingField.SetValueWithoutNotify(state.exportEncodingType);
            RebuildExportActorPopup();
            RefreshExportVisibility();
        }

        /// <summary>
        /// Rebuilds the Actor popup from the database's actors. PopupField choices can't be
        /// replaced in Unity 2022, so this makes a new popup.
        /// </summary>
        private void RebuildExportActorPopup()
        {
            if (exportActorPopupHost == null) return;
            exportActorPopupHost.Clear();
            exportActorPopup = null;
            if (window == null || database == null || database.actors.Count == 0) return;
            var ids = new List<int>();
            database.actors.ForEach(actor => ids.Add(actor.id));
            var state = databaseState;
            var index = ids.IndexOf(state.exportActorID);
            if (index < 0)
            {
                index = 0;
                state.exportActorID = ids[0];
            }
            exportActorPopup = new PopupField<int>("Actor", ids, index, FormatExportActor, FormatExportActor) { tooltip = "Actor whose lines to export." };
            exportActorPopup.labelElement.style.minWidth = ExportLabelWidth;
            exportActorPopup.RegisterValueChangedCallback(evt =>
            {
                if (window != null) databaseState.exportActorID = evt.newValue;
            });
            exportActorPopupHost.Add(exportActorPopup);
        }

        private string FormatExportActor(int actorID)
        {
            var actor = (database != null) ? database.GetActor(actorID) : null;
            return (actor != null) ? actor.Name : string.Format("[{0}]", actorID);
        }

        private static string GetExportDescription(ExportFormat format)
        {
            switch (format)
            {
                case ExportFormat.ChatMapperXML:
                    return "Export to Chat Mapper XML format. To import into Chat Mapper, prepare a Chat Mapper template project that contains all the fields listed in the _Template.txt file that's exported with the XML file. You can use the Dialogue System Chat Mapper template project as a base. To reimport into the Dialogue System, use Tools > Pixel Crushers > Dialogue System > Import > Chat Mapper.";
                case ExportFormat.JSON:
                    return "Export to JSON format. To reimport into the Dialogue System, use Tools > Pixel Crushers > Dialogue System > Import > JSON.";
                case ExportFormat.CSV:
                    return "Export to CSV format, which you can open in spreadsheet programs such as Excel and Google Sheets. To reimport into the Dialogue System, use Tools > Pixel Crushers > Dialogue System > Import > CSV.";
                case ExportFormat.VoiceoverScript:
                    return "Export a CSV file for each language that you can use as a guide to record voice actors. Each row specifies the entrytag filename for use with entrytags. If you set an Info Field, you can update it in the CSV file and import it back.";
                case ExportFormat.LanguageText:
                    return "Export a file for each language that contains all of the language's localized text. You can use these files to determine which characters your language-specific fonts need to support.";
                case ExportFormat.Screenplay:
                    return "Export a screenplay text file for each language.";
                case ExportFormat.Proofreading:
                    return "Export a proofreading text file for each language. After making corrections, you can import the files back.";
                default:
                    return string.Empty;
            }
        }

        /// <summary>Shows the fields that the selected format uses.</summary>
        private void RefreshExportVisibility()
        {
            if (exportButton == null) return;
            var format = (window != null) ? databaseState.exportFormat : ExportFormat.ChatMapperXML;
            var isVoiceover = format == ExportFormat.VoiceoverScript;
            var usesAssetToggles = format == ExportFormat.ChatMapperXML || format == ExportFormat.CSV || isVoiceover;
            exportDescriptionBox.text = GetExportDescription(format);
            SetShown(exportAssetToggles, usesAssetToggles);
            SetShown(exportCanvasRectToggle, format == ExportFormat.ChatMapperXML);
            SetShown(exportConversationsAfterEntriesToggle, format == ExportFormat.CSV);
            SetShown(exportTitleColumnToggle, isVoiceover);
            SetShown(exportSpecificActorToggle, isVoiceover);
            SetShown(exportActorPopupHost, isVoiceover && window != null && databaseState.exportSpecificActor);
            SetShown(exportOmitHiddenToggle, format == ExportFormat.Screenplay);
            SetShown(exportSortField, isVoiceover || format == ExportFormat.Proofreading);
            SetShown(exportInfoField, isVoiceover);
            SetShown(exportEntrytagField, usesAssetToggles);
            SetShown(exportEncodingField, isVoiceover || format == ExportFormat.LanguageText ||
                format == ExportFormat.Screenplay || format == ExportFormat.Proofreading);

            var canImport = isVoiceover || format == ExportFormat.Proofreading;
            SetShown(exportImportButton, canImport);
            var hasInfoField = window != null && !string.IsNullOrEmpty(databaseState.exportVoiceoverInfoFieldName);
            exportImportButton.SetEnabled(!isVoiceover || hasInfoField);
            exportImportButton.tooltip = isVoiceover
                ? (hasInfoField ? "Update the Info Field in dialogue entries from a voiceover script CSV file." : "Set an Info Field to import voiceover script info.")
                : "Import corrections from proofreading files into this database.";
            exportButton.SetEnabled(window != null && database != null);
        }

        private static void SetShown(VisualElement element, bool shown)
        {
            if (element != null) element.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
        }

        #endregion

        #region Export

        private void GetExportFileInfo(ExportFormat format, out string title, out string extension)
        {
            switch (format)
            {
                case ExportFormat.ChatMapperXML: title = "Save Chat Mapper XML"; extension = "xml"; break;
                case ExportFormat.JSON: title = "Save JSON"; extension = "json"; break;
                case ExportFormat.CSV: title = "Save CSV"; extension = "csv"; break;
                case ExportFormat.VoiceoverScript: title = "Save Voiceover Scripts"; extension = "csv"; break;
                case ExportFormat.LanguageText: title = "Save Language Text"; extension = "txt"; break;
                case ExportFormat.Screenplay: title = "Save Screenplays"; extension = "txt"; break;
                default: title = "Save Proofreading Text"; extension = "txt"; break;
            }
        }

        private string GetLastExportPath(ExportFormat format)
        {
            var state = databaseState;
            switch (format)
            {
                case ExportFormat.ChatMapperXML: return state.chatMapperExportPath;
                case ExportFormat.JSON: return state.jsonExportPath;
                case ExportFormat.CSV: return state.csvExportPath;
                case ExportFormat.VoiceoverScript: return state.voiceoverExportPath;
                case ExportFormat.LanguageText: return state.languageTextExportPath;
                case ExportFormat.Screenplay: return state.screenplayExportPath;
                default: return state.proofreadingExportPath;
            }
        }

        private void SetLastExportPath(ExportFormat format, string path)
        {
            var state = databaseState;
            switch (format)
            {
                case ExportFormat.ChatMapperXML: state.chatMapperExportPath = path; break;
                case ExportFormat.JSON: state.jsonExportPath = path; break;
                case ExportFormat.CSV: state.csvExportPath = path; break;
                case ExportFormat.VoiceoverScript: state.voiceoverExportPath = path; break;
                case ExportFormat.LanguageText: state.languageTextExportPath = path; break;
                case ExportFormat.Screenplay: state.screenplayExportPath = path; break;
                default: state.proofreadingExportPath = path; break;
            }
        }

        private void ChooseFileAndExport()
        {
            if (window == null || database == null) return;
            var format = databaseState.exportFormat;
            GetExportFileInfo(format, out var title, out var extension);
            var lastPath = GetLastExportPath(format);
            var folder = string.IsNullOrEmpty(lastPath) ? string.Empty : Path.GetDirectoryName(lastPath);
            var fileName = string.IsNullOrEmpty(lastPath) ? database.name : Path.GetFileNameWithoutExtension(lastPath);
            var path = EditorUtility.SaveFilePanel(title, folder, fileName, extension);
            if (string.IsNullOrEmpty(path)) return;
            SetLastExportPath(format, path);
            var message = ExportToPath(path);
            if (!string.IsNullOrEmpty(message)) EditorUtility.DisplayDialog("Export Complete", message, "OK");
        }

        /// <summary>
        /// Exports the database in the selected format, without changing it. Returns the
        /// message to show when it's done.
        /// </summary>
        private string ExportToPath(string path)
        {
            if (window == null || database == null || string.IsNullOrEmpty(path)) return null;
            if (serializedObject != null) serializedObject.ApplyModifiedProperties();
            var state = databaseState;
            // Exporters read the current Localization.language as the default language, and some
            // change it as they go, so export in the default language and restore it afterward.
            var language = Localization.language;
            Localization.language = string.Empty;
            DialogueDatabase copy = null;
            try
            {
                switch (state.exportFormat)
                {
                    case ExportFormat.ChatMapperXML:
                        copy = CreateExportCopy();
                        var template = JsonUtility.FromJson<Template>(JsonUtility.ToJson(window.Template));
                        ChatMapperExportPreparer.Prepare(copy, template, state.exportCanvasRect);
                        ChatMapperExporter.Export(copy, path, state.exportActors, state.exportItems, state.exportLocations,
                            state.exportVariables, state.exportConversations, state.exportCanvasRect);
                        var templatePath = ChatMapperExportPreparer.GetTemplateFieldListPath(path);
                        ChatMapperExportPreparer.WriteTemplateFieldList(path, templatePath, template);
                        return "The dialogue database was exported to Chat Mapper XML format.\n\n" +
                            "Remember to apply a template after importing it into Chat Mapper. " +
                            "The required fields in the template are listed in " + templatePath + ".\n\n" +
                            "If you have any issues importing, please contact us at support@pixelcrushers.com.";
                    case ExportFormat.JSON:
                        File.WriteAllText(path, JsonUtility.ToJson(database), System.Text.Encoding.UTF8);
                        return "The dialogue database was exported to JSON format.";
                    case ExportFormat.CSV:
                        CSVExporter.Export(database, path, state.exportActors, state.exportItems, state.exportLocations,
                            state.exportVariables, state.exportConversations, state.exportConversationsAfterEntries, state.exportEntrytagFormat);
                        return "The dialogue database was exported to CSV (comma-separated values) format.";
                    case ExportFormat.VoiceoverScript:
                        var actorName = state.exportSpecificActor ? GetExportActorName() : null;
                        copy = CreateSortedExportCopy(state.exportSortMode);
                        VoiceoverScriptExporter.Export(copy ?? database, path, state.exportActors, actorName, state.exportConversationTitleSeparateColumn,
                            state.exportEntrytagFormat, state.exportEncodingType, state.exportVoiceoverInfoFieldName);
                        return "The voiceover scripts were exported to CSV (comma-separated values) files in " + path + ".";
                    case ExportFormat.LanguageText:
                        LanguageTextExporter.Export(database, path, state.exportEncodingType);
                        return "The language texts have been exported to " + path + " with the language code appended to the end of each filename.";
                    case ExportFormat.Screenplay:
                        ScreenplayExporter.Export(database, path, state.exportEncodingType, state.exportOmitHiddenLines);
                        return "The screenplay files were exported to " + path + ".";
                    case ExportFormat.Proofreading:
                        copy = CreateSortedExportCopy(state.exportSortMode);
                        ProofreadingExporter.Export(copy ?? database, path, state.exportEncodingType);
                        return "The proofreading texts were exported to files in " + path + ".";
                    default:
                        return null;
                }
            }
            finally
            {
                if (copy != null) Object.DestroyImmediate(copy);
                Localization.language = language;
            }
        }

        /// <summary>A temporary copy of the database, with the same name, for exports that need to change it.</summary>
        private DialogueDatabase CreateExportCopy()
        {
            var copy = Object.Instantiate(database);
            copy.name = database.name;
            copy.hideFlags = HideFlags.HideAndDontSave;
            return copy;
        }

        /// <summary>A sorted copy of the database, or null if no sorting is needed.</summary>
        private DialogueDatabase CreateSortedExportCopy(DialogueEntrySorter.SortMode sortMode)
        {
            if (sortMode == DialogueEntrySorter.SortMode.DoNotSort) return null;
            var copy = CreateExportCopy();
            DialogueEntrySorter.SortAllConversations(copy, sortMode);
            return copy;
        }

        private string GetExportActorName()
        {
            var actor = database.GetActor(databaseState.exportActorID);
            return (actor != null) ? actor.Name : null;
        }

        #endregion

        #region Import

        private void ChooseFileAndImport()
        {
            if (window == null || database == null) return;
            var format = databaseState.exportFormat;
            if (format != ExportFormat.VoiceoverScript && format != ExportFormat.Proofreading) return;
            var lastPath = GetLastExportPath(format);
            var folder = string.IsNullOrEmpty(lastPath) ? string.Empty : Path.GetDirectoryName(lastPath);
            var path = (format == ExportFormat.VoiceoverScript)
                ? EditorUtility.OpenFilePanel("Import Voiceover CSV", folder, "csv")
                : EditorUtility.OpenFilePanel("Import Proofreading Text", folder, "txt");
            if (string.IsNullOrEmpty(path)) return;
            SetLastExportPath(format, path);
            ImportFromPath(path);
        }

        /// <summary>
        /// Imports voiceover info or proofreading corrections into the database. It's one undo step.
        /// </summary>
        private void ImportFromPath(string path)
        {
            if (window == null || database == null || string.IsNullOrEmpty(path)) return;
            var state = databaseState;
            var isVoiceover = state.exportFormat == ExportFormat.VoiceoverScript;
            if (isVoiceover && string.IsNullOrEmpty(state.exportVoiceoverInfoFieldName)) return;
            if (serializedObject != null) serializedObject.ApplyModifiedProperties();
            Undo.RegisterCompleteObjectUndo(database, isVoiceover ? "Import Voiceover Info" : "Import Proofreading");
            var language = Localization.language;
            Localization.language = string.Empty;
            try
            {
                if (isVoiceover)
                {
                    VoiceoverScriptExporter.Import(database, path, state.exportActors, state.exportConversationTitleSeparateColumn,
                        state.exportEntrytagFormat, state.exportEncodingType, state.exportVoiceoverInfoFieldName);
                }
                else
                {
                    ProofreadingExporter.Import(database, path, state.exportEncodingType);
                }
            }
            finally
            {
                Localization.language = language;
                EditorUtility.SetDirty(database);
                window.RefreshAfterDirectDatabaseChange();
                if (DialogueEditorWindow.instance != null) DialogueEditorWindow.instance.Reset();
            }
        }

        #endregion

    }

}

#endif
