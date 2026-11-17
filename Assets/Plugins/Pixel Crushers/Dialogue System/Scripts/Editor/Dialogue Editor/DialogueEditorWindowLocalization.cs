// Copyright (c) Pixel Crushers. All rights reserved.

using UnityEngine;
using UnityEngine.Networking;
using UnityEditor;
using UnityEditorInternal;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.IO;
using System.Text;

namespace PixelCrushers.DialogueSystem.DialogueEditor
{

    /// <summary>
    /// This part of the Dialogue Editor window handles the Database tab's Localization foldout.
    /// Credits: This section is based on code contributed by FourAttic / Devolver Digital. Thank you!
    /// </summary>
    public partial class DialogueEditorWindow
    {

        #region Database Tab > Localization Foldout Variables

        [SerializeField]
        private DialogueDatabaseLocalizationImporter.LocalizationLanguages localizationLanguages = new DialogueDatabaseLocalizationImporter.LocalizationLanguages();

        private ReorderableList exportLanguageList = null;
        private ReorderableList exportLanguageExtraEntryFieldsList = null;
        private ReorderableList exportLanguageExtraQuestFieldsList = null;
        private ReorderableList exportLanguageExtraItemFieldsList = null;
        private bool doesDatabaseHaveItems = false;

        [SerializeField]
        private bool exportLocalizationConversationTitle = false;

        [SerializeField]
        private bool exportLocalizationKeyField = false;

        [SerializeField]
        private bool exportAssignFieldValues = false;

        [SerializeField]
        private bool exportLocalizationCreateNewFields = false;

        [SerializeField]
        private bool exportIncludeEntriesWithoutText = true; // Matches the files exported before the option was fixed.

        [SerializeField]
        private string localizationKeyField = "Articy Id";

        [SerializeField]
        private DialogueEntrySortMethod localizationEntrySortMethod = DialogueEntrySortMethod.No;

        private GUIContent exportLocalizationConversationTitleLabel = new GUIContent("Export Conversation Title Instead Of ID", "Export conversation title instead of ID. Titles should be unique.");
        private GUIContent exportLocalizationKeyFieldLabel = new GUIContent("Use Key Field", "Tie each dialogue entry row to a key field (e.g., 'Articy Id' or 'Celtx ID') instead of conversation & entry IDs.");
        private GUIContent exportAssignFieldValuesLabel = new GUIContent("Assign Values", "If key field is blank for dialogue entry, assign a unique value to it.");
        private GUIContent exportLocalizationCreateNewFieldsLabel = new GUIContent("Create New Fields", "If Extra Dialogue Entry field doesn't exist in an entry or if Extra Quest Field doesn't exist for a quest, create field when importing.");
        private GUIContent exportIncludeEntriesWithoutTextLabel = new GUIContent("Include Blank Entries", "Include entries without Dialogue Text or Menu Text in dialogue export.");
        private GUIContent exportLocalizationSortModeLabel = new GUIContent("Sort Entries?", "Export dialogue entries in a sorted order.");
        private GUIContent exportExtraEntryFieldsLabel = new GUIContent("Extra Dialogue Entry Fields", "(Optional) Extra dialogue entry fields to localize.");
        private GUIContent exportExtraQuestFieldsLabel = new GUIContent("Extra Quest Fields", "(Optional) Extra quest fields to localize.");
        private GUIContent exportExtraItemFieldsLabel = new GUIContent("Extra Item Fields", "(Optional) Extra item fields to localize.");

        private Rect localizationButtonPosition = new Rect();

        #endregion

        #region Draw Localization Foldout Section

        private void ResetLocalizationFoldout()
        {
            doesDatabaseHaveItems = false;
            if (database != null)
            {
                foreach (var item in database.items)
                {
                    if (item.IsItem)
                    {
                        doesDatabaseHaveItems = true;
                        break;
                    }
                }
            }
        }

        private void DrawLocalizationSection()
        {
            EditorWindowTools.StartIndentedSection();

            if (exportLanguageList == null)
            {
                exportLanguageList = new ReorderableList(localizationLanguages.languages, typeof(string), true, true, true, true);
                exportLanguageList.drawHeaderCallback += OnDrawExportLanguageListHeader;
                exportLanguageList.drawElementCallback = OnDrawExportLanguageListElement;
                exportLanguageList.onAddCallback += OnAddExportLanguageListElement;
            }
            exportLanguageList.DoLayoutList();

            if (exportLanguageExtraEntryFieldsList == null)
            {
                exportLanguageExtraEntryFieldsList = new ReorderableList(localizationLanguages.extraEntryFields, typeof(string), true, true, true, true);
                exportLanguageExtraEntryFieldsList.drawHeaderCallback += OnDrawExportLanguageExtraEntryFieldsListHeader;
                exportLanguageExtraEntryFieldsList.drawElementCallback = OnDrawExportLanguageExtraEntryFieldsListElement;
                exportLanguageExtraEntryFieldsList.onAddCallback += OnAddExportLanguageExtraEntryFieldsListElement;
            }
            exportLanguageExtraEntryFieldsList.DoLayoutList();

            if (exportLanguageExtraQuestFieldsList == null)
            {
                exportLanguageExtraQuestFieldsList = new ReorderableList(localizationLanguages.extraQuestFields, typeof(string), true, true, true, true);
                exportLanguageExtraQuestFieldsList.drawHeaderCallback += OnDrawExportLanguageExtraQuestFieldsListHeader;
                exportLanguageExtraQuestFieldsList.drawElementCallback = OnDrawExportLanguageExtraQuestFieldsListElement;
                exportLanguageExtraQuestFieldsList.onAddCallback += OnAddExportLanguageExtraQuestFieldsListElement;
            }
            exportLanguageExtraQuestFieldsList.DoLayoutList();

            if (doesDatabaseHaveItems)
            {
                if (exportLanguageExtraItemFieldsList == null)
                {
                    exportLanguageExtraItemFieldsList = new ReorderableList(localizationLanguages.extraItemFields, typeof(string), true, true, true, true);
                    exportLanguageExtraItemFieldsList.drawHeaderCallback += OnDrawExportLanguageExtraItemFieldsListHeader;
                    exportLanguageExtraItemFieldsList.drawElementCallback = OnDrawExportLanguageExtraItemFieldsListElement;
                    exportLanguageExtraItemFieldsList.onAddCallback += OnAddExportLanguageExtraItemFieldsListElement;
                }
                exportLanguageExtraItemFieldsList.DoLayoutList();
            }

            exportLocalizationConversationTitle = EditorGUILayout.ToggleLeft(exportLocalizationConversationTitleLabel, exportLocalizationConversationTitle);
            EditorGUILayout.BeginHorizontal();
            exportLocalizationKeyField = EditorGUILayout.ToggleLeft(exportLocalizationKeyFieldLabel, exportLocalizationKeyField, GUILayout.Width(160));
            if (exportLocalizationKeyField)
            {
                localizationKeyField = EditorGUILayout.TextField(GUIContent.none, localizationKeyField, GUILayout.Width(160));
                exportAssignFieldValues = EditorGUILayout.ToggleLeft(exportAssignFieldValuesLabel, exportAssignFieldValues, GUILayout.Width(160));
            }
            EditorGUILayout.EndHorizontal();

            exportLocalizationCreateNewFields = EditorGUILayout.ToggleLeft(exportLocalizationCreateNewFieldsLabel, exportLocalizationCreateNewFields, GUILayout.Width(160));
            exportIncludeEntriesWithoutText = EditorGUILayout.ToggleLeft(exportIncludeEntriesWithoutTextLabel, exportIncludeEntriesWithoutText, GUILayout.Width(160));

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(exportLocalizationSortModeLabel, GUILayout.Width(80));
            localizationEntrySortMethod = (DialogueEntrySortMethod)EditorGUILayout.EnumPopup(GUIContent.none, localizationEntrySortMethod, GUILayout.Width(100));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Find Languages", GUILayout.Width(120)))
            {
                FindLanguagesForLocalizationExportImport();
            }
            EditorGUI.BeginDisabledGroup(exportLocalizationKeyField && string.IsNullOrEmpty(localizationKeyField));
            if (GUILayout.Button("Export...", GUILayout.Width(100)))
            {
                ExportLocalizationFiles();
            }
            if (GUILayout.Button("Import...", GUILayout.Width(100)))
            {
                ImportLocalizationFiles();
            }

            if (EditorGUILayout.DropdownButton(new GUIContent("Send Localization Request", "Request a quote for localization services from one of our partners."), FocusType.Keyboard, GUILayout.Width(180)))
            {
                GenericMenu dropdownMenu = new GenericMenu();
                dropdownMenu.AddItem(new GUIContent("Get Localized by Altagram..."), false, () =>
                {
                    LocalizationByAltagram();
                });
                dropdownMenu.DropDown(localizationButtonPosition);
            }
            if (Event.current.type == EventType.Repaint) localizationButtonPosition = GUILayoutUtility.GetLastRect(); // cache the position so it can be used when the user clicks the dropdown

            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();
            EditorWindowTools.EndIndentedSection();
        }

        private void OnDrawExportLanguageListHeader(Rect rect)
        {
            EditorGUI.LabelField(rect, "Languages (checkbox also updates main text when reimporting language file)");
        }

        private void OnDrawExportLanguageListElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            if (!(0 <= index && index < localizationLanguages.languages.Count)) return;
            var toggleWidth = 18f;
            var langRect = new Rect(rect.x, rect.y, rect.width - toggleWidth - 4, EditorGUIUtility.singleLineHeight);
            var mainRect = new Rect(rect.x + rect.width - toggleWidth, rect.y, toggleWidth, rect.height);
            localizationLanguages.languages[index] = EditorGUI.TextField(langRect, localizationLanguages.languages[index]);
            EditorGUI.BeginChangeCheck();
            var newValue = EditorGUI.Toggle(mainRect, GUIContent.none, localizationLanguages.importMainTextIndex == index);
            if (EditorGUI.EndChangeCheck())
            {
                localizationLanguages.importMainTextIndex = newValue ? index : -1;
            }
        }

        private void OnAddExportLanguageListElement(ReorderableList list)
        {
            localizationLanguages.languages.Add(string.Empty);
        }
        private void OnDrawExportLanguageExtraEntryFieldsListHeader(Rect rect)
        {
            EditorGUI.LabelField(rect, exportExtraEntryFieldsLabel);
        }

        private void OnDrawExportLanguageExtraEntryFieldsListElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            if (!(0 <= index && index < localizationLanguages.extraEntryFields.Count)) return;
            var langRect = new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight);
            localizationLanguages.extraEntryFields[index] = EditorGUI.TextField(langRect, localizationLanguages.extraEntryFields[index]);
        }

        private void OnAddExportLanguageExtraEntryFieldsListElement(ReorderableList list)
        {
            localizationLanguages.extraEntryFields.Add(string.Empty);
        }

        private void OnDrawExportLanguageExtraQuestFieldsListHeader(Rect rect)
        {
            EditorGUI.LabelField(rect, exportExtraQuestFieldsLabel);
        }

        private void OnDrawExportLanguageExtraQuestFieldsListElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            if (!(0 <= index && index < localizationLanguages.extraQuestFields.Count)) return;
            var langRect = new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight);
            localizationLanguages.extraQuestFields[index] = EditorGUI.TextField(langRect, localizationLanguages.extraQuestFields[index]);
        }

        private void OnAddExportLanguageExtraQuestFieldsListElement(ReorderableList list)
        {
            localizationLanguages.extraQuestFields.Add(string.Empty);
        }

        private void OnDrawExportLanguageExtraItemFieldsListHeader(Rect rect)
        {
            EditorGUI.LabelField(rect, exportExtraItemFieldsLabel);
        }

        private void OnDrawExportLanguageExtraItemFieldsListElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            if (!(0 <= index && index < localizationLanguages.extraItemFields.Count)) return;
            var langRect = new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight);
            localizationLanguages.extraItemFields[index] = EditorGUI.TextField(langRect, localizationLanguages.extraItemFields[index]);
        }

        private void OnAddExportLanguageExtraItemFieldsListElement(ReorderableList list)
        {
            localizationLanguages.extraItemFields.Add(string.Empty);
        }

        private void FindLanguagesForLocalizationExportImport()
        {
            if (database == null) return;
            EditorUtility.DisplayProgressBar("Finding Languages", "Scanning database. Please wait...", 0);
            try
            {
                DialogueDatabaseLocalizationExporter.FindLanguages(database, template, localizationLanguages.languages);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        #endregion

        #region Export Section

        private void ExportLocalizationFiles()
        {
            var newOutputFolder = EditorUtility.OpenFolderPanel("Export Localization Files", localizationLanguages.outputFolder, string.Empty);
            if (!string.IsNullOrEmpty(newOutputFolder))
            {
                if (ExportLocalizationFilesToFolder(newOutputFolder))
                {
                    EditorUtility.DisplayDialog("Exported Localization CSV", "Localization files are in " + localizationLanguages.outputFolder + ". Open these files in a spreadsheet application and add translations. Then import them by using the Import... button.", "OK");
                }
            }
        }

        private bool ExportLocalizationFilesToFolder(string folderName)
        {
            localizationLanguages.outputFolder = folderName;
            var keyField = exportLocalizationKeyField ? localizationKeyField : null;
            if (exportLocalizationKeyField && exportAssignFieldValues)
            {
                Undo.RegisterCompleteObjectUndo(database, "Assign Localization Keys");
                if (DialogueDatabaseLocalizationExporter.AssignMissingKeyValues(database, keyField) > 0) SetDatabaseDirty("Assign Localization Keys");
            }
            var options = new DialogueDatabaseLocalizationExporter.Options()
            {
                useConversationTitle = exportLocalizationConversationTitle,
                keyField = keyField,
                includeBlankEntries = exportIncludeEntriesWithoutText,
                sortMethod = localizationEntrySortMethod
            };
            try
            {
                return DialogueDatabaseLocalizationExporter.Export(database, folderName, localizationLanguages, options,
                    (language, progress) => EditorUtility.DisplayCancelableProgressBar("Exporting Localization CSV", "Exporting CSV files for " + language, progress));
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        // The sorting code is in DialogueEntrySorter, shared with the UI Toolkit editor's exports.
        private List<DialogueEntry> DepthFirstSortEntries(List<DialogueEntry> entries)
        {
            return DialogueEntrySorter.DepthFirstSortEntries(entries);
        }

        private List<DialogueEntry> BreadthFirstSortEntries(List<DialogueEntry> entries)
        {
            return DialogueEntrySorter.BreadthFirstSortEntries(entries);
        }

        #endregion

        #region Import Section

        private void ImportLocalizationFiles()
        {
            var newOutputFolder = EditorUtility.OpenFolderPanel("Import Localization Files", localizationLanguages.outputFolder, string.Empty);
            if (!string.IsNullOrEmpty(newOutputFolder))
            {
                ImportLocalizationFilesFromFolder(newOutputFolder);
            }

        }

        public void ImportLocalizationFilesFromFolder(string folderName)
        {
            DialogueDatabaseLocalizationImporter.ImportResult result;
            Undo.RegisterCompleteObjectUndo(database, "Import Localization");
            try
            {
                result = new DialogueDatabaseLocalizationImporter().ImportLocalizationFilesFromFolder(database, folderName, localizationLanguages,
                    exportLocalizationConversationTitle, exportLocalizationCreateNewFields,
                    exportLocalizationKeyField ? localizationKeyField : null,
                    (language, progress) => EditorUtility.DisplayCancelableProgressBar("Importing Localization CSV", "Importing CSV files for " + language, progress));
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                SetDatabaseDirty("Import Localization");
            }
            if (!result.canceled)
            {
                var warnings = (result.warnings > 0) ? string.Format("\n\n{0} problem{1} logged to the Console.", result.warnings, (result.warnings == 1) ? " was" : "s were") : string.Empty;
                EditorUtility.DisplayDialog("Imported Localization CSV", "The CSV files have been imported back into your dialogue database." + warnings, "OK");
            }
            Reset();
        }

        #endregion

        #region Altagram (Alocai)

        private void LocalizationByAltagram()
        {
            // Show an explanation and confirmation dialog:
            if (!EditorUtility.DisplayDialog("Request Quote From Altagram",
                "Pixel Crushers is partnering with localization services to provide additional localization options for your Dialogue System projects.\n\n" +
                "Click Continue to request a quote from game localization platform Altagram (Alocai) to translate your dialogue database content. " +
                "The Dialogue Editor will ask you to select a folder to export your database content, which will then be sent to Altagram so they can prepare a quote.",
                "Continue", "Cancel"))
            {
                return;
            }

            // make sure the options are selected correctly
            if (!exportLocalizationKeyField)
            {
                exportLocalizationKeyField = true;
                localizationKeyField = "Guid";
                exportAssignFieldValues = true;
            }

            // export the localization data to files in a local folder
            var newOutputFolder = EditorUtility.OpenFolderPanel("Export Localization Files", localizationLanguages.outputFolder, string.Empty);
            if (!string.IsNullOrEmpty(newOutputFolder))
            {
                if (ExportLocalizationFilesToFolder(newOutputFolder))
                {
                    // send the files to Altagram
                    SendRequestToAltagram();
                }
            }
        }

        private void SendRequestToAltagram()
        {
            string server = "https://quote-requester-api.alocai.com";
            string apiKey = "539568b8-3589-4ccc-ace5-4439e315bd4f";

            try
            {
                List<IMultipartFormSection> data = new List<IMultipartFormSection>();

                int numLanguages = localizationLanguages.languages.Count;
                for (int i = 0; i < numLanguages; i++)
                {
                    string language = localizationLanguages.languages[i];

                    // Dialogue_LN.csv file:
                    string dialogueFilename = "Dialogue_" + language + ".csv";
                    string dialogueFilePath = localizationLanguages.outputFolder + "/" + dialogueFilename;
                    byte[] dialogueFileContents = File.ReadAllBytes(dialogueFilePath);
                    data.Add(new MultipartFormFileSection("files", dialogueFileContents, dialogueFilename, "text/csv"));

                    // Quests_LN.csv file:
                    string questsFilename = "Quests_" + language + ".csv";
                    string questsFilePath = localizationLanguages.outputFolder + "/" + questsFilename;
                    byte[] questsFileContents = File.ReadAllBytes(questsFilePath);
                    data.Add(new MultipartFormFileSection("files", questsFileContents, questsFilename, "text/csv"));
                }

                Debug.Log("Sending request to " + server);

                UnityWebRequest www = UnityWebRequest.Post(server + "/api/v1/files", data);
                www.SetRequestHeader("X-Api-Key", apiKey);
                www.SendWebRequest();

                while (!www.isDone)
                {
                    if (EditorUtility.DisplayCancelableProgressBar("Uploading Localization files", "Uploading localization files to Altagram", www.uploadProgress))
                    {
                        www.Abort();
                        return;
                    }
                }

#if UNITY_2020_1_OR_NEWER
                Debug.LogFormat(this, "{0} {1}", www.responseCode, www.result);
                if (www.result == UnityWebRequest.Result.Success)
#else
                long responseCodeSuccess = 201;
                Debug.LogFormat(this, "Web response code: {0}", www.responseCode);
                if (www.responseCode == responseCodeSuccess)
#endif
                {
                    // success
                    string text = www.downloadHandler.text;
                    Debug.Log(text);

                    var response = JsonUtility.FromJson<AltagramResponse>(text);
                    string responseUrl = response.quote_requester_client_url;
                    Application.OpenURL(responseUrl);

                    // Show reminder to add the languages to the list on the web form
                    //EditorUtility.DisplayDialog("Localization by Altagram", "Be sure to select all the desired languages on the web form.", "OK");

                    Debug.Log("Request sent to Altagram quote request server.");
                }
                else
                {
                    // error
                    Debug.LogError("Error connecting to Altagram's request server: " + www.error);
                    Debug.Log("Error details text: " + www.downloadHandler.text);

                    EditorUtility.DisplayDialog("Localization by Altagram",
                        "There was an error connecting to Altagram's request server:\n\n" + www.error +
                        "\n\nPlease contact Pixel Crushers for support, and provide the error message.", "OK");
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        public class AltagramResponse
        {
            public string quote_requester_client_url;
        }

        #endregion

    }

}