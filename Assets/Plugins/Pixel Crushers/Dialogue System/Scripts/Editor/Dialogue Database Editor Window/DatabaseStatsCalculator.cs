// Copyright (c) Pixel Crushers. All rights reserved.

#if UNITY_2022_1_OR_NEWER

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;

namespace PixelCrushers.DialogueSystem.DialogueEditor
{

    /// <summary>
    /// Word counts by language. The default (unlocalized) language is DatabaseStats.DefaultLanguage.
    /// </summary>
    public class WordCounts
    {
        public Dictionary<string, int> byLanguage = new Dictionary<string, int>();

        public int Total
        {
            get
            {
                int total = 0;
                foreach (var kvp in byLanguage) total += kvp.Value;
                return total;
            }
        }

        public int Get(string language)
        {
            int count;
            return byLanguage.TryGetValue(language, out count) ? count : 0;
        }

        public void Add(string language, int count)
        {
            if (count == 0) return;
            byLanguage[language] = Get(language) + count;
        }

        public void AddFrom(WordCounts other)
        {
            foreach (var kvp in other.byLanguage) Add(kvp.Key, kvp.Value);
        }
    }

    public class ActorStatsRow
    {
        public int actorID;
        public string label;
        /// <summary>False if no actor in the database has actorID.</summary>
        public bool exists;
        /// <summary>Conversations the actor speaks or is spoken to in.</summary>
        public HashSet<int> conversationIDs = new HashSet<int>();
        /// <summary>Entries the actor speaks.</summary>
        public int entries;
        /// <summary>Words the actor speaks.</summary>
        public WordCounts words = new WordCounts();
    }

    public class ConversationStatsRow
    {
        public int conversationID;
        public string title;
        public int entries;
        public WordCounts words = new WordCounts();
    }

    /// <summary>
    /// Asset counts and word counts for a dialogue database. Word counts include only
    /// player-facing text: quest and item Display Name, Description, Success/Failure
    /// Description and Entry N text, and dialogue entry Dialogue Text and Menu Text, plus
    /// their localized versions. Markup tags aren't counted. START entries aren't counted.
    /// </summary>
    public class DatabaseStats
    {
        public const string DefaultLanguage = "Default";

        public int numActors;
        public int numItems;
        public int numQuests;
        public int numLocations;
        public int numVariables;
        public int numConversations;
        public int numEntries;
        public int numEntriesNonBlank;
        public int numSceneEvents;

        /// <summary>DefaultLanguage first, then localized languages sorted.</summary>
        public List<string> languages = new List<string>();

        public WordCounts quests = new WordCounts();
        public WordCounts items = new WordCounts();
        public WordCounts conversations = new WordCounts();
        public WordCounts total = new WordCounts();

        public List<ActorStatsRow> actors = new List<ActorStatsRow>();
        public List<ConversationStatsRow> conversationRows = new List<ConversationStatsRow>();

        public DateTime computedAt;
        public bool canceled;
    }

    /// <summary>
    /// Computes DatabaseStats.
    /// </summary>
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public static class DatabaseStatsCalculator
    {

        // Databases with fewer entries than this don't show a progress bar.
        private const int ProgressBarThreshold = 20000;

        private static readonly string[] QuestTextFieldTitles =
        {
            DialogueSystemFields.DisplayName, DialogueSystemFields.Description,
            DialogueSystemFields.SuccessDescription, DialogueSystemFields.FailureDescription
        };

        private static readonly Regex QuestEntryTitleRegex = new Regex(@"^Entry \d+$");
        private static readonly Regex LocalizedQuestEntryTitleRegex = new Regex(@"^Entry \d+ (.+)$");

        // Primary entry fields, which can also have Localization type, aren't localized versions.
        private static readonly HashSet<string> EntryBuiltInFieldTitles = new HashSet<string>
        {
            "Title", "Description", "Actor", "Conversant", "Menu Text", "Dialogue Text", "Sequence", "Response Menu Sequence"
        };

        // Localized entry fields with these prefixes aren't spoken text.
        private static readonly string[] NonTextEntryLocalizationPrefixes = { "Sequence ", "Response Menu Sequence ", "Description ", "Title " };
        private const string MenuTextPrefix = "Menu Text ";
        private const string DialogueTextPrefix = "Dialogue Text ";

        // [em1], [/em1], [var=x], [lua(...)], [pic=2], and rich text tags such as <color=red>.
        private static readonly Regex MarkupRegex = new Regex(@"\[[^\[\]]*\]|<[^<>]*>");
        // Runs of letters and digits, allowing internal apostrophes and hyphens, as in "don't" and "well-known".
        private static readonly Regex WordRegex = new Regex(@"[\p{L}\p{N}]+(?:['’\-][\p{L}\p{N}]+)*");

        /// <summary>
        /// Counts the words in s after removing markup tags. Text without spaces between
        /// words, such as Chinese or Japanese, counts as one word per run.
        /// </summary>
        public static int CountWords(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            if (s.IndexOf('[') >= 0 || s.IndexOf('<') >= 0) s = MarkupRegex.Replace(s, " ");
            return WordRegex.Matches(s).Count;
        }

        public static DatabaseStats Compute(DialogueDatabase database)
        {
            var stats = new DatabaseStats() { computedAt = DateTime.Now };
            if (database == null) return stats;
            var languages = new HashSet<string>();
            try
            {
                stats.numActors = database.actors.Count;
                stats.numLocations = database.locations.Count;
                stats.numVariables = database.variables.Count;
                stats.numConversations = database.conversations.Count;
                CountItems(database, stats, languages);
                CountConversations(database, stats, languages);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            stats.total.AddFrom(stats.quests);
            stats.total.AddFrom(stats.items);
            stats.total.AddFrom(stats.conversations);
            stats.languages.AddRange(languages);
            stats.languages.Sort(StringComparer.OrdinalIgnoreCase);
            stats.languages.Insert(0, DatabaseStats.DefaultLanguage);
            stats.actors.Sort((a, b) => b.words.Total.CompareTo(a.words.Total));
            stats.conversationRows.Sort((a, b) => b.words.Total.CompareTo(a.words.Total));
            return stats;
        }

        private static void CountItems(DialogueDatabase database, DatabaseStats stats, HashSet<string> languages)
        {
            foreach (var item in database.items)
            {
                if (item == null) continue;
                var isItem = item.IsItem;
                if (isItem) stats.numItems++; else stats.numQuests++;
                var words = isItem ? stats.items : stats.quests;
                if (item.fields == null) continue;
                foreach (var field in item.fields)
                {
                    if (field == null || string.IsNullOrEmpty(field.title) || string.IsNullOrEmpty(field.value)) continue;
                    var language = GetQuestFieldLanguage(field);
                    if (language == null) continue;
                    if (language != DatabaseStats.DefaultLanguage) languages.Add(language);
                    words.Add(language, CountWords(field.value));
                }
            }
        }

        /// <summary>
        /// Returns the language of a quest or item text field, DefaultLanguage for an
        /// unlocalized one, or null if the field isn't player-facing text.
        /// </summary>
        private static string GetQuestFieldLanguage(Field field)
        {
            var title = field.title;
            if (field.type == FieldType.Text)
            {
                if (Array.IndexOf(QuestTextFieldTitles, title) >= 0 || QuestEntryTitleRegex.IsMatch(title)) return DatabaseStats.DefaultLanguage;
                return null;
            }
            if (field.type != FieldType.Localization) return null;
            foreach (var prefix in QuestTextFieldTitles)
            {
                if (title.Length > prefix.Length + 1 && title.StartsWith(prefix + " ")) return NonBlankOrNull(title.Substring(prefix.Length + 1));
            }
            var match = LocalizedQuestEntryTitleRegex.Match(title);
            if (match.Success && !title.EndsWith(" State")) return NonBlankOrNull(match.Groups[1].Value);
            return null;
        }

        private static string NonBlankOrNull(string s)
        {
            return string.IsNullOrWhiteSpace(s) ? null : s;
        }

        private static void CountConversations(DialogueDatabase database, DatabaseStats stats, HashSet<string> languages)
        {
            var totalEntries = 0;
            foreach (var conversation in database.conversations)
            {
                if (conversation != null) totalEntries += conversation.dialogueEntries.Count;
            }
            var showProgress = totalEntries >= ProgressBarThreshold;
            var actorRows = new Dictionary<int, ActorStatsRow>();
            int entriesDone = 0;
            foreach (var conversation in database.conversations)
            {
                if (conversation == null) continue;
                if (showProgress && EditorUtility.DisplayCancelableProgressBar("Computing Stats",
                    string.Format("Conversation '{0}'. Please wait...", conversation.Title), (float)entriesDone / totalEntries))
                {
                    stats.canceled = true;
                    return;
                }
                entriesDone += conversation.dialogueEntries.Count;
                var row = new ConversationStatsRow() { conversationID = conversation.id, title = conversation.Title };
                stats.conversationRows.Add(row);
                foreach (var entry in conversation.dialogueEntries)
                {
                    if (entry == null || entry.id == 0) continue; // START isn't a line of dialogue.
                    var actorRow = GetActorRow(database, actorRows, stats, entry.ActorID);
                    actorRow.conversationIDs.Add(conversation.id);
                    actorRow.entries++;
                    GetActorRow(database, actorRows, stats, entry.ConversantID).conversationIDs.Add(conversation.id);

                    stats.numEntries++;
                    row.entries++;
                    var dialogueText = entry.DialogueText;
                    var menuText = entry.MenuText;
                    if (!(string.IsNullOrEmpty(dialogueText) && string.IsNullOrEmpty(menuText))) stats.numEntriesNonBlank++;
                    if (!string.IsNullOrEmpty(entry.sceneEventGuid)) stats.numSceneEvents++;

                    AddEntryWords(DatabaseStats.DefaultLanguage, CountWords(dialogueText) + CountWords(menuText), stats, row, actorRow);
                    if (entry.fields == null) continue;
                    foreach (var field in entry.fields)
                    {
                        if (field == null || field.type != FieldType.Localization || string.IsNullOrEmpty(field.value)) continue;
                        var language = GetEntryFieldLanguage(field.title);
                        if (language == null) continue;
                        languages.Add(language);
                        AddEntryWords(language, CountWords(field.value), stats, row, actorRow);
                    }
                }
            }
        }

        private static void AddEntryWords(string language, int count, DatabaseStats stats, ConversationStatsRow row, ActorStatsRow actorRow)
        {
            stats.conversations.Add(language, count);
            row.words.Add(language, count);
            actorRow.words.Add(language, count);
        }

        /// <summary>
        /// Returns the language of a localized Dialogue Text or Menu Text field, such as "fr"
        /// for "fr" (Chat Mapper style), "Dialogue Text fr" or "Menu Text fr", or null if the
        /// field isn't localized spoken text.
        /// </summary>
        private static string GetEntryFieldLanguage(string title)
        {
            if (string.IsNullOrEmpty(title) || EntryBuiltInFieldTitles.Contains(title)) return null;
            foreach (var prefix in NonTextEntryLocalizationPrefixes)
            {
                if (title.StartsWith(prefix)) return null;
            }
            if (title.StartsWith(MenuTextPrefix)) return NonBlankOrNull(title.Substring(MenuTextPrefix.Length));
            if (title.StartsWith(DialogueTextPrefix)) return NonBlankOrNull(title.Substring(DialogueTextPrefix.Length));
            return NonBlankOrNull(title);
        }

        private static ActorStatsRow GetActorRow(DialogueDatabase database, Dictionary<int, ActorStatsRow> rows, DatabaseStats stats, int actorID)
        {
            ActorStatsRow row;
            if (rows.TryGetValue(actorID, out row)) return row;
            var actor = database.GetActor(actorID);
            row = new ActorStatsRow()
            {
                actorID = actorID,
                exists = actor != null,
                label = string.Format("[{0}] {1}", actorID, (actor != null) ? actor.Name : "(Unknown)")
            };
            rows.Add(actorID, row);
            stats.actors.Add(row);
            return row;
        }

        #region CSV

        /// <summary>
        /// Returns the stats as CSV: Asset Count, Word Count, Actors and Conversations
        /// sections separated by blank lines.
        /// </summary>
        public static string ToCsv(DatabaseStats stats)
        {
            var sb = new StringBuilder();
            AppendRow(sb, "Asset Count", "Count");
            AppendRow(sb, "Actors", stats.numActors.ToString());
            AppendRow(sb, "Items", stats.numItems.ToString());
            AppendRow(sb, "Quests", stats.numQuests.ToString());
            AppendRow(sb, "Locations", stats.numLocations.ToString());
            AppendRow(sb, "Variables", stats.numVariables.ToString());
            AppendRow(sb, "Conversations", stats.numConversations.ToString());
            AppendRow(sb, "Dialogue Entries", stats.numEntries.ToString());
            AppendRow(sb, "Entries Non-Blank", stats.numEntriesNonBlank.ToString());
            AppendRow(sb, "Scene Events", stats.numSceneEvents.ToString());
            sb.AppendLine();

            var header = new List<string> { "Word Count", "Total" };
            header.AddRange(stats.languages);
            AppendRow(sb, header.ToArray());
            AppendWordRow(sb, stats, "Quests", stats.quests);
            AppendWordRow(sb, stats, "Items", stats.items);
            AppendWordRow(sb, stats, "Conversations", stats.conversations);
            AppendWordRow(sb, stats, "Total", stats.total);
            sb.AppendLine();

            header = new List<string> { "Actor", "Conversations", "Entries", "Words" };
            header.AddRange(stats.languages);
            AppendRow(sb, header.ToArray());
            foreach (var row in stats.actors)
            {
                var cells = new List<string> { row.label, row.conversationIDs.Count.ToString(), row.entries.ToString(), row.words.Total.ToString() };
                foreach (var language in stats.languages) cells.Add(row.words.Get(language).ToString());
                AppendRow(sb, cells.ToArray());
            }
            sb.AppendLine();

            header = new List<string> { "Conversation", "Entries", "Words" };
            header.AddRange(stats.languages);
            AppendRow(sb, header.ToArray());
            foreach (var row in stats.conversationRows)
            {
                var cells = new List<string> { row.title, row.entries.ToString(), row.words.Total.ToString() };
                foreach (var language in stats.languages) cells.Add(row.words.Get(language).ToString());
                AppendRow(sb, cells.ToArray());
            }
            return sb.ToString();
        }

        private static void AppendWordRow(StringBuilder sb, DatabaseStats stats, string label, WordCounts words)
        {
            var cells = new List<string> { label, words.Total.ToString() };
            foreach (var language in stats.languages) cells.Add(words.Get(language).ToString());
            AppendRow(sb, cells.ToArray());
        }

        private static void AppendRow(StringBuilder sb, params string[] cells)
        {
            for (int i = 0; i < cells.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(CsvCell(cells[i]));
            }
            sb.AppendLine();
        }

        public static string CsvCell(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            if (s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0) return s;
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        }

        #endregion

    }

}

#endif
