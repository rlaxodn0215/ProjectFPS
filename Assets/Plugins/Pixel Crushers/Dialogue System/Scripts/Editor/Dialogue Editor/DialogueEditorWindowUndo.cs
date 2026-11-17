// Copyright (c) Pixel Crushers. All rights reserved.

using UnityEngine;
using UnityEditor;
using System.IO;

namespace PixelCrushers.DialogueSystem.DialogueEditor
{

    /// <summary>
    /// Undo functionality for Dialogue Editor window.
    /// </summary>
    public partial class DialogueEditorWindow : EditorWindow
    {

        private void HandleUndo()
        {
            switch (toolbar.current)
            {
                case Toolbar.Tab.Actors:
                    break;
                case Toolbar.Tab.Items:
                    break;
                case Toolbar.Tab.Locations:
                    break;
                case Toolbar.Tab.Variables:
                    break;
                case Toolbar.Tab.Conversations:
                    // If undid creating a conversation, reset conversation section:
                    if (currentConversation != null)
                    {
                        if (database.GetConversation(currentConversation.id) == null)
                        {
                            if (debug) Debug.Log("Dialogue Editor: Undo create conversation");
                            ResetConversationSection();
                            Repaint();
                        }
                        else if (currentEntry != null && !currentConversation.dialogueEntries.Contains(currentEntry))
                        {
                            if (debug) Debug.Log("Dialogue Editor: Undo create dialogue entry");
                            RefreshConversation();
                            Repaint();
                        }
                    }
                    break;
            }
        }

        private void RecordUndo()
        {
            if (database == null) return;
            var eventType = Event.current.type;
            if (eventType == EventType.Repaint || eventType == EventType.Layout ||
                eventType == EventType.MouseEnterWindow || eventType == EventType.MouseLeaveWindow ||
                eventType == EventType.MouseUp || eventType == EventType.MouseDown ||
                eventType == EventType.MouseMove || eventType == EventType.MouseDrag ||
                eventType == EventType.ContextClick) return;
            {
                if (registerCompleteObjectUndo)
                {
                    Undo.RegisterCompleteObjectUndo(database, "Dialogue Database");
                }
                else
                {
                    Undo.RecordObject(database, "Dialogue Database");
                }
            }
        }

        public void SetDatabaseDirty(string reason)
        {
            if (debug)
            {
                if (database == null)
                {
                    if (string.IsNullOrEmpty(reason))
                    {
                        Debug.LogWarning("Dialogue Editor wants to mark database to be saved, but it doesn't have a reference to a database!");
                    }
                    else
                    {
                        Debug.LogWarning("Dialogue Editor wants to mark database to be saved after change '" + reason + "', but it doesn't have a reference to a database!");
                    }
                }
                else
                {
                    if (string.IsNullOrEmpty(reason))
                    {
                        Debug.Log("Dialogue Editor marking database '" + database.name + "' to be saved.", database);
                    }
                    else
                    {
                        Debug.Log("Dialogue Editor marking database '" + database.name + "' to be saved after change '" + reason + "'.", database);
                    }
                }
            }
            if (database != null)
            {
                EditorUtility.SetDirty(database);
                PrefabUtility.RecordPrefabInstancePropertyModifications(database);
            }
        }

        private void MakeAutoBackup()
        {
            DialogueDatabaseAutoBackup.MakeBackup(database, autoBackupFolder);
        }

    }

}