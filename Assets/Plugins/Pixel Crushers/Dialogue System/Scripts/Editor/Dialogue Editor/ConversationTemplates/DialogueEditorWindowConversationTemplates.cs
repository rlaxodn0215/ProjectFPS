// Copyright (c) Pixel Crushers. All rights reserved.

using UnityEngine;
using UnityEditor;
using System;
using System.IO;

namespace PixelCrushers.DialogueSystem.DialogueEditor
{

    /// <summary>
    /// This part of the Dialogue Editor window handles conversation templates.
    /// </summary>
    public partial class DialogueEditorWindow
    {

        private string lastConversationTemplateFilename;

        private void SaveConversationTemplate()
        {
            var directory = string.IsNullOrEmpty(lastConversationTemplateFilename) ? Application.dataPath : Path.GetDirectoryName(lastConversationTemplateFilename);
            var filename = EditorUtility.SaveFilePanel("Save Conversation Template", directory, "ConversationTemplate.json", "json");
            if (string.IsNullOrEmpty(filename)) return;
            lastConversationTemplateFilename = filename;
            try
            {
                File.WriteAllText(filename, JsonUtility.ToJson(currentConversation));
                Debug.Log("Dialogue System: Saved conversation template " + filename);
            }
            catch (Exception e)
            {
                Debug.LogError("Dialogue System: Unable to save conversation template " + filename + ": " + e.Message);
            }
        }

        private void CreateConversationFromTemplate()
        {
            var directory = string.IsNullOrEmpty(lastConversationTemplateFilename) ? Application.dataPath : Path.GetDirectoryName(lastConversationTemplateFilename);
            var filename = EditorUtility.OpenFilePanel("Select Conversation Template", directory, "json");
            if (string.IsNullOrEmpty(filename)) return;
            lastConversationTemplateFilename = filename;
            try
            {
                CreateConversationFromJson(File.ReadAllText(filename));
            }
            catch (Exception e)
            {
                Debug.LogError("Dialogue System: Unable to load conversation template: " + filename + ": " + e.Message);
                return;
            }
        }

        private void CreateConversationFromJson(string json)
        {
            var newConversationID = template.GetNextConversationID(database);

            var conversation = JsonUtility.FromJson<Conversation>(json);
            if (conversation == null)
            {
                Debug.LogError("Dialogue System: Unable to read conversation template");
                return;
            }
            Undo.RegisterCompleteObjectUndo(database, "New Conversation");
            database.conversations.Add(conversation);

            // Set new conversation title:
            int safeguard = 1;
            var oldConversationTitle = conversation.Title;
            string proposedTitle;
            do
            {
                proposedTitle = oldConversationTitle + " " + safeguard;
            }
            while (database.GetConversation(proposedTitle) != null && safeguard++ < 99);
            conversation.Title = proposedTitle;

            // Use new conversation ID:
            var oldConversationID = conversation.id;
            conversation.id = newConversationID;
            foreach (var entry in conversation.dialogueEntries)
            {
                entry.conversationID = newConversationID;
                foreach (var link in entry.outgoingLinks)
                {
                    if (link.originConversationID == oldConversationID) link.originConversationID = newConversationID;
                    if (link.destinationConversationID == oldConversationID) link.destinationConversationID = newConversationID;
                }
            }

            SetDatabaseDirty("Add New Conversation");
            Debug.Log("Dialogue System: Create conversation '" + proposedTitle + "'");
            OpenConversation(conversation);
            conversationIndex = -1; // Update dropdown.
            ValidateConversationMenuTitleIndex();
            Repaint();
        }

        private void CreateQuestConversationFromTemplate()
        {
            CreateConversationFromJson(BuiltInConversationTemplates.QuestConversationJson);
        }

    }

}
