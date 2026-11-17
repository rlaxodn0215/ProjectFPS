// Copyright (c) Pixel Crushers. All rights reserved.

using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace PixelCrushers.DialogueSystem.DialogueEditor
{

    /// <summary>
    /// Lets editor tools such as AI addons work with whichever dialogue editor is open: the
    /// UI Toolkit DialogueDatabaseEditorWindow (Unity 2022+) if it's open, otherwise the IMGUI
    /// DialogueEditorWindow. When neither is open, the Open methods open the UI Toolkit window
    /// on Unity 2022+ and the IMGUI window on older versions. Use this instead of calling
    /// DialogueEditorWindow directly.
    /// </summary>
    public static class DialogueEditorBridge
    {

#if UNITY_2022_1_OR_NEWER
        /// <summary>The open UI Toolkit dialogue database editor window, or null.</summary>
        public static DialogueDatabaseEditorWindow NewEditorWindow
        {
            get
            {
                if (!EditorWindow.HasOpenInstances<DialogueDatabaseEditorWindow>()) return null;
                var windows = Resources.FindObjectsOfTypeAll<DialogueDatabaseEditorWindow>();
                return (windows.Length > 0) ? windows[0] : null;
            }
        }

        /// <summary>Opens the UI Toolkit window if it's open, and makes it edit the database.</summary>
        private static DialogueDatabaseEditorWindow ShowNewEditorWindow(DialogueDatabase database)
        {
            var window = NewEditorWindow;
            if (window == null) return null;
            if (database != null && database != window.Database) window.SetDatabase(database);
            window.Focus();
            return window;
        }

        /// <summary>
        /// Returns the UI Toolkit window to navigate: the open one, or a newly opened one if the
        /// IMGUI editor isn't open either. Returns null if only the IMGUI editor is open.
        /// </summary>
        private static DialogueDatabaseEditorWindow ShowOrOpenNewEditorWindow(DialogueDatabase database)
        {
            var window = ShowNewEditorWindow(database);
            if (window != null || DialogueEditorWindow.instance != null) return window;
            return DialogueDatabaseEditorWindow.Open(database);
        }

        private const int MaxWaitForGUIFrames = 10;

        /// <summary>
        /// Runs action on the window once its panels exist. A newly opened window doesn't build
        /// them until CreateGUI runs, which can be after Open returns.
        /// </summary>
        private static void WhenGUICreated(DialogueDatabaseEditorWindow window, System.Action<DialogueDatabaseEditorWindow> action, int framesLeft = MaxWaitForGUIFrames)
        {
            if (window == null) return;
            if (window.IsGUICreated || framesLeft <= 0)
            {
                action(window);
                return;
            }
            EditorApplication.delayCall += () => WhenGUICreated(window, action, framesLeft - 1);
        }
#endif

        /// <summary>True if either dialogue editor window is open.</summary>
        public static bool IsEditorOpen
        {
            get
            {
#if UNITY_2022_1_OR_NEWER
                if (NewEditorWindow != null) return true;
#endif
                return DialogueEditorWindow.instance != null;
            }
        }

        /// <summary>The database that the open dialogue editor is editing, or null.</summary>
        public static DialogueDatabase GetCurrentlyEditedDatabase()
        {
#if UNITY_2022_1_OR_NEWER
            var window = NewEditorWindow;
            if (window != null) return window.Database;
#endif
            return DialogueEditorWindow.GetCurrentlyEditedDatabase();
        }

        /// <summary>Opens the dialogue editor to a dialogue entry.</summary>
        public static void OpenDialogueEntry(DialogueDatabase database, int conversationID, int dialogueEntryID)
        {
#if UNITY_2022_1_OR_NEWER
            var window = ShowOrOpenNewEditorWindow(database);
            if (window != null)
            {
                WhenGUICreated(window, w => w.GoToAsset(DialogueDatabaseEditorWindow.Category.Conversations, conversationID, dialogueEntryID));
                return;
            }
#endif
            DialogueEditorWindow.OpenDialogueEntry(database, conversationID, dialogueEntryID);
        }

        /// <summary>Opens the dialogue editor to an actor.</summary>
        public static void OpenActor(DialogueDatabase database, Actor actor)
        {
#if UNITY_2022_1_OR_NEWER
            var window = ShowOrOpenNewEditorWindow(database);
            if (window != null)
            {
                if (actor != null) WhenGUICreated(window, w => w.GoToAsset(DialogueDatabaseEditorWindow.Category.Actors, actor.id));
                return;
            }
#endif
            DialogueEditorWindow.OpenActor(database, actor);
        }

        /// <summary>Opens the dialogue editor to a quest or item.</summary>
        public static void OpenQuest(DialogueDatabase database, Item item)
        {
#if UNITY_2022_1_OR_NEWER
            var window = ShowOrOpenNewEditorWindow(database);
            if (window != null)
            {
                if (item != null) WhenGUICreated(window, w => w.GoToAsset(DialogueDatabaseEditorWindow.Category.ItemsQuests, item.id));
                return;
            }
#endif
            DialogueEditorWindow.OpenQuest(database, item);
        }

        /// <summary>
        /// Refreshes the open dialogue editors after the database was changed directly instead
        /// of through the editor, such as by an AI addon.
        /// </summary>
        public static void RefreshAfterExternalChange(DialogueDatabase database)
        {
#if UNITY_2022_1_OR_NEWER
            var window = NewEditorWindow;
            if (window != null && (database == null || database == window.Database))
            {
                window.RefreshAllPanels();
                window.Repaint();
            }
#endif
            if (DialogueEditorWindow.instance != null)
            {
                DialogueEditorWindow.instance.Reset();
                DialogueEditorWindow.instance.Repaint();
            }
        }

        /// <summary>
        /// Like RefreshAfterExternalChange, but only repaints the IMGUI editor instead of
        /// resetting it. The UI Toolkit editor needs a refresh either way to show the changes.
        /// </summary>
        public static void RepaintAfterExternalChange(DialogueDatabase database)
        {
#if UNITY_2022_1_OR_NEWER
            var window = NewEditorWindow;
            if (window != null && (database == null || database == window.Database))
            {
                window.RefreshAllPanels();
                window.Repaint();
            }
#endif
            if (DialogueEditorWindow.instance != null) DialogueEditorWindow.instance.Repaint();
        }

        /// <summary>The dialogue entries selected in the open dialogue editor's node graph.</summary>
        public static List<DialogueEntry> GetSelectedDialogueEntries()
        {
#if UNITY_2022_1_OR_NEWER
            var window = NewEditorWindow;
            if (window != null) return window.GetSelectedDialogueEntries();
#endif
            var selection = (DialogueEditorWindow.instance != null) ? DialogueEditorWindow.instance.GetMultinodeSelection() : null;
            return (selection != null && selection.nodes != null) ? new List<DialogueEntry>(selection.nodes) : new List<DialogueEntry>();
        }

        /// <summary>Arranges the nodes of the conversation shown in the open dialogue editor.</summary>
        public static void AutoArrangeNodes(DialogueDatabase database, int conversationID)
        {
#if UNITY_2022_1_OR_NEWER
            var window = NewEditorWindow;
            if (window != null)
            {
                if (database == null || database == window.Database) window.ArrangeConversationNodes(conversationID, true);
                return;
            }
#endif
            if (DialogueEditorWindow.instance != null) DialogueEditorWindow.instance.AutoArrangeNodes(true);
        }

        /// <summary>
        /// Opens the AI addon's Translate Database panel through the open dialogue editor,
        /// which supplies the localization languages. If no editor is open, opens the UI Toolkit
        /// editor on Unity 2022+ and the IMGUI editor on older versions.
        /// </summary>
        public static void OpenTranslateDatabasePanel(DialogueDatabase database)
        {
#if UNITY_2022_1_OR_NEWER
            var window = ShowOrOpenNewEditorWindow(database);
            if (window != null)
            {
                WhenGUICreated(window, w => w.OpenTranslateDatabasePanel());
                return;
            }
#endif
            DialogueEditorWindow.OpenDialogueEditorWindow();
            DialogueEditorWindow.inspectorSelection = database;
            DialogueEditorWindow.instance.OpenTranslateDatabasePanel();
        }

    }

}
