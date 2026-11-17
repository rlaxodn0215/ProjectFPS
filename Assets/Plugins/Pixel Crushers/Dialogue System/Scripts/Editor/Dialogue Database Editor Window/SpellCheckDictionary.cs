// Copyright (c) Pixel Crushers. All rights reserved.

#if UNITY_2022_1_OR_NEWER

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PixelCrushers.DialogueSystem.DialogueEditor
{

    /// <summary>
    /// The project's spell check dictionary of extra words, such as character and place
    /// names. It's a JSON file in ProjectSettings so a team shares it through version control.
    /// Words match regardless of case.
    /// </summary>
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public static class SpellCheckDictionary
    {

        public const string RelativePath = "ProjectSettings/DialogueSystemSpellCheckDictionary.json";

        [Serializable]
        private class Data
        {
            public List<string> words = new List<string>();
        }

        private static List<string> s_words = null;
        private static HashSet<string> s_lookup = null;
        private static DateTime s_loadedTimestamp = DateTime.MinValue;

        public static string FullPath
        {
            get { return Path.Combine(Path.GetDirectoryName(Application.dataPath), RelativePath); }
        }

        /// <summary>The words, sorted. Don't modify the list; use Add and Remove.</summary>
        public static List<string> Words
        {
            get
            {
                EnsureLoaded();
                return s_words;
            }
        }

        public static bool Contains(string word)
        {
            if (string.IsNullOrEmpty(word)) return false;
            EnsureLoaded();
            return s_lookup.Contains(word);
        }

        public static void Add(string word)
        {
            word = (word ?? string.Empty).Trim();
            if (word.Length == 0 || Contains(word)) return;
            s_words.Add(word);
            s_words.Sort(StringComparer.OrdinalIgnoreCase);
            s_lookup.Add(word);
            Save();
        }

        public static void Remove(string word)
        {
            EnsureLoaded();
            var index = s_words.FindIndex(w => string.Equals(w, word, StringComparison.OrdinalIgnoreCase));
            if (index < 0) return;
            s_words.RemoveAt(index);
            s_lookup.Remove(word);
            Save();
        }

        /// <summary>Reloads the file if it changed since it was loaded, such as after a version control update.</summary>
        private static void EnsureLoaded()
        {
            var path = FullPath;
            var timestamp = File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;
            if (s_words != null && timestamp == s_loadedTimestamp) return;
            s_loadedTimestamp = timestamp;
            Data data = null;
            if (timestamp != DateTime.MinValue)
            {
                try
                {
                    data = JsonUtility.FromJson<Data>(File.ReadAllText(path));
                }
                catch (Exception e)
                {
                    Debug.LogWarning("Dialogue System: Couldn't read the spell check dictionary " + RelativePath + ": " + e.Message);
                }
            }
            s_words = new List<string>();
            s_lookup = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (data == null || data.words == null) return;
            foreach (var word in data.words)
            {
                var trimmed = (word ?? string.Empty).Trim();
                if (trimmed.Length == 0 || s_lookup.Contains(trimmed)) continue;
                s_words.Add(trimmed);
                s_lookup.Add(trimmed);
            }
            s_words.Sort(StringComparer.OrdinalIgnoreCase);
        }

        private static void Save()
        {
            var path = FullPath;
            try
            {
                File.WriteAllText(path, JsonUtility.ToJson(new Data() { words = s_words }, true));
                s_loadedTimestamp = File.GetLastWriteTimeUtc(path);
            }
            catch (Exception e)
            {
                Debug.LogError("Dialogue System: Couldn't save the spell check dictionary " + RelativePath + ": " + e.Message);
            }
        }

    }

}

#endif
