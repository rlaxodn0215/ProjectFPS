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
    /// A database string that spell check checks, such as an entry's Dialogue Text.
    /// </summary>
    public class SpellCheckTarget
    {
        /// <summary>Serialized property path of the string, relative to the database.</summary>
        public string propertyPath;
        public GlobalSearchMatch.Kind kind;
        /// <summary>ID of the asset that owns the string. For entries, the conversation ID.</summary>
        public int assetID;
        /// <summary>Entry ID if kind is Entry, else -1.</summary>
        public int entryID;
        public string ownerLabel;
        public string fieldLabel;
        /// <summary>JSpell language code.</summary>
        public string language;
        /// <summary>The string's value. Updated when spell check fixes a word in it.</summary>
        public string value;
        /// <summary>The value with markup replaced by spaces, so positions are the same.</summary>
        public string masked;
        public List<SpellCheckWordSpan> words;
    }

    public struct SpellCheckWordSpan
    {
        public int start;
        public int length;
        public int end { get { return start + length; } }
    }

    /// <summary>
    /// A possible misspelling, or a repeated word, in a target string.
    /// </summary>
    public class SpellCheckIssue
    {
        public SpellCheckTarget target;
        /// <summary>Where the text to replace starts in the target's value.</summary>
        public int position;
        /// <summary>The text to replace, exactly as it is in the value.</summary>
        public string original;
        /// <summary>The misspelled word, for Ignore All and the dictionary. For a repeated word, the text to replace.</summary>
        public string word;
        public string[] suggestions;
        /// <summary>The text that Fix replaces the original text with.</summary>
        public string replacement;
        public bool isDoubledWord;
    }

    public class SpellCheckResult
    {
        public List<SpellCheckIssue> issues = new List<SpellCheckIssue>();
        public int targetCount;
        public int checkedWordCount;
        public int requestsSent;
        public int wordsSent;
        public int wordsFromCache;
        public List<string> unsupportedLanguages = new List<string>();
        public bool canceled;
        public string error;
        public DateTime checkedAt = DateTime.Now;
    }

    /// <summary>
    /// Finds possible misspellings in a dialogue database with the JSpell Checker API.
    /// To use as little API quota as possible, it sends each language's distinct words, not
    /// the database text, packing as many into each request as fit, and caches JSpell's
    /// answer for every word it sends (SpellCheckWordCache). Then it maps misspelled words
    /// back to every place they occur. Repeated words are found locally.
    /// </summary>
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public static class SpellChecker
    {

        public const string UndoName = "Spell Check Fix";

        /// <summary>Most characters to send in one request, under JSpell's 100,000 limit.</summary>
        public const int MaxRequestCharacters = 99000;

        // Words are sent separated by spaces, not line breaks, since JSpell treats the first
        // word of each line as the start of a sentence: it flags lowercase words for
        // capitalization and capitalizes their suggestions. A request starts with a correctly
        // capitalized word so the first word that's checked isn't at the start of a sentence.
        private const string WordSeparator = " ";
        private const string RequestPrefix = "The ";

        private static readonly Regex MarkupRegex = new Regex(
            @"\[[^\]\r\n]*\]|<[^>\r\n]*>|\{\{[^}\r\n]*\}\}|https?://\S+", RegexOptions.Compiled);

        private static readonly Regex WordRegex = new Regex(
            @"[\p{L}\p{M}\p{Nd}]+(?:['’\-][\p{L}\p{M}\p{Nd}]+)*", RegexOptions.Compiled);

        private static HashSet<string> s_ignoredWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static HashSet<string> s_ignoredOccurrences = new HashSet<string>(StringComparer.Ordinal);

        #region Ignore

        /// <summary>Ignores a word everywhere until the editor is restarted.</summary>
        public static void IgnoreWord(string word)
        {
            if (!string.IsNullOrEmpty(word)) s_ignoredWords.Add(word);
        }

        /// <summary>Ignores one issue until the editor is restarted.</summary>
        public static void IgnoreOccurrence(SpellCheckIssue issue)
        {
            if (issue != null) s_ignoredOccurrences.Add(GetOccurrenceKey(issue));
        }

        public static void ClearIgnoredWords()
        {
            s_ignoredWords.Clear();
            s_ignoredOccurrences.Clear();
        }

        private static string GetOccurrenceKey(SpellCheckIssue issue)
        {
            return issue.target.propertyPath + "|" + issue.target.value + "|" + issue.position + "|" + issue.original;
        }

        #endregion

        #region Text

        /// <summary>
        /// Replaces markup, such as [em1], [var=x], rich text tags and URLs, with spaces so
        /// it isn't checked. The result is the same length, so positions still match.
        /// </summary>
        public static string MaskMarkup(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return MarkupRegex.Replace(value, match => new string(' ', match.Length));
        }

        public static List<SpellCheckWordSpan> Tokenize(string text)
        {
            var spans = new List<SpellCheckWordSpan>();
            if (string.IsNullOrEmpty(text)) return spans;
            for (var match = WordRegex.Match(text); match.Success; match = match.NextMatch())
            {
                spans.Add(new SpellCheckWordSpan() { start = match.Index, length = match.Length });
            }
            return spans;
        }

        /// <summary>True if a word should be checked at all, according to the settings.</summary>
        public static bool ShouldCheckWord(string word, SpellCheckSettings settings)
        {
            if (string.IsNullOrEmpty(word) || word.Length < 2) return false;
            bool hasLetter = false, hasDigit = false, hasLower = false;
            foreach (var c in word)
            {
                if (char.IsLetter(c))
                {
                    hasLetter = true;
                    if (char.IsLower(c)) hasLower = true;
                }
                else if (char.IsDigit(c))
                {
                    hasDigit = true;
                }
            }
            if (!hasLetter) return false;
            if (hasDigit && settings.ignoreWordsWithNumbers) return false;
            if (!hasLower && settings.ignoreAllCapsWords) return false;
            if (settings.ignoreCapitalizedWords && char.IsUpper(word[0])) return false;
            return true;
        }

        /// <summary>
        /// Packs words into as few request texts as possible, separated by spaces, each at
        /// most maxCharacters long, including the request's prefix.
        /// </summary>
        public static List<List<string>> PackWords(IEnumerable<string> words, int maxCharacters = MaxRequestCharacters)
        {
            var batches = new List<List<string>>();
            List<string> batch = null;
            var length = 0;
            foreach (var word in words)
            {
                if (string.IsNullOrEmpty(word) || RequestPrefix.Length + word.Length > maxCharacters) continue;
                var added = (batch == null || batch.Count == 0) ? word.Length : word.Length + WordSeparator.Length;
                if (batch == null || length + added > maxCharacters)
                {
                    batch = new List<string>();
                    batches.Add(batch);
                    length = RequestPrefix.Length;
                    added = word.Length;
                }
                batch.Add(word);
                length += added;
            }
            return batches;
        }

        /// <summary>The request text for a batch of words made by PackWords.</summary>
        public static string JoinWords(List<string> words)
        {
            return RequestPrefix + string.Join(WordSeparator, words);
        }

        /// <summary>
        /// True if the error is only about capitalization, such as 'monday' with the
        /// suggestion 'Monday'. The word is spelled correctly, so it isn't an issue.
        /// </summary>
        public static bool IsCapitalizationOnly(string word, string[] suggestions)
        {
            if (string.IsNullOrEmpty(word) || suggestions == null) return false;
            foreach (var suggestion in suggestions)
            {
                if (string.Equals(suggestion, word, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        /// <summary>
        /// Maps a response to the words of the request text made by JoinWords. Every word gets
        /// an entry, with an empty list if it's spelled correctly.
        /// </summary>
        public static Dictionary<string, List<SpellCheckWordError>> MapResponse(List<string> words, JSpellResponse response)
        {
            var results = new Dictionary<string, List<SpellCheckWordError>>(StringComparer.Ordinal);
            var starts = new int[words.Count];
            var position = RequestPrefix.Length;
            for (int i = 0; i < words.Count; i++)
            {
                starts[i] = position;
                position += words[i].Length + WordSeparator.Length;
                results[words[i]] = new List<SpellCheckWordError>();
            }
            if (response == null || response.elements == null) return results;
            foreach (var element in response.elements)
            {
                if (element == null || element.errors == null) continue;
                foreach (var error in element.errors)
                {
                    if (error == null || string.IsNullOrEmpty(error.word)) continue;
                    if (error.position < RequestPrefix.Length) continue; // The prefix, not a word that was sent.
                    if (IsCapitalizationOnly(error.word, error.suggestions)) continue;
                    var index = Array.BinarySearch(starts, error.position);
                    if (index < 0) index = ~index - 1;
                    string word = null;
                    int offset = 0;
                    if (index >= 0 && index < words.Count)
                    {
                        offset = error.position - starts[index];
                        var candidate = words[index];
                        if (offset >= 0 && offset + error.word.Length <= candidate.Length &&
                            string.CompareOrdinal(candidate, offset, error.word, 0, error.word.Length) == 0)
                        {
                            word = candidate;
                        }
                    }
                    if (word == null && results.ContainsKey(error.word))
                    {
                        // Position didn't line up, but the error is a whole word that was sent.
                        word = error.word;
                        offset = 0;
                    }
                    if (word == null) continue;
                    var list = results[word];
                    if (list.Exists(e => e.offset == offset && e.word == error.word)) continue;
                    list.Add(new SpellCheckWordError()
                    {
                        offset = offset,
                        word = error.word,
                        suggestions = error.suggestions ?? new string[0]
                    });
                }
            }
            return results;
        }

        #endregion

        #region Collect

        /// <summary>
        /// Finds the strings to check: entries' Dialogue Text and Menu Text, the actor, item,
        /// quest and location fields named in the settings, and, if enabled, their localized
        /// versions in languages that JSpell supports. If conversationID isn't -1, only that
        /// conversation's entries are checked.
        /// </summary>
        public static List<SpellCheckTarget> CollectTargets(DialogueDatabase database, SpellCheckSettings settings, int conversationID,
            List<string> unsupportedLanguages, out bool canceled)
        {
            var targets = new List<SpellCheckTarget>();
            canceled = false;
            if (database == null) return targets;
            var walker = new DatabaseTextWalker(database, "Spell Check", "Reading");
            canceled = !walker.Walk(conversationID, location =>
            {
                var field = location.field;
                if (field == null || string.IsNullOrEmpty(location.value)) return;
                if (field.type != FieldType.Text && field.type != FieldType.Localization) return;
                var language = GetTargetLanguage(location.kind, field, settings, unsupportedLanguages);
                if (language == null) return;
                var masked = MaskMarkup(location.value);
                var words = Tokenize(masked);
                if (words.Count == 0) return;
                targets.Add(new SpellCheckTarget()
                {
                    propertyPath = location.propertyPath,
                    kind = location.kind,
                    assetID = location.assetID,
                    entryID = location.entryID,
                    ownerLabel = location.ownerLabel,
                    fieldLabel = location.fieldLabel,
                    language = language,
                    value = location.value,
                    masked = masked,
                    words = words
                });
            });
            return targets;
        }

        /// <summary>Returns the JSpell language to check a field in, or null to skip the field.</summary>
        private static string GetTargetLanguage(GlobalSearchMatch.Kind kind, Field field, SpellCheckSettings settings, List<string> unsupportedLanguages)
        {
            var title = field.title;
            if (string.IsNullOrEmpty(title)) return null;
            bool isEntry;
            switch (kind)
            {
                case GlobalSearchMatch.Kind.Entry:
                    isEntry = true;
                    break;
                case GlobalSearchMatch.Kind.Actor:
                case GlobalSearchMatch.Kind.Item:
                case GlobalSearchMatch.Kind.Quest:
                case GlobalSearchMatch.Kind.Location:
                    isEntry = false;
                    break;
                default:
                    return null;
            }
            if (isEntry ? (title == "Dialogue Text" || title == "Menu Text") : settings.IsAssetFieldTitle(title))
            {
                return settings.mainLanguage;
            }
            if (!settings.checkLocalizedFields || field.type != FieldType.Localization) return null;

            // A localized field's title is the original title plus a language code, such as
            // "Menu Text fr". An entry's localized Dialogue Text field is titled only with
            // the language code.
            string code = null;
            var space = title.LastIndexOf(' ');
            if (space > 0)
            {
                var baseTitle = title.Substring(0, space);
                var isLocalizedVersion = isEntry ? (baseTitle == "Dialogue Text" || baseTitle == "Menu Text") : settings.IsAssetFieldTitle(baseTitle);
                if (isLocalizedVersion) code = title.Substring(space + 1);
            }
            else if (isEntry)
            {
                code = title;
            }
            if (string.IsNullOrEmpty(code)) return null;
            if (JSpellClient.TryMapLanguageCode(code, out var jspellCode)) return jspellCode;
            if (unsupportedLanguages != null && !unsupportedLanguages.Contains(code)) unsupportedLanguages.Add(code);
            return null;
        }

        /// <summary>
        /// The words in actor, item, quest, location and variable names and conversation
        /// titles, which spell check can skip.
        /// </summary>
        public static HashSet<string> CollectNameWords(DialogueDatabase database)
        {
            var words = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (database == null) return words;
            foreach (var actor in database.actors)
            {
                if (actor == null) continue;
                AddNameWords(words, actor.Name);
                AddNameWords(words, Field.LookupValue(actor.fields, "Display Name"));
            }
            foreach (var item in database.items)
            {
                if (item == null) continue;
                AddNameWords(words, item.Name);
                AddNameWords(words, Field.LookupValue(item.fields, "Display Name"));
            }
            foreach (var location in database.locations)
            {
                if (location != null) AddNameWords(words, location.Name);
            }
            foreach (var variable in database.variables)
            {
                if (variable != null) AddNameWords(words, variable.Name);
            }
            foreach (var conversation in database.conversations)
            {
                if (conversation != null) AddNameWords(words, conversation.Title);
            }
            return words;
        }

        private static void AddNameWords(HashSet<string> words, string name)
        {
            if (string.IsNullOrEmpty(name)) return;
            foreach (var span in Tokenize(name))
            {
                var word = name.Substring(span.start, span.length);
                words.Add(word);
                // Also add the parts of names like "Mary-Jane" and "Kael's".
                foreach (var part in word.Split('-', '\'', '’'))
                {
                    if (part.Length > 1) words.Add(part);
                }
            }
        }

        #endregion

        #region Issues

        /// <summary>
        /// Lists the issues in the targets, using the words that the cache knows. Words that
        /// haven't been checked yet are skipped.
        /// </summary>
        public static List<SpellCheckIssue> BuildIssues(List<SpellCheckTarget> targets, SpellCheckSettings settings, HashSet<string> nameWords)
        {
            var allIssues = new List<SpellCheckIssue>();
            var issues = new List<SpellCheckIssue>();
            foreach (var target in targets)
            {
                issues.Clear();
                foreach (var span in target.words)
                {
                    var word = target.masked.Substring(span.start, span.length);
                    if (!ShouldCheckWord(word, settings) || IsKnownWord(word, nameWords)) continue;
                    if (!SpellCheckWordCache.TryGet(target.language, word, out var errors)) continue;
                    foreach (var error in errors)
                    {
                        if (IsKnownWord(error.word, nameWords) || IsCapitalizationOnly(error.word, error.suggestions)) continue;
                        var position = span.start + error.offset;
                        if (position < 0 || position + error.word.Length > target.value.Length ||
                            string.CompareOrdinal(target.value, position, error.word, 0, error.word.Length) != 0) continue;
                        AddIssue(issues, new SpellCheckIssue()
                        {
                            target = target,
                            position = position,
                            original = error.word,
                            word = error.word,
                            suggestions = error.suggestions ?? new string[0],
                            replacement = (error.suggestions != null && error.suggestions.Length > 0) ? error.suggestions[0] : string.Empty
                        });
                    }
                }
                if (settings.checkDoubledWords) AddDoubledWordIssues(issues, target);
                issues.Sort((a, b) => a.position.CompareTo(b.position));
                allIssues.AddRange(issues);
            }
            return allIssues;
        }

        private static void AddIssue(List<SpellCheckIssue> issues, SpellCheckIssue issue)
        {
            if (s_ignoredOccurrences.Contains(GetOccurrenceKey(issue))) return;
            issues.Add(issue);
        }

        public static bool IsKnownWord(string word, HashSet<string> nameWords)
        {
            if (string.IsNullOrEmpty(word)) return true;
            if (s_ignoredWords.Contains(word) || SpellCheckDictionary.Contains(word)) return true;
            return nameWords != null && nameWords.Contains(word);
        }

        /// <summary>Finds a word repeated with only spaces or tabs between, such as "the the".</summary>
        private static void AddDoubledWordIssues(List<SpellCheckIssue> issues, SpellCheckTarget target)
        {
            for (int i = 1; i < target.words.Count; i++)
            {
                var previous = target.words[i - 1];
                var current = target.words[i];
                if (previous.length != current.length) continue;
                if (string.Compare(target.value, previous.start, target.value, current.start, current.length, StringComparison.OrdinalIgnoreCase) != 0) continue;
                var onlySpaces = current.start > previous.end;
                for (int j = previous.end; j < current.start && onlySpaces; j++)
                {
                    var c = target.value[j];
                    if (c != ' ' && c != '\t') onlySpaces = false;
                }
                if (!onlySpaces) continue;
                var firstWord = target.value.Substring(previous.start, previous.length);
                if (!char.IsLetter(firstWord[0])) continue;
                var original = target.value.Substring(previous.start, current.end - previous.start);
                if (s_ignoredWords.Contains(original)) continue;
                AddIssue(issues, new SpellCheckIssue()
                {
                    target = target,
                    position = previous.start,
                    original = original,
                    word = original,
                    suggestions = new string[] { firstWord },
                    replacement = firstWord,
                    isDoubledWord = true
                });
            }
        }

        #endregion

        #region Fix

        /// <summary>
        /// Replaces each issue's original text with its replacement as one undoable change.
        /// Skips issues whose text has changed since the check or whose owner has moved.
        /// Updates the positions of openIssues in the same strings. Returns the issues fixed.
        /// </summary>
        public static List<SpellCheckIssue> ApplyFixes(SerializedObject serializedObject, IList<SpellCheckIssue> fixes, IList<SpellCheckIssue> openIssues)
        {
            var fixedIssues = new List<SpellCheckIssue>();
            if (serializedObject == null || serializedObject.targetObject == null || fixes == null || fixes.Count == 0) return fixedIssues;
            serializedObject.Update();
            var fixesByTarget = new Dictionary<SpellCheckTarget, List<SpellCheckIssue>>();
            foreach (var issue in fixes)
            {
                if (issue == null || issue.target == null || issue.replacement == null) continue;
                if (!fixesByTarget.TryGetValue(issue.target, out var list))
                {
                    list = new List<SpellCheckIssue>();
                    fixesByTarget.Add(issue.target, list);
                }
                list.Add(issue);
            }
            foreach (var kvp in fixesByTarget)
            {
                var target = kvp.Key;
                var property = serializedObject.FindProperty(target.propertyPath);
                if (property == null || property.propertyType != SerializedPropertyType.String) continue;
                if (!OwnerMatches(serializedObject, target)) continue;
                var value = property.stringValue ?? string.Empty;
                var edits = new List<KeyValuePair<int, int>>(); // Position, length change.
                var list = kvp.Value;
                list.Sort((a, b) => b.position.CompareTo(a.position)); // Right to left.
                foreach (var issue in list)
                {
                    var position = Locate(value, issue);
                    if (position < 0) continue;
                    value = value.Substring(0, position) + issue.replacement + value.Substring(position + issue.original.Length);
                    edits.Add(new KeyValuePair<int, int>(position, issue.replacement.Length - issue.original.Length));
                    fixedIssues.Add(issue);
                }
                if (edits.Count == 0) continue;
                property.stringValue = value;
                ShiftOpenIssues(target, value, edits, openIssues, fixedIssues);
            }
            if (fixedIssues.Count > 0)
            {
                serializedObject.ApplyModifiedProperties();
                Undo.SetCurrentGroupName(UndoName);
            }
            return fixedIssues;
        }

        /// <summary>Returns where an issue's original text is in value now, or -1.</summary>
        private static int Locate(string value, SpellCheckIssue issue)
        {
            var original = issue.original;
            if (string.IsNullOrEmpty(original)) return -1;
            if (IsWholeWordAt(value, issue.position, original)) return issue.position;
            // The text moved. Use the nearest whole-word occurrence.
            var best = -1;
            for (var index = value.IndexOf(original, StringComparison.Ordinal); index >= 0;
                index = (index + 1 < value.Length) ? value.IndexOf(original, index + 1, StringComparison.Ordinal) : -1)
            {
                if (!IsWholeWordAt(value, index, original)) continue;
                if (best < 0 || Math.Abs(index - issue.position) < Math.Abs(best - issue.position)) best = index;
            }
            return best;
        }

        private static bool IsWholeWordAt(string value, int position, string word)
        {
            if (position < 0 || position + word.Length > value.Length) return false;
            if (string.CompareOrdinal(value, position, word, 0, word.Length) != 0) return false;
            if (position > 0 && IsWordChar(value[position - 1])) return false;
            var end = position + word.Length;
            return end >= value.Length || !IsWordChar(value[end]);
        }

        private static bool IsWordChar(char c)
        {
            return char.IsLetterOrDigit(c) || char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.NonSpacingMark;
        }

        private static void ShiftOpenIssues(SpellCheckTarget target, string newValue, List<KeyValuePair<int, int>> edits,
            IList<SpellCheckIssue> openIssues, List<SpellCheckIssue> fixedIssues)
        {
            if (openIssues != null)
            {
                foreach (var issue in openIssues)
                {
                    if (issue.target != target || fixedIssues.Contains(issue)) continue;
                    var shift = 0;
                    foreach (var edit in edits)
                    {
                        if (edit.Key < issue.position) shift += edit.Value;
                    }
                    issue.position += shift;
                }
            }
            target.value = newValue;
            target.masked = MaskMarkup(newValue);
            target.words = Tokenize(target.masked);
        }

        /// <summary>
        /// Checks that the asset or entry at the target's property path still has the
        /// target's ID, in case assets or entries were added, removed or reordered.
        /// </summary>
        private static bool OwnerMatches(SerializedObject serializedObject, SpellCheckTarget target)
        {
            var index = target.propertyPath.LastIndexOf(".fields.Array.data[", StringComparison.Ordinal);
            if (index < 0) return true;
            var owner = serializedObject.FindProperty(target.propertyPath.Substring(0, index));
            var idProperty = (owner != null) ? owner.FindPropertyRelative("id") : null;
            var expectedID = (target.kind == GlobalSearchMatch.Kind.Entry) ? target.entryID : target.assetID;
            return idProperty != null && idProperty.intValue == expectedID;
        }

        #endregion

        /// <summary>Text listing the issues, for the clipboard.</summary>
        public static string ToText(List<SpellCheckIssue> issues)
        {
            var sb = new StringBuilder();
            foreach (var issue in issues)
            {
                sb.AppendFormat("{0} / {1}: {2}", issue.target.ownerLabel, issue.target.fieldLabel, issue.original);
                if (issue.isDoubledWord) sb.Append(" (repeated word)");
                else if (issue.suggestions != null && issue.suggestions.Length > 0) sb.Append(" -> ").Append(string.Join(", ", issue.suggestions));
                sb.AppendLine();
            }
            return sb.ToString();
        }

    }

    /// <summary>
    /// Runs a spell check: collects the text, sends the words that aren't cached to JSpell
    /// one request at a time, and reports the result. Requests don't block the editor.
    /// </summary>
    public class SpellCheckRun
    {

        private readonly DialogueDatabase database;
        private readonly int conversationID;
        private readonly SpellCheckSettings settings;
        private readonly Action<SpellCheckResult> completed;
        private readonly SpellCheckResult result = new SpellCheckResult();
        private readonly Queue<KeyValuePair<string, List<string>>> batches = new Queue<KeyValuePair<string, List<string>>>();
        private List<SpellCheckTarget> targets;
        private HashSet<string> nameWords;
        private JSpellClient.Call currentCall;
        private int totalBatches;

        private const int MaxRetries = 3;
        private const float RetryDelaySeconds = 2;

        public bool isRunning { get; private set; }

        /// <summary>Describes the request being sent, for a status label.</summary>
        public string progressText { get; private set; }

        public Action progressChanged;

        public SpellCheckRun(DialogueDatabase database, int conversationID, SpellCheckSettings settings, Action<SpellCheckResult> completed)
        {
            this.database = database;
            this.conversationID = conversationID;
            this.settings = settings;
            this.completed = completed;
        }

        /// <summary>
        /// Collects the text and queues requests for uncached words. Call ApplyPendingChanges
        /// on the window first so the database's objects are current.
        /// </summary>
        public void Start()
        {
            isRunning = true;
            targets = SpellChecker.CollectTargets(database, settings, conversationID, result.unsupportedLanguages, out var canceled);
            if (canceled)
            {
                result.canceled = true;
                Finish();
                return;
            }
            result.targetCount = targets.Count;
            nameWords = settings.ignoreDatabaseNames ? SpellChecker.CollectNameWords(database) : null;

            // Distinct words per language that need checking.
            var wordsByLanguage = new Dictionary<string, HashSet<string>>();
            foreach (var target in targets)
            {
                if (!wordsByLanguage.TryGetValue(target.language, out var words))
                {
                    words = new HashSet<string>(StringComparer.Ordinal);
                    wordsByLanguage.Add(target.language, words);
                }
                foreach (var span in target.words)
                {
                    var word = target.masked.Substring(span.start, span.length);
                    if (!SpellChecker.ShouldCheckWord(word, settings) || SpellChecker.IsKnownWord(word, nameWords)) continue;
                    words.Add(word);
                }
            }
            foreach (var kvp in wordsByLanguage)
            {
                result.checkedWordCount += kvp.Value.Count;
                var uncached = new List<string>();
                foreach (var word in kvp.Value)
                {
                    if (SpellCheckWordCache.TryGet(kvp.Key, word, out _)) result.wordsFromCache++;
                    else uncached.Add(word);
                }
                uncached.Sort(StringComparer.Ordinal);
                foreach (var batch in SpellChecker.PackWords(uncached))
                {
                    batches.Enqueue(new KeyValuePair<string, List<string>>(kvp.Key, batch));
                }
            }
            totalBatches = batches.Count;
            if (totalBatches > 0 && !settings.HasApiKey)
            {
                result.error = "Enter your JSpell Checker API key in Settings to check words that haven't been checked before.";
                Finish();
                return;
            }
            SendNext();
        }

        public void Cancel()
        {
            if (!isRunning) return;
            result.canceled = true;
            if (currentCall != null) currentCall.Abort();
            currentCall = null;
            Finish();
        }

        private void SendNext()
        {
            if (!isRunning) return;
            if (batches.Count == 0)
            {
                Finish();
                return;
            }
            Send(batches.Dequeue(), 0);
        }

        private void Send(KeyValuePair<string, List<string>> batch, int retries)
        {
            var language = batch.Key;
            var words = batch.Value;
            progressText = string.Format("Checking {0} words in {1}: request {2} of {3}...",
                words.Count, JSpellClient.GetLanguageName(language), totalBatches - batches.Count, totalBatches);
            progressChanged?.Invoke();
            var request = new JSpellRequest() { language = language, fieldvalues = SpellChecker.JoinWords(words) };
            JSpellClient.Call call = null;
            call = JSpellClient.Send(settings.apiKey.Trim(), request, (response, error) =>
            {
                currentCall = null;
                if (!isRunning) return;
                result.requestsSent++;
                if (error != null && call.isTooManyRequests && retries < MaxRetries)
                {
                    // The free plan is rate limited. Wait, then send the same words again.
                    var delay = RetryDelaySeconds * (retries + 1);
                    progressText = string.Format("JSpell is busy. Trying again in {0} seconds...", delay);
                    progressChanged?.Invoke();
                    WaitThen(delay, () => Send(batch, retries + 1));
                    return;
                }
                if (error != null)
                {
                    result.error = error;
                    Finish();
                    return;
                }
                result.wordsSent += words.Count;
                foreach (var kvp in SpellChecker.MapResponse(words, response))
                {
                    SpellCheckWordCache.Set(language, kvp.Key, kvp.Value);
                }
                SendNext();
            });
            currentCall = call;
        }

        /// <summary>Calls action after a delay, unless the check is canceled first.</summary>
        private void WaitThen(float seconds, Action action)
        {
            var time = EditorApplication.timeSinceStartup + seconds;
            EditorApplication.CallbackFunction wait = null;
            wait = () =>
            {
                if (!isRunning)
                {
                    EditorApplication.update -= wait;
                    return;
                }
                if (EditorApplication.timeSinceStartup < time) return;
                EditorApplication.update -= wait;
                action();
            };
            EditorApplication.update += wait;
        }

        private void Finish()
        {
            if (!isRunning) return;
            isRunning = false;
            SpellCheckWordCache.Save();
            if (targets != null) result.issues = SpellChecker.BuildIssues(targets, settings, nameWords);
            result.checkedAt = DateTime.Now;
            progressText = null;
            completed?.Invoke(result);
        }

    }

}

#endif
