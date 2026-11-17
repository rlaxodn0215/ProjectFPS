// Copyright (c) Pixel Crushers. All rights reserved.

#if UNITY_2022_1_OR_NEWER

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor;

namespace PixelCrushers.DialogueSystem.DialogueEditor
{

    /// <summary>
    /// Spell check user settings, saved as JSON in a single EditorPrefs key so the API key
    /// stays on this machine and out of the project. Change a value and call Save.
    /// </summary>
    [Serializable]
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public class SpellCheckSettings
    {

        private const string EditorPrefsKey = "PixelCrushers.DialogueSystem.SpellCheckSettings";

        public const string DefaultAssetFieldTitles = "Display Name, Description, Success Description, Failure Description, Entry #";

        /// <summary>RapidAPI key for the JSpell Checker API.</summary>
        public string apiKey = string.Empty;

        /// <summary>JSpell language code of the main (non-localized) text.</summary>
        public string mainLanguage = "enUS";

        /// <summary>Also check localized fields whose language JSpell supports.</summary>
        public bool checkLocalizedFields = true;

        /// <summary>Don't flag words in actor, item, quest, location and variable names or conversation titles.</summary>
        public bool ignoreDatabaseNames = true;

        /// <summary>Flag repeated words, such as "the the".</summary>
        public bool checkDoubledWords = true;

        public bool ignoreWordsWithNumbers = true;

        /// <summary>Ignore words in all capitals, such as acronyms.</summary>
        public bool ignoreAllCapsWords = true;

        /// <summary>Ignore words that start with a capital letter, such as names.</summary>
        public bool ignoreCapitalizedWords = false;

        /// <summary>
        /// Comma-separated titles of actor, item, quest and location fields to check. In a
        /// title, # matches a number and * matches anything. Dialogue entries always check
        /// Dialogue Text and Menu Text.
        /// </summary>
        public string assetFieldTitles = DefaultAssetFieldTitles;

        public bool settingsFoldout = false;

        [NonSerialized] private string cachedTitlesSource = null;
        [NonSerialized] private List<Regex> cachedTitlePatterns = null;

        private static SpellCheckSettings s_instance = null;

        public static SpellCheckSettings instance
        {
            get
            {
                if (s_instance == null) s_instance = Load();
                return s_instance;
            }
        }

        public bool HasApiKey { get { return !string.IsNullOrEmpty(apiKey) && !string.IsNullOrEmpty(apiKey.Trim()); } }

        public void Save()
        {
            EditorPrefs.SetString(EditorPrefsKey, JsonUtility.ToJson(this));
        }

        private static SpellCheckSettings Load()
        {
            SpellCheckSettings settings = null;
            if (EditorPrefs.HasKey(EditorPrefsKey))
            {
                try
                {
                    settings = JsonUtility.FromJson<SpellCheckSettings>(EditorPrefs.GetString(EditorPrefsKey));
                }
                catch (ArgumentException)
                {
                    settings = null;
                }
            }
            if (settings == null) settings = new SpellCheckSettings();
            if (Array.IndexOf(JSpellClient.LanguageCodes, settings.mainLanguage) < 0) settings.mainLanguage = "enUS";
            if (settings.assetFieldTitles == null) settings.assetFieldTitles = DefaultAssetFieldTitles;
            return settings;
        }

        /// <summary>True if an actor, item, quest or location field with this title should be checked.</summary>
        public bool IsAssetFieldTitle(string title)
        {
            if (string.IsNullOrEmpty(title)) return false;
            if (cachedTitlePatterns == null || cachedTitlesSource != assetFieldTitles)
            {
                cachedTitlesSource = assetFieldTitles;
                cachedTitlePatterns = new List<Regex>();
                foreach (var part in (assetFieldTitles ?? string.Empty).Split(','))
                {
                    var pattern = part.Trim();
                    if (pattern.Length == 0) continue;
                    var regex = "^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\#", "[0-9]+").Replace("#", "[0-9]+") + "$";
                    cachedTitlePatterns.Add(new Regex(regex));
                }
            }
            foreach (var regex in cachedTitlePatterns)
            {
                if (regex.IsMatch(title)) return true;
            }
            return false;
        }

    }

}

#endif
