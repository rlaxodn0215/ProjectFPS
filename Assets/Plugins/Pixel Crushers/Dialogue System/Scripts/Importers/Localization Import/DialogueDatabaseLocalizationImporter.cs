using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace PixelCrushers.DialogueSystem
{

    /// <summary>
    /// Imports CSV files created by Dialogue Editor's Database section > Localization Export/Import.
    /// Both Dialogue Editors use it, and it can be used at runtime.
    /// </summary>
    public class DialogueDatabaseLocalizationImporter
    {

        [Serializable]
        public class LocalizationLanguages
        {
            public List<string> languages = new List<string>();
            public List<string> extraEntryFields = new List<string>();
            public List<string> extraQuestFields = new List<string>();
            public List<string> extraItemFields = new List<string>();
            public int importMainTextIndex = -1;
            public string outputFolder;
        }

        /// <summary>The outcome of an import.</summary>
        public class ImportResult
        {
            /// <summary>Problems logged to the Console, such as missing files, assets, or invalid lines.</summary>
            public int warnings = 0;

            /// <summary>True if the progress callback canceled the import before all languages were imported.</summary>
            public bool canceled = false;
        }

        // Columns before the extra fields in each file. Each extra field adds an original and a
        // translated column, in the order of the non-blank names in the extra field lists.
        private const int DialogueExtraFieldsStartColumn = 8;
        private const int QuestExtraFieldsStartColumn = 11;
        private const int ItemExtraFieldsStartColumn = 5;

        private LocalizationLanguages localizationLanguages = new LocalizationLanguages();
        private string localizationKeyField = "Articy Id";
        private Dictionary<string, int> conversationIDCache = new Dictionary<string, int>();
        private Conversation lastCachedConversation = null;
        private ImportResult result = new ImportResult();

        /// <summary>
        /// Imports localization CSV files from specified folder.
        /// </summary>
        /// <param name="database">Dialogue database to put localizations into.</param>
        /// <param name="folderName">Path to CSV files.</param>
        /// <param name="localizationLanguages">Info about languages to import.</param>
        /// <param name="exportLocalizationConversationTitle">If true, first column is conversation Title instead of ID.</param>
        /// <param name="exportLocalizationCreateNewFields">If true, create new field if dialogue entry doesn't have field.</param>
        /// <param name="localizationKeyField">If not blank, use this key field (e.g., "Articy Id") instead of entry ID etc.</param>
        public ImportResult ImportLocalizationFilesFromFolder(
            DialogueDatabase database,
            string folderName,
            LocalizationLanguages localizationLanguages,
            bool exportLocalizationConversationTitle = false,
            bool exportLocalizationCreateNewFields = false,
            string localizationKeyField = null)
        {
            return ImportLocalizationFilesFromFolder(database, folderName, localizationLanguages, exportLocalizationConversationTitle,
                exportLocalizationCreateNewFields, localizationKeyField, null);
        }

        /// <summary>
        /// Imports localization CSV files from specified folder, reporting progress.
        /// </summary>
        /// <param name="progress">Optional. Called before each language with the language and
        /// the fraction done (0-1). Return true to cancel the import.</param>
        public ImportResult ImportLocalizationFilesFromFolder(
            DialogueDatabase database,
            string folderName,
            LocalizationLanguages localizationLanguages,
            bool exportLocalizationConversationTitle,
            bool exportLocalizationCreateNewFields,
            string localizationKeyField,
            Func<string, float, bool> progress)
        {
            result = new ImportResult();
            if (database == null || localizationLanguages == null) return result;
            this.localizationLanguages = localizationLanguages;
            this.localizationKeyField = localizationKeyField;
            localizationLanguages.outputFolder = folderName;
            conversationIDCache.Clear();
            lastCachedConversation = null;
            var numLanguages = localizationLanguages.languages.Count;
            for (int i = 0; i < numLanguages; i++)
            {
                var language = localizationLanguages.languages[i];
                if (string.IsNullOrEmpty(language)) continue;
                if (progress != null && progress(language, (float)i / numLanguages))
                {
                    result.canceled = true;
                    return result;
                }
                var alsoImportMainText = localizationLanguages.importMainTextIndex == i;
                ImportActors(database, language, alsoImportMainText);
                ImportDialogue(database, language, alsoImportMainText, exportLocalizationConversationTitle, exportLocalizationCreateNewFields);
                ImportQuests(database, language, alsoImportMainText, exportLocalizationCreateNewFields);
                ImportItems(database, language, alsoImportMainText, exportLocalizationCreateNewFields);
            }
            return result;
        }

        private void Warn(string message)
        {
            result.warnings++;
            Debug.LogWarning(message);
        }

        /// <summary>The non-blank names in an extra field list, in order; these are the ones that have columns.</summary>
        private static List<string> GetExtraFields(List<string> fields)
        {
            var list = new List<string>();
            if (fields != null) fields.ForEach(field => { if (!string.IsNullOrEmpty(field)) list.Add(field); });
            return list;
        }

        /// <summary>A column's value, or an empty string if the line is too short to have it.</summary>
        private static string Column(List<string> columns, int index)
        {
            return (0 <= index && index < columns.Count) ? columns[index] : string.Empty;
        }

        /// <summary>Reads a file's data lines (after the language and heading lines), or null if the file doesn't exist.</summary>
        private List<string> ReadDataLines(string filename, bool isRequired)
        {
            if (!File.Exists(filename))
            {
                if (isRequired) Warn("Dialogue System: Localization file " + filename + " doesn't exist. Skipping it.");
                return null;
            }
            var lines = ReadCSV(filename);
            CombineMultilineCSVSourceLines(lines);
            return lines;
        }

        private void ImportActors(DialogueDatabase database, string language, bool alsoImportMainText)
        {
            var filename = localizationLanguages.outputFolder + "/Actors_" + language + ".csv";
            var lines = ReadDataLines(filename, true);
            if (lines == null) return;
            for (int j = 2; j < lines.Count; j++)
            {
                var columns = GetCSVColumnsFromLine(lines[j]);
                if (columns.Count < 3)
                {
                    Warn(filename + ":" + (j + 1) + " Invalid line: " + lines[j]);
                    continue;
                }
                var actorName = columns[0].Trim();
                var actorDisplayName = columns[1].Trim();
                var translatedName = columns[2].Trim();
                var actor = database.GetActor(actorName);
                if (actor == null)
                {
                    Warn(filename + ": No actor in database is named '" + actorName + "'.");
                    continue;
                }
                Field.SetValue(actor.fields, "Display Name " + language, translatedName, FieldType.Localization);
                if (alsoImportMainText && !string.IsNullOrEmpty(actorDisplayName))
                {
                    Field.SetValue(actor.fields, "Display Name", actorDisplayName);
                }
            }
        }

        private void ImportDialogue(DialogueDatabase database, string language, bool alsoImportMainText,
            bool useConversationTitle, bool createNewFields)
        {
            var filename = localizationLanguages.outputFolder + "/Dialogue_" + language + ".csv";
            var lines = ReadDataLines(filename, true);
            if (lines == null) return;
            var useKeyField = !string.IsNullOrEmpty(localizationKeyField);
            var extraFields = GetExtraFields(localizationLanguages.extraEntryFields);
            for (int j = 2; j < lines.Count; j++)
            {
                var columns = GetCSVColumnsFromLine(lines[j]);
                if (columns.Count < (useKeyField ? 8 : 7))
                {
                    Warn(filename + ":" + (j + 1) + " Invalid line: " + lines[j]);
                    continue;
                }

                // Peel key field value off front if exporting key fields:
                string keyFieldValue = null;
                if (useKeyField)
                {
                    keyFieldValue = columns[0];
                    columns.RemoveAt(0);
                }

                // Get conversation ID:
                int conversationID = 0;
                if (useConversationTitle)
                {
                    var conversationTitle = columns[0];
                    if (!conversationIDCache.ContainsKey(conversationTitle))
                    {
                        var conversation = database.GetConversation(conversationTitle);
                        if (conversation == null)
                        {
                            Warn(filename + ":" + (j + 1) + " Database doesn't contain conversation '" + conversationTitle + "'.");
                            continue;
                        }
                        conversationIDCache[conversationTitle] = conversation.id;
                    }
                    conversationID = conversationIDCache[conversationTitle];
                }
                else
                {
                    conversationID = Tools.StringToInt(columns[0]);
                }

                var entryID = Tools.StringToInt(columns[1]);
                // columns[2] is Actor. Ignore it.
                DialogueEntry entry = null;
                if (useKeyField)
                {
                    // Find entry by key field:
                    if (lastCachedConversation == null || lastCachedConversation.id != conversationID)
                    {
                        lastCachedConversation = database.GetConversation(conversationID);
                    }
                    if (lastCachedConversation == null)
                    {
                        Warn(filename + ":" + (j + 1) + " Database doesn't contain conversation " + conversationID + ".");
                        continue;
                    }
                    entry = lastCachedConversation.dialogueEntries.Find(x => Field.LookupValue(x.fields, localizationKeyField) == keyFieldValue);
                    if (entry == null)
                    {
                        Warn(filename + ":" + (j + 1) + " Conversation " + conversationID + " doesn't contain an entry whose " + localizationKeyField + " is '" + keyFieldValue + "'.");
                        continue;
                    }
                }
                else
                {
                    // Find entry by ID:
                    entry = database.GetDialogueEntry(conversationID, entryID);
                    if (entry == null)
                    {
                        Warn(filename + ":" + (j + 1) + " Database doesn't contain conversation " + conversationID + " dialogue entry " + entryID);
                        continue;
                    }
                }

                Field.SetValue(entry.fields, language, Column(columns, 4), FieldType.Localization);
                Field.SetValue(entry.fields, "Menu Text " + language, Column(columns, 6), FieldType.Localization);
                if (alsoImportMainText)
                {
                    entry.DialogueText = Column(columns, 3);
                    entry.MenuText = Column(columns, 5);
                }

                // Extra entry fields:
                for (int k = 0; k < extraFields.Count; k++)
                {
                    var field = extraFields[k];
                    var originalColumn = DialogueExtraFieldsStartColumn + 2 * k;
                    var original = Column(columns, originalColumn);
                    if (!createNewFields && !Field.FieldExists(entry.fields, field) && string.IsNullOrEmpty(original)) continue;
                    Field.SetValue(entry.fields, field + " " + language, Column(columns, originalColumn + 1), FieldType.Localization);
                    if (alsoImportMainText) Field.SetValue(entry.fields, field, original);
                }
            }
        }

        private void ImportQuests(DialogueDatabase database, string language, bool alsoImportMainText, bool createNewFields)
        {
            var filename = localizationLanguages.outputFolder + "/Quests_" + language + ".csv";
            var lines = ReadDataLines(filename, false);
            if (lines == null) return;
            var extraFields = GetExtraFields(localizationLanguages.extraQuestFields);
            var questEntriesStartColumn = QuestExtraFieldsStartColumn + 2 * extraFields.Count;
            for (int j = 2; j < lines.Count; j++)
            {
                var columns = GetCSVColumnsFromLine(lines[j]);
                if (columns.Count < 11)
                {
                    Warn(filename + ":" + (j + 1) + " Invalid line: " + lines[j]);
                    continue;
                }
                var quest = database.GetItem(columns[0]);
                if (quest == null) continue; // Skip if quest is not present.

                var displayName = columns[1];
                var translatedDisplayName = columns[2];
                if (!string.IsNullOrEmpty(translatedDisplayName))
                {
                    if (!quest.FieldExists("Display Name")) Field.SetValue(quest.fields, "Display Name", displayName);
                    Field.SetValue(quest.fields, "Display Name " + language, translatedDisplayName, FieldType.Localization);
                }
                var group = columns[3];
                var translatedGroup = columns[4];
                var needToAddGroup = !quest.FieldExists("Group") && (!string.IsNullOrEmpty(group) || !string.IsNullOrEmpty(translatedGroup));
                if (quest.FieldExists("Group") && string.IsNullOrEmpty(quest.LookupValue("Group")) && !string.IsNullOrEmpty(group)) needToAddGroup = true;
                if (needToAddGroup) Field.SetValue(quest.fields, "Group", group);
                if (quest.FieldExists("Group")) Field.SetValue(quest.fields, "Group " + language, translatedGroup, FieldType.Localization);
                Field.SetValue(quest.fields, "Description " + language, columns[6], FieldType.Localization);
                Field.SetValue(quest.fields, "Success Description " + language, columns[8], FieldType.Localization);
                Field.SetValue(quest.fields, "Failure Description " + language, columns[10], FieldType.Localization);

                // Extra quest fields:
                for (int k = 0; k < extraFields.Count; k++)
                {
                    var field = extraFields[k];
                    var originalColumn = QuestExtraFieldsStartColumn + 2 * k;
                    var original = Column(columns, originalColumn);
                    if (!createNewFields && !Field.FieldExists(quest.fields, field) && string.IsNullOrEmpty(original)) continue;
                    Field.SetValue(quest.fields, field + " " + language, Column(columns, originalColumn + 1), FieldType.Localization);
                    if (alsoImportMainText) Field.SetValue(quest.fields, field, original);
                }

                // Quest entry fields, after the extra fields:
                var entryCount = quest.LookupInt("Entry Count");
                for (int k = 0; k < entryCount; k++)
                {
                    Field.SetValue(quest.fields, "Entry " + (k + 1) + " " + language, Column(columns, questEntriesStartColumn + 2 * k + 1), FieldType.Localization);
                }

                // Check if we need to also import main text:
                if (alsoImportMainText)
                {
                    if (quest.FieldExists("Display Name")) Field.SetValue(quest.fields, "Display Name", displayName);
                    if (quest.FieldExists("Group")) Field.SetValue(quest.fields, "Group", group, FieldType.Text);
                    Field.SetValue(quest.fields, "Description", columns[5], FieldType.Text);
                    Field.SetValue(quest.fields, "Success Description", columns[7], FieldType.Text);
                    Field.SetValue(quest.fields, "Failure Description", columns[9], FieldType.Text);
                    for (int k = 0; k < entryCount; k++)
                    {
                        Field.SetValue(quest.fields, "Entry " + (k + 1), Column(columns, questEntriesStartColumn + 2 * k), FieldType.Text);
                    }
                }
            }
        }

        private void ImportItems(DialogueDatabase database, string language, bool alsoImportMainText, bool createNewFields)
        {
            var filename = localizationLanguages.outputFolder + "/Items_" + language + ".csv";
            var lines = ReadDataLines(filename, false);
            if (lines == null) return;
            var extraFields = GetExtraFields(localizationLanguages.extraItemFields);
            for (int j = 2; j < lines.Count; j++)
            {
                var columns = GetCSVColumnsFromLine(lines[j]);
                if (columns.Count < 5)
                {
                    Warn(filename + ":" + (j + 1) + " Invalid line: " + lines[j]);
                    continue;
                }
                var item = database.GetItem(columns[0]);
                if (item == null) continue; // Skip if item is not present.

                var displayName = columns[1];
                var translatedDisplayName = columns[2];
                if (!string.IsNullOrEmpty(translatedDisplayName))
                {
                    if (!item.FieldExists("Display Name")) Field.SetValue(item.fields, "Display Name", displayName);
                    Field.SetValue(item.fields, "Display Name " + language, translatedDisplayName, FieldType.Localization);
                }
                Field.SetValue(item.fields, "Description " + language, columns[4], FieldType.Localization);

                // Extra item fields:
                for (int k = 0; k < extraFields.Count; k++)
                {
                    var field = extraFields[k];
                    var originalColumn = ItemExtraFieldsStartColumn + 2 * k;
                    var original = Column(columns, originalColumn);
                    if (!createNewFields && !Field.FieldExists(item.fields, field) && string.IsNullOrEmpty(original)) continue;
                    Field.SetValue(item.fields, field + " " + language, Column(columns, originalColumn + 1), FieldType.Localization);
                    if (alsoImportMainText) Field.SetValue(item.fields, field, original);
                }
            }
        }

        private List<string> GetCSVColumnsFromLine(string line)
        {
            Regex csvSplit = new Regex("(?:^|,)(\"(?:[^\"]+|\"\")*\"|[^,]*)");
            List<string> values = new List<string>();
            foreach (Match match in csvSplit.Matches(line))
            {
                values.Add(UnwrapCSVValue(match.Value.TrimStart(',')));
            }
            return values;
        }

        private List<string> ReadCSV(string filename)
        {
            var lines = new List<string>();
            using (var sr = new StreamReader(filename, new UTF8Encoding(true)))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    lines.Add(line.TrimEnd());
                }
            }
            return lines;
        }

        /// <summary>
        /// Combines lines that are actually a multiline CSV row. This also helps prevent the
        /// CSV-splitting regex from hanging due to catastrophic backtracking on unterminated quotes.
        /// </summary>
        private void CombineMultilineCSVSourceLines(List<string> sourceLines)
        {
            int lineNum = 0;
            int safeguard = 0;
            int MaxIterations = 999999;
            while ((lineNum < sourceLines.Count) && (safeguard < MaxIterations))
            {
                safeguard++;
                string line = sourceLines[lineNum];
                if (line == null)
                {
                    sourceLines.RemoveAt(lineNum);
                }
                else
                {
                    bool terminated = true;
                    char previousChar = (char)0;
                    for (int i = 0; i < line.Length; i++)
                    {
                        char currentChar = line[i];
                        bool isQuote = (currentChar == '"') && (previousChar != '\\');
                        if (isQuote) terminated = !terminated;
                        previousChar = currentChar;
                    }
                    if (terminated || (lineNum + 1) >= sourceLines.Count)
                    {
                        if (!terminated) sourceLines[lineNum] = line + '"';
                        lineNum++;
                    }
                    else
                    {
                        sourceLines[lineNum] = line + "\\n" + sourceLines[lineNum + 1];
                        sourceLines.RemoveAt(lineNum + 1);
                    }
                }
            }
        }

        string UnwrapCSVValue(string s)
        {
            string s2 = s.Replace("\\n", "\n").Replace("\\r", "\r");
            if (s2.StartsWith("\"") && s2.EndsWith("\""))
            {
                s2 = s2.Substring(1, s2.Length - 2).Replace("\"\"", "\"");
            }
            return s2;
        }

    }

}
