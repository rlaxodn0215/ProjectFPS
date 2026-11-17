// Copyright (c) Pixel Crushers. All rights reserved.

#if UNITY_2022_1_OR_NEWER

using UnityEngine;

namespace PixelCrushers.DialogueSystem.DialogueEditor
{

    /// <summary>
    /// Hidden, unsaved object that holds the UI Toolkit dialogue database editor window's
    /// template, so the template can be edited through a SerializedObject (with bindings and
    /// undo) like the database itself. The window saves the template to the database's
    /// templateJson and to EditorPrefs when it changes.
    /// </summary>
    public class DialogueDatabaseTemplateHolder : ScriptableObject
    {
        public Template template = new Template();
    }

}

#endif
