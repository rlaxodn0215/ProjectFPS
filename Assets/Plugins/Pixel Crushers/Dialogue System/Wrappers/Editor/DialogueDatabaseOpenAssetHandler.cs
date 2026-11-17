// Copyright (c) Pixel Crushers. All rights reserved.

#if UNITY_2022_1_OR_NEWER

using UnityEditor;
using UnityEditor.Callbacks;

namespace PixelCrushers.DialogueSystem.Wrappers
{

    /// <summary>
    /// Opens the Dialogue Editor when a dialogue database asset is opened, such as by
    /// double-clicking it or pressing Enter on it in the Project view.
    /// This handler lives in Wrappers (shipped as source) instead of the editor DLL
    /// because the [OnOpenAsset] signature depends on the Unity version, so it must
    /// be compiled by the Unity version that's actually running.
    /// </summary>
    public static class DialogueDatabaseOpenAssetHandler
    {

        [OnOpenAsset]
#if UNITY_6000_3_OR_NEWER
        private static bool OnOpenAsset(UnityEngine.EntityId entityId, int line)
        {
            var openedDatabase = EditorUtility.EntityIdToObject(entityId) as DialogueDatabase;
#else
        private static bool OnOpenAsset(int instanceID, int line)
        {
            var openedDatabase = EditorUtility.InstanceIDToObject(instanceID) as DialogueDatabase;
#endif
            if (openedDatabase == null) return false;
            PixelCrushers.DialogueSystem.DialogueEditor.DialogueDatabaseEditorWindow.Open(openedDatabase);
            return true;
        }

    }

}

#endif
