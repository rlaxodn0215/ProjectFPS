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
    /// Options for a global search of a dialogue database.
    /// </summary>
    public class GlobalSearchOptions
    {
        public string searchText = string.Empty;
        public string replaceText = string.Empty;
        public bool useRegex = false;
        public bool matchCase = true;

        /// <summary>Conversation to search, or -1 to search the whole database.</summary>
        public int conversationID = -1;
    }

    /// <summary>
    /// One string in the database that matches a global search.
    /// </summary>
    public class GlobalSearchMatch
    {
        public enum Kind { Database, Actor, Item, Quest, Location, Variable, Conversation, Entry }

        /// <summary>Serialized property path of the matching string, relative to the database.</summary>
        public string propertyPath;
        public Kind kind;
        /// <summary>ID of the asset that owns the string. For entries, the conversation ID.</summary>
        public int assetID = -1;
        /// <summary>Entry ID if kind is Entry, else -1.</summary>
        public int entryID = -1;
        /// <summary>Where the string is, such as "Actor 'Player'".</summary>
        public string ownerLabel;
        /// <summary>What the string is, such as a field title or "Conditions".</summary>
        public string fieldLabel;
        /// <summary>The string's value when it was found.</summary>
        public string value;
        /// <summary>Whether Replace Checked replaces this match.</summary>
        public bool include = true;
    }

    /// <summary>
    /// Matches and replaces text for a global search, as plain text or as a regular expression.
    /// </summary>
    public class GlobalSearchMatcher
    {

        // Keeps a runaway regular expression from hanging the editor.
        private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

        private readonly string searchText;
        private readonly string replaceText;
        private readonly StringComparison comparison;
        private readonly Regex regex;

        private GlobalSearchMatcher(GlobalSearchOptions options, Regex regex)
        {
            searchText = options.searchText;
            replaceText = options.replaceText ?? string.Empty;
            comparison = options.matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            this.regex = regex;
        }

        /// <summary>
        /// Creates a matcher for the options. Returns null and sets error if the search text
        /// is empty or isn't a valid regular expression.
        /// </summary>
        public static GlobalSearchMatcher Create(GlobalSearchOptions options, out string error)
        {
            error = null;
            if (options == null || string.IsNullOrEmpty(options.searchText))
            {
                error = "Enter text to search for.";
                return null;
            }
            Regex regex = null;
            if (options.useRegex)
            {
                try
                {
                    var regexOptions = options.matchCase ? RegexOptions.None : RegexOptions.IgnoreCase;
                    regex = new Regex(options.searchText, regexOptions, RegexTimeout);
                }
                catch (ArgumentException e)
                {
                    error = "Invalid regular expression: " + e.Message;
                    return null;
                }
            }
            return new GlobalSearchMatcher(options, regex);
        }

        public bool IsMatch(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            return (regex != null) ? regex.IsMatch(s) : s.IndexOf(searchText, comparison) >= 0;
        }

        /// <summary>
        /// Finds the first match in s. Returns false if there isn't one.
        /// </summary>
        public bool FindFirst(string s, out int index, out int length)
        {
            index = -1;
            length = 0;
            if (string.IsNullOrEmpty(s)) return false;
            if (regex != null)
            {
                var match = regex.Match(s);
                if (!match.Success) return false;
                index = match.Index;
                length = match.Length;
                return true;
            }
            index = s.IndexOf(searchText, comparison);
            length = searchText.Length;
            return index >= 0;
        }

        /// <summary>
        /// Replaces every match in s. A regular expression's replacement text can use
        /// substitutions such as $1.
        /// </summary>
        public string Replace(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            if (regex != null) return regex.Replace(s, replaceText);
            var sb = new StringBuilder();
            int start = 0;
            int index;
            while ((index = s.IndexOf(searchText, start, comparison)) >= 0)
            {
                sb.Append(s, start, index - start);
                sb.Append(replaceText);
                start = index + searchText.Length;
            }
            if (start == 0) return s;
            sb.Append(s, start, s.Length - start);
            return sb.ToString();
        }

    }

    /// <summary>
    /// Finds and replaces text across a dialogue database. Finding reads the database's C#
    /// objects. Replacing goes through a SerializedObject so it's one undo step and bound UI
    /// updates. Only field values of text-like types are searched, plus entry Conditions and
    /// Script and the database's Description and Global User Script. Field titles and ID,
    /// number and boolean fields are left alone so references stay valid.
    /// </summary>
    public static class GlobalSearchReplace
    {

        public const string UndoName = "Global Search & Replace";

        public class Result
        {
            public List<GlobalSearchMatch> matches = new List<GlobalSearchMatch>();
            public bool canceled = false;
            public string error = null;
        }

        /// <summary>
        /// True if a field's value is text that search & replace may change.
        /// Custom field types are covered by the type they're stored as.
        /// </summary>
        public static bool IsSearchableField(Field field)
        {
            if (field == null || string.IsNullOrEmpty(field.value)) return false;
            switch (field.type)
            {
                case FieldType.Text:
                case FieldType.Files:
                case FieldType.Localization:
                    return true;
                default:
                    return false;
            }
        }

        public static Result Find(DialogueDatabase database, GlobalSearchMatcher matcher, int conversationID)
        {
            var result = new Result();
            if (database == null || matcher == null) return result;
            var walker = new DatabaseTextWalker(database, "Global Search", "Searching");
            try
            {
                result.canceled = !walker.Walk(conversationID, location =>
                {
                    if (location.field != null && !IsSearchableField(location.field)) return;
                    if (!matcher.IsMatch(location.value)) return;
                    result.matches.Add(new GlobalSearchMatch()
                    {
                        propertyPath = location.propertyPath,
                        kind = location.kind,
                        assetID = location.assetID,
                        entryID = location.entryID,
                        ownerLabel = location.ownerLabel,
                        fieldLabel = location.fieldLabel,
                        value = location.value
                    });
                });
            }
            catch (RegexMatchTimeoutException)
            {
                result.matches.Clear();
                result.error = "The regular expression took too long to run. Try a simpler expression.";
            }
            return result;
        }

        /// <summary>
        /// Replaces text in the strings at the matches' property paths. Skips strings that no
        /// longer match, such as after edits since the search. Returns the number of strings changed.
        /// </summary>
        public static int Apply(SerializedObject serializedObject, IEnumerable<GlobalSearchMatch> matches, GlobalSearchMatcher matcher)
        {
            if (serializedObject == null || matches == null || matcher == null) return 0;
            serializedObject.Update();
            int count = 0;
            foreach (var match in matches)
            {
                var property = serializedObject.FindProperty(match.propertyPath);
                if (property == null || property.propertyType != SerializedPropertyType.String) continue;
                var current = property.stringValue;
                if (!matcher.IsMatch(current)) continue;
                var replaced = matcher.Replace(current);
                if (replaced == current) continue;
                property.stringValue = replaced;
                count++;
            }
            if (count > 0)
            {
                serializedObject.ApplyModifiedProperties();
                Undo.SetCurrentGroupName(UndoName);
            }
            return count;
        }

    }

}

#endif
