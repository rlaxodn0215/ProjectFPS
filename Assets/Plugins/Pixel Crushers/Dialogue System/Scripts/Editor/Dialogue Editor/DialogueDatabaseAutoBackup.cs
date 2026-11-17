// Copyright (c) Pixel Crushers. All rights reserved.

using System.IO;
using UnityEngine;
using UnityEditor;

namespace PixelCrushers.DialogueSystem.DialogueEditor
{

    /// <summary>
    /// Makes auto-backups of dialogue databases. Used by the dialogue editor windows.
    /// </summary>
    public static class DialogueDatabaseAutoBackup
    {

        private const string BackupSuffix = " (Auto-Backup).asset";

        /// <summary>
        /// Writes the database's current state, including unsaved changes, to a backup asset
        /// without saving the database itself. AssetDatabase.CopyAsset isn't used because it
        /// saves a dirty source asset first.
        /// </summary>
        /// <param name="database">The database to back up.</param>
        /// <param name="folder">Folder for the backup, or blank for the database's folder.</param>
        public static void MakeBackup(DialogueDatabase database, string folder)
        {
            if (database == null) return;
            var path = AssetDatabase.GetAssetPath(database);
            if (string.IsNullOrEmpty(path)) return; // Not an asset, such as one created at runtime.
            if (path.EndsWith("(Auto-Backup).asset"))
            {
                Debug.Log("Dialogue Editor: Not creating an auto-backup. You're already editing the auto-backup file.");
                return;
            }
            var backupFolder = string.IsNullOrEmpty(folder) ? Path.GetDirectoryName(path).Replace('\\', '/') : folder;
            if (!AssetDatabase.IsValidFolder(backupFolder))
            {
                Debug.LogWarning($"Dialogue Editor: Not creating an auto-backup. Auto Backup Folder '{backupFolder}' isn't a folder in your project.");
                return;
            }
            var backupName = Path.GetFileNameWithoutExtension(path) + BackupSuffix;
            try
            {
                EditorUtility.DisplayProgressBar("Dialogue Editor Auto-Backup", "Creating auto-backup " + backupName, 0);
                var snapshot = Object.Instantiate(database);
                var backupPath = backupFolder + "/" + backupName;
                AssetDatabase.DeleteAsset(backupPath);
                AssetDatabase.CreateAsset(snapshot, backupPath);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

    }

}
