// Copyright (c) Pixel Crushers. All rights reserved.

#if UNITY_2022_1_OR_NEWER

using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;

namespace PixelCrushers.DialogueSystem.DialogueEditor
{

    /// <summary>
    /// Dockable window that spell checks one conversation, so the node graph stays visible
    /// while fixing words. Opened from the Conversations panel's menu. Fixes go through the
    /// Dialogue Editor window that shows the conversation's database.
    /// </summary>
    public class SpellCheckWindow : EditorWindow
    {

        private const float MinWidth = 480;
        private const float MinHeight = 200;

        [SerializeField] private DialogueDatabase database = null;
        [SerializeField] private int conversationID = -1;

        private Label conversationLabel = null;
        private HelpBox noEditorHelpBox = null;
        private SpellCheckView view = null;
        [System.NonSerialized] private bool startWhenCreated = false;

        /// <summary>
        /// Opens the window to a conversation in the dialogue editor window's database. If
        /// autoStart is true and an API key is set, starts checking right away.
        /// </summary>
        public static SpellCheckWindow Open(DialogueDatabaseEditorWindow editorWindow, int conversationID, bool autoStart)
        {
            var window = GetWindow<SpellCheckWindow>("Spell Check");
            window.minSize = new Vector2(MinWidth, MinHeight);
            var isDifferent = window.database != editorWindow.Database || window.conversationID != conversationID;
            window.database = editorWindow.Database;
            window.conversationID = conversationID;
            window.RefreshHeader();
            var start = autoStart && SpellCheckSettings.instance.HasApiKey;
            if (window.view != null)
            {
                if (isDifferent) window.view.ClearResults();
                if (start) window.view.StartCheck();
            }
            else
            {
                window.startWhenCreated = start; // CreateGUI hasn't run yet.
            }
            return window;
        }

        private void OnEnable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        private void OnFocus()
        {
            RefreshHeader();
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;
            DialogueDatabaseEditorTheme.TrackThemeChanges(root, () => { if (view != null) view.RefreshTheme(); });
            root.style.paddingLeft = 8;
            root.style.paddingRight = 8;
            root.style.paddingTop = 8;
            root.style.paddingBottom = 8;

            conversationLabel = new Label();
            conversationLabel.style.fontSize = 14;
            conversationLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            conversationLabel.style.marginBottom = 4;
            root.Add(conversationLabel);

            noEditorHelpBox = new HelpBox(string.Empty, UnityEngine.UIElements.HelpBoxMessageType.Warning);
            root.Add(noEditorHelpBox);

            view = new SpellCheckView(FindEditorWindow, () => conversationID, () => "this conversation");
            root.Add(view);
            RefreshHeader();
            if (startWhenCreated)
            {
                startWhenCreated = false;
                view.StartCheck();
            }
        }

        /// <summary>The open Dialogue Editor window that shows this window's database, or null.</summary>
        private DialogueDatabaseEditorWindow FindEditorWindow()
        {
            if (database == null) return null;
            foreach (var editorWindow in Resources.FindObjectsOfTypeAll<DialogueDatabaseEditorWindow>())
            {
                if (editorWindow != null && editorWindow.Database == database) return editorWindow;
            }
            return null;
        }

        private void RefreshHeader()
        {
            if (conversationLabel == null) return;
            var conversation = (database != null) ? database.GetConversation(conversationID) : null;
            var title = (conversation != null) ? conversation.Title : "(None)";
            titleContent = new GUIContent("Spell Check: " + title);
            conversationLabel.text = "Conversation: " + title;
            string problem = null;
            if (database == null || conversation == null) problem = "Choose Spell Check Conversation from the Conversations panel's Menu in the Dialogue Editor.";
            else if (FindEditorWindow() == null) problem = "Open the database '" + database.name + "' in the Dialogue Editor to check and fix this conversation.";
            noEditorHelpBox.text = problem ?? string.Empty;
            noEditorHelpBox.style.display = (problem != null) ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void OnUndoRedo()
        {
            RefreshHeader();
            if (view != null) view.MarkOutOfDate();
        }

    }

}

#endif
