// Copyright (c) Pixel Crushers. All rights reserved.

#if UNITY_2022_1_OR_NEWER

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PixelCrushers.DialogueSystem.DialogueEditor
{

    /// <summary>
    /// A spelling error that JSpell reported inside a word that spell check sent, such as
    /// one part of a hyphenated word.
    /// </summary>
    [Serializable]
    public class SpellCheckWordError
    {
        /// <summary>Where the misspelled text starts in the word that was sent.</summary>
        public int offset;
        public string word;
        public string[] suggestions;
    }

    /// <summary>
    /// Remembers JSpell's answer for every word spell check has sent, so a word is only sent
    /// once per language. Saved in the project's Library folder, which isn't committed.
    /// </summary>
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public static class SpellCheckWordCache
    {

        public const string RelativePath = "Library/DialogueSystem/SpellCheckWordCache.json";

        // Increase if what's cached changes, such as the config sent to JSpell.
        // Version 2: words are sent separated by spaces instead of line breaks.
        private const int Version = 2;

        [Serializable]
        private class WordData
        {
            public string word;
            public List<SpellCheckWordError> errors;
        }

        [Serializable]
        private class LanguageData
        {
            public string language;
            public List<WordData> words = new List<WordData>();
        }

        [Serializable]
        private class Data
        {
            public int version;
            public List<LanguageData> languages = new List<LanguageData>();
        }

        private static readonly List<SpellCheckWordError> NoErrors = new List<SpellCheckWordError>();

        // Language -> word -> errors (empty if the word is spelled correctly).
        private static Dictionary<string, Dictionary<string, List<SpellCheckWordError>>> s_cache = null;
        private static bool s_isDirty = false;

        private static string FullPath
        {
            get { return Path.Combine(Path.GetDirectoryName(Application.dataPath), RelativePath); }
        }

        public static int Count
        {
            get
            {
                EnsureLoaded();
                var count = 0;
                foreach (var words in s_cache.Values)
                {
                    count += words.Count;
                }
                return count;
            }
        }

        /// <summary>
        /// Gets JSpell's errors for a word. Returns false if the word hasn't been checked.
        /// An empty list means the word is spelled correctly.
        /// </summary>
        public static bool TryGet(string language, string word, out List<SpellCheckWordError> errors)
        {
            EnsureLoaded();
            errors = null;
            return s_cache.TryGetValue(language, out var words) && words.TryGetValue(word, out errors);
        }

        public static void Set(string language, string word, List<SpellCheckWordError> errors)
        {
            EnsureLoaded();
            if (!s_cache.TryGetValue(language, out var words))
            {
                words = new Dictionary<string, List<SpellCheckWordError>>(StringComparer.Ordinal);
                s_cache.Add(language, words);
            }
            words[word] = (errors != null && errors.Count > 0) ? errors : NoErrors;
            s_isDirty = true;
        }

        public static void Clear()
        {
            s_cache = new Dictionary<string, Dictionary<string, List<SpellCheckWordError>>>();
            s_isDirty = false;
            try
            {
                if (File.Exists(FullPath)) File.Delete(FullPath);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Dialogue System: Couldn't delete the spell check cache " + RelativePath + ": " + e.Message);
            }
        }

        /// <summary>Saves the cache if it changed.</summary>
        public static void Save()
        {
            if (!s_isDirty || s_cache == null) return;
            s_isDirty = false;
            var data = new Data() { version = Version };
            foreach (var kvp in s_cache)
            {
                var languageData = new LanguageData() { language = kvp.Key };
                foreach (var wordKvp in kvp.Value)
                {
                    languageData.words.Add(new WordData() { word = wordKvp.Key, errors = wordKvp.Value });
                }
                data.languages.Add(languageData);
            }
            try
            {
                var path = FullPath;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, JsonUtility.ToJson(data));
            }
            catch (Exception e)
            {
                Debug.LogWarning("Dialogue System: Couldn't save the spell check cache " + RelativePath + ": " + e.Message);
            }
        }

        private static void EnsureLoaded()
        {
            if (s_cache != null) return;
            s_cache = new Dictionary<string, Dictionary<string, List<SpellCheckWordError>>>();
            var path = FullPath;
            if (!File.Exists(path)) return;
            Data data = null;
            try
            {
                data = JsonUtility.FromJson<Data>(File.ReadAllText(path));
            }
            catch (Exception)
            {
                data = null; // Unreadable. Start over.
            }
            if (data == null || data.version != Version || data.languages == null) return;
            foreach (var languageData in data.languages)
            {
                if (languageData == null || string.IsNullOrEmpty(languageData.language) || languageData.words == null) continue;
                var words = new Dictionary<string, List<SpellCheckWordError>>(StringComparer.Ordinal);
                foreach (var wordData in languageData.words)
                {
                    if (wordData == null || string.IsNullOrEmpty(wordData.word)) continue;
                    words[wordData.word] = (wordData.errors != null && wordData.errors.Count > 0) ? wordData.errors : NoErrors;
                }
                s_cache[languageData.language] = words;
            }
        }

    }

}

#endif
