// Copyright (c) Pixel Crushers. All rights reserved.

#if UNITY_2022_1_OR_NEWER

using System;
using UnityEngine;
using UnityEditor;

namespace PixelCrushers.DialogueSystem.DialogueEditor
{

    /// <summary>
    /// DialogueDatabaseEditorWindow's user settings, saved as JSON in a single EditorPrefs key.
    /// Change a value and call Save.
    /// </summary>
    [Serializable]
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public class DialogueDatabaseEditorSettings
    {

        private const string EditorPrefsKey = "DialogueDatabaseEditorSettings";

        public float autoBackupFrequency = DialogueEditorWindow.DefaultAutoBackupFrequency;
        public string autoBackupFolder = string.Empty;
        public bool syncOnOpen = true;
        public bool useGroupFoldouts = false;

        private static DialogueDatabaseEditorSettings s_instance = null;

        /// <summary>
        /// The settings. Loaded once, since some values are read very often.
        /// </summary>
        public static DialogueDatabaseEditorSettings instance
        {
            get
            {
                if (s_instance == null) s_instance = Load();
                return s_instance;
            }
        }

        public void Save()
        {
            EditorPrefs.SetString(EditorPrefsKey, JsonUtility.ToJson(this));
        }

        private static DialogueDatabaseEditorSettings Load()
        {
            if (!EditorPrefs.HasKey(EditorPrefsKey))
            {
                var migratedSettings = CreateFromDialogueEditorPrefs();
                migratedSettings.Save();
                return migratedSettings;
            }
            DialogueDatabaseEditorSettings settings = null;
            try
            {
                settings = JsonUtility.FromJson<DialogueDatabaseEditorSettings>(EditorPrefs.GetString(EditorPrefsKey));
            }
            catch (ArgumentException)
            {
                settings = null;
            }
            return settings ?? new DialogueDatabaseEditorSettings();
        }

        /// <summary>
        /// The first time, starts from DialogueEditorWindow's settings. It still uses its own
        /// EditorPrefs keys, so they're only read here.
        /// </summary>
        private static DialogueDatabaseEditorSettings CreateFromDialogueEditorPrefs()
        {
            return new DialogueDatabaseEditorSettings()
            {
                autoBackupFrequency = EditorPrefs.GetFloat(DialogueEditorWindow.AutoBackupKey, DialogueEditorWindow.DefaultAutoBackupFrequency),
                autoBackupFolder = EditorPrefs.GetString(DialogueEditorWindow.AutoBackupFolderKey, string.Empty),
                syncOnOpen = EditorPrefs.GetBool(DialogueEditorWindow.SyncOnOpenKey, true),
                useGroupFoldouts = EditorPrefs.GetBool(DialogueEditorVariableView.ShowGroupsPrefsKey, false)
            };
        }

    }

}

#endif
