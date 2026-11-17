// Copyright (c) Pixel Crushers. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace PixelCrushers.DialogueSystem.DialogueEditor
{

    /// <summary>
    /// Writes the localization CSV files of the Dialogue Editors' Database > Localization
    /// Export/Import section: Actors_LN, Dialogue_LN, Quests_LN and Items_LN for each language.
    /// DialogueDatabaseLocalizationImporter reads them back. Exporting doesn't change the database.
    /// </summary>
    public static class DialogueDatabaseLocalizationExporter
    {

        public class Options
        {
            /// <summary>Identify conversations by title instead of ID in the Dialogue file.</summary>
            public bool useConversationTitle = false;

            /// <summary>If not blank, the first Dialogue column is this field, which identifies each entry on import.</summary>
            public string keyField = null;

            /// <summary>Include entries that have no Dialogue Text or Menu Text.</summary>
            public bool includeBlankEntries = false;

            /// <summary>Order of each conversation's entries.</summary>
            public DialogueEntrySortMethod sortMethod = DialogueEntrySortMethod.No;
        }

        /// <summary>Field title prefixes of localized fields that aren't a language's dialogue text.</summary>
        private static readonly string[] LanguageFieldPrefixes = new string[] {
            "Dialogue Text ", "Menu Text ", "Sequence ", "Response Menu Sequence ",
            "Description ", "Success Description ", "Failure Description ", "Title "
        };

        /// <summary>
        /// Writes the localization files for each language to a folder. Returns false if the
        /// progress callback canceled the export.
        /// </summary>
        /// <param name="progress">Optional. Called before each language with the language and
        /// the fraction done (0-1). Return true to cancel.</param>
        public static bool Export(DialogueDatabase database, string folder, DialogueDatabaseLocalizationImporter.LocalizationLanguages languages,
            Options options, Func<string, float, bool> progress = null)
        {
            if (database == null || languages == null) return false;
            if (options == null) options = new Options();
            var actorNames = new Dictionary<int, string>();
            var numLanguages = languages.languages.Count;
            for (int i = 0; i < numLanguages; i++)
            {
                var language = languages.languages[i];
                if (string.IsNullOrEmpty(language)) continue;
                if (progress != null && progress(language, (float)i / numLanguages)) return false;
                ExportActors(database, folder, language);
                ExportDialogue(database, folder, language, languages.extraEntryFields, options, actorNames);
                ExportQuests(database, folder, language, languages.extraQuestFields);
                ExportItems(database, folder, language, languages.extraItemFields);
            }
            return true;
        }

        /// <summary>
        /// Gives each dialogue entry without a value in the key field a new unique value, adding
        /// the field if needed. Returns the number of entries changed. Record undo before calling it.
        /// </summary>
        public static int AssignMissingKeyValues(DialogueDatabase database, string keyField)
        {
            if (database == null || string.IsNullOrEmpty(keyField)) return 0;
            int count = 0;
            foreach (var conversation in database.conversations)
            {
                foreach (var entry in conversation.dialogueEntries)
                {
                    var field = Field.Lookup(entry.fields, keyField);
                    if (field == null)
                    {
                        entry.fields.Add(new Field(keyField, GetNewKeyFieldValue(), FieldType.Text));
                        count++;
                    }
                    else if (string.IsNullOrEmpty(field.value))
                    {
                        field.value = GetNewKeyFieldValue();
                        count++;
                    }
                }
            }
            return count;
        }

        /// <summary>Returns the number of exported dialogue entries that have no value in the key field.</summary>
        public static int CountMissingKeyValues(DialogueDatabase database, string keyField, bool includeBlankEntries)
        {
            if (database == null || string.IsNullOrEmpty(keyField)) return 0;
            int count = 0;
            foreach (var conversation in database.conversations)
            {
                foreach (var entry in conversation.dialogueEntries)
                {
                    if (!includeBlankEntries && IsBlankEntry(entry)) continue;
                    if (string.IsNullOrEmpty(Field.LookupValue(entry.fields, keyField))) count++;
                }
            }
            return count;
        }

        /// <summary>
        /// Adds languages that dialogue entries (and the template's dialogue entry fields) have
        /// localized Dialogue Text fields for, such as a field titled "fr".
        /// </summary>
        public static void FindLanguages(DialogueDatabase database, Template template, List<string> languages)
        {
            if (database == null || languages == null) return;
            database.conversations.ForEach(conversation => conversation.dialogueEntries.ForEach(entry => FindLanguagesInFields(entry.fields, languages)));
            if (template != null) FindLanguagesInFields(template.dialogueEntryFields, languages);
        }

        private static void FindLanguagesInFields(List<Field> fields, List<string> languages)
        {
            foreach (var field in fields)
            {
                if (field.type != FieldType.Localization || string.IsNullOrEmpty(field.title)) continue;
                if (field.title == "Dialogue Text") continue;
                // A localized Dialogue Text field's title is the language. Skip other
                // localized fields, such as "Menu Text fr".
                var isOtherField = false;
                foreach (var prefix in LanguageFieldPrefixes)
                {
                    if (field.title.StartsWith(prefix)) isOtherField = true;
                }
                if (!isOtherField && !languages.Contains(field.title)) languages.Add(field.title);
            }
        }

        private static bool IsBlankEntry(DialogueEntry entry)
        {
            return string.IsNullOrEmpty(entry.DialogueText) && string.IsNullOrEmpty(entry.MenuText);
        }

        private static List<string> GetExtraFields(List<string> fields)
        {
            var list = new List<string>();
            if (fields != null) fields.ForEach(field => { if (!string.IsNullOrEmpty(field)) list.Add(field); });
            return list;
        }

        private static StreamWriter CreateFile(string folder, string prefix, string language)
        {
            return new StreamWriter(folder + "/" + prefix + "_" + language + ".csv", false, new UTF8Encoding(true));
        }

        private static void ExportActors(DialogueDatabase database, string folder, string language)
        {
            using (var file = CreateFile(folder, "Actors", language))
            {
                file.WriteLine(language);
                file.WriteLine("Name, Display Name, Translated Display Name");
                foreach (var actor in database.actors)
                {
                    file.WriteLine(string.Format("{0}, {1}, {2}", WrapCSVValue(actor.Name),
                        WrapCSVValue(actor.LookupValue("Display Name")), WrapCSVValue(actor.LookupValue("Display Name " + language))));
                }
            }
        }

        private static void ExportDialogue(DialogueDatabase database, string folder, string language, List<string> extraEntryFields,
            Options options, Dictionary<int, string> actorNames)
        {
            var useKeyField = !string.IsNullOrEmpty(options.keyField);
            var extraFields = GetExtraFields(extraEntryFields);
            using (var file = CreateFile(folder, "Dialogue", language))
            {
                file.WriteLine(language);
                var heading = string.Format("{0},{1},{2},{3},{4},{5},{6},{7}",
                    options.useConversationTitle ? "Conversation" : "Conversation ID",
                    "Entry ID",
                    "Actor",
                    "Original Text",
                    "Translated Text [" + language + "]",
                    "Original Menu",
                    "Translated Menu [" + language + "]",
                    "Description");
                foreach (var field in extraFields)
                {
                    heading += "," + field + "," + field + " [" + language + "]";
                }
                if (useKeyField) heading = options.keyField + "," + heading;
                file.WriteLine(heading);

                var orderedFields = new string[] { "Dialogue Text", language, "Menu Text", "Menu Text " + language, "Description" };
                foreach (var conversation in database.conversations)
                {
                    var entries = SortEntries(conversation.dialogueEntries, options.sortMethod);
                    if (!options.includeBlankEntries) entries.RemoveAll(IsBlankEntry);
                    foreach (var entry in entries)
                    {
                        var sb = new StringBuilder();
                        if (useKeyField) sb.AppendFormat("{0},", WrapCSVValue(Field.LookupValue(entry.fields, options.keyField)));
                        sb.AppendFormat("{0},{1},{2}",
                            options.useConversationTitle ? WrapCSVValue(conversation.Title) : conversation.id.ToString(),
                            entry.id,
                            WrapCSVValue(LookupActorName(database, entry.ActorID, actorNames)));
                        foreach (var title in orderedFields)
                        {
                            var field = entry.fields.Find(x => x.title == title);
                            sb.AppendFormat(",{0}", WrapCSVValue((field != null) ? field.value : string.Empty));
                        }
                        foreach (var field in extraFields)
                        {
                            sb.AppendFormat(",{0},{1}", WrapCSVValue(Field.LookupValue(entry.fields, field)),
                                WrapCSVValue(Field.LookupValue(entry.fields, field + " " + language)));
                        }
                        file.WriteLine(sb.ToString());
                    }
                }
            }
        }

        private static void ExportQuests(DialogueDatabase database, string folder, string language, List<string> extraQuestFields)
        {
            var quests = database.items.FindAll(item => !item.IsItem);
            if (quests.Count == 0) return;
            var extraFields = GetExtraFields(extraQuestFields);
            int maxEntryCount = 0;
            quests.ForEach(quest => maxEntryCount = Mathf.Max(maxEntryCount, quest.LookupInt("Entry Count")));
            using (var file = CreateFile(folder, "Quests", language))
            {
                file.WriteLine(language);
                var sb = new StringBuilder();
                sb.AppendFormat("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10}",
                    "Name",
                    "Display Name",
                    "Translated Display Name [" + language + "]",
                    "Group",
                    "Translated Group [" + language + "]",
                    "Description",
                    "Translated Description [" + language + "]",
                    "Success Description",
                    "Translated Success Description [" + language + "]",
                    "Failure Description",
                    "Translated Failure Description [" + language + "]");
                foreach (var field in extraFields)
                {
                    sb.AppendFormat(",{0},{1} [{2}]", field, field, language);
                }
                for (int j = 0; j < maxEntryCount; j++)
                {
                    sb.AppendFormat(",{0},{1}", "Entry " + (j + 1), "Translated Entry [" + (j + 1) + "]");
                }
                file.WriteLine(sb.ToString());

                foreach (var quest in quests)
                {
                    sb = new StringBuilder();
                    sb.AppendFormat("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10}",
                        WrapCSVValue(quest.Name),
                        WrapCSVValue(quest.LookupValue("Display Name")),
                        WrapCSVValue(quest.LookupValue("Display Name " + language)),
                        WrapCSVValue(quest.LookupValue("Group")),
                        WrapCSVValue(quest.LookupValue("Group " + language)),
                        WrapCSVValue(quest.LookupValue("Description")),
                        WrapCSVValue(quest.LookupValue("Description " + language)),
                        WrapCSVValue(quest.LookupValue("Success Description")),
                        WrapCSVValue(quest.LookupValue("Success Description " + language)),
                        WrapCSVValue(quest.LookupValue("Failure Description")),
                        WrapCSVValue(quest.LookupValue("Failure Description " + language)));
                    foreach (var field in extraFields)
                    {
                        sb.AppendFormat(",{0},{1}", WrapCSVValue(quest.LookupValue(field)), WrapCSVValue(quest.LookupValue(field + " " + language)));
                    }
                    var entryCount = quest.LookupInt("Entry Count");
                    for (int j = 0; j < maxEntryCount; j++)
                    {
                        if (j < entryCount)
                        {
                            sb.AppendFormat(",{0},{1}",
                                WrapCSVValue(quest.LookupValue("Entry " + (j + 1))),
                                WrapCSVValue(quest.LookupValue("Entry " + (j + 1) + " " + language)));
                        }
                        else
                        {
                            sb.Append(",,");
                        }
                    }
                    file.WriteLine(sb.ToString());
                }
            }
        }

        private static void ExportItems(DialogueDatabase database, string folder, string language, List<string> extraItemFields)
        {
            var items = database.items.FindAll(item => item.IsItem);
            if (items.Count == 0) return;
            var extraFields = GetExtraFields(extraItemFields);
            using (var file = CreateFile(folder, "Items", language))
            {
                file.WriteLine(language);
                var sb = new StringBuilder();
                sb.AppendFormat("{0},{1},{2},{3},{4}",
                    "Name",
                    "Display Name",
                    "Translated Display Name [" + language + "]",
                    "Description",
                    "Translated Description [" + language + "]");
                foreach (var field in extraFields)
                {
                    sb.AppendFormat(",{0},{1} [{2}]", field, field, language);
                }
                file.WriteLine(sb.ToString());

                foreach (var item in items)
                {
                    sb = new StringBuilder();
                    sb.AppendFormat("{0},{1},{2},{3},{4}",
                        WrapCSVValue(item.Name),
                        WrapCSVValue(item.LookupValue("Display Name")),
                        WrapCSVValue(item.LookupValue("Display Name " + language)),
                        WrapCSVValue(item.LookupValue("Description")),
                        WrapCSVValue(item.LookupValue("Description " + language)));
                    foreach (var field in extraFields)
                    {
                        sb.AppendFormat(",{0},{1}", WrapCSVValue(item.LookupValue(field)), WrapCSVValue(item.LookupValue(field + " " + language)));
                    }
                    file.WriteLine(sb.ToString());
                }
            }
        }

        private static List<DialogueEntry> SortEntries(List<DialogueEntry> entries, DialogueEntrySortMethod sortMethod)
        {
            switch (sortMethod)
            {
                case DialogueEntrySortMethod.DepthFirst:
                    return DialogueEntrySorter.DepthFirstSortEntries(entries);
                case DialogueEntrySortMethod.BreadthFirst:
                    return DialogueEntrySorter.BreadthFirstSortEntries(entries);
                default:
                    return new List<DialogueEntry>(entries);
            }
        }

        private static string LookupActorName(DialogueDatabase database, int actorID, Dictionary<int, string> actorNames)
        {
            if (!actorNames.TryGetValue(actorID, out var actorName))
            {
                var actor = database.GetActor(actorID);
                actorName = (actor != null) ? actor.Name : "Not Assigned";
                actorNames[actorID] = actorName;
            }
            return actorName;
        }

        private static string GetNewKeyFieldValue()
        {
            var s = Guid.NewGuid().ToString();
            var pos = s.IndexOf('-');
            return (pos == -1) ? s : s.Substring(0, pos);
        }

        /// <summary>Escapes newlines as \n and quotes values that contain commas or quotes.</summary>
        public static string WrapCSVValue(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            string s2 = s.Contains("\n") ? s.Replace("\n", "\\n") : s;
            if (s2.Contains("\r")) s2 = s2.Replace("\r", "\\r");
            if (s2.Contains(",") || s2.Contains("\""))
            {
                return "\"" + s2.Replace("\"", "\"\"") + "\"";
            }
            return s2;
        }

    }

}
