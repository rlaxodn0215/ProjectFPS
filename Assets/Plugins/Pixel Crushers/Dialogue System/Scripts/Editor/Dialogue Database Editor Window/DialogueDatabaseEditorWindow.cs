// Copyright (c) Pixel Crushers. All rights reserved.

#if UNITY_2022_1_OR_NEWER

using UnityEditor;

namespace PixelCrushers.DialogueSystem.DialogueEditor
{

    /// <summary>
    /// UI Toolkit dialogue database editor window. This part adds the menu item to Unity.
    /// The window's functionality is split into separate files that constitute partial
    /// class definitions, such as the toolbar and the category panels.
    /// </summary>
    public partial class DialogueDatabaseEditorWindow : EditorWindow
    {

        [MenuItem("Tools/Pixel Crushers/Dialogue System/Dialogue Editor (New)...", false, -3)]
        public static DialogueDatabaseEditorWindow OpenDialogueDatabaseEditorWindow()
        {
            return GetWindow<DialogueDatabaseEditorWindow>("Dialogue Editor");
        }

        /// <summary>
        /// Opens the window to a specific database.
        /// </summary>
        /// <param name="database">The database to open.</param>
        public static DialogueDatabaseEditorWindow Open(DialogueDatabase database)
        {
            var window = OpenDialogueDatabaseEditorWindow();
            if (database != null && database != window.database) window.SetDatabase(database);
            return window;
        }

        // Note: The [OnOpenAsset] handler that opens this window when double-clicking a
        // database is in Wrappers/Editor/DialogueDatabaseOpenAssetHandler.cs so it's
        // compiled from source (its signature depends on the Unity version).

    }

}

#endif
