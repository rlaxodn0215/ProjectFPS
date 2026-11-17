// Copyright (c) Pixel Crushers. All rights reserved.

#if UNITY_2022_1_OR_NEWER

using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PixelCrushers.DialogueSystem.DialogueEditor
{

    /// <summary>
    /// Prepares a copy of a database and its template for Chat Mapper XML export, like the
    /// IMGUI editor did before exporting: repairs invalid references, syncs the template with
    /// the assets' fields, and lists the template fields that a Chat Mapper project needs.
    /// It changes the database and template it's given, so give it copies.
    /// </summary>
    public static class ChatMapperExportPreparer
    {

        public const string CanvasRectFieldTitle = "canvasRect";

        /// <summary>
        /// Repairs the database copy, then syncs it with the template copy so every asset has
        /// every template field, in template order.
        /// </summary>
        public static void Prepare(DialogueDatabase database, Template template, bool includeCanvasRect)
        {
            if (database == null || template == null) return;
            if (includeCanvasRect && template.dialogueEntryFields.Find(f => string.Equals(f.title, CanvasRectFieldTitle)) == null)
            {
                template.dialogueEntryFields.Add(new Field(CanvasRectFieldTitle, string.Empty, FieldType.Text));
            }
            Validate(database, template);
            TemplatePanel.SyncAssetsAndTemplate(template, database);
        }

        /// <summary>
        /// Makes references valid for Chat Mapper: conversation item and location fields,
        /// conversation and entry actors, and link origins. Marks the first link to each entry
        /// in a conversation's tree as its non-connector link.
        /// </summary>
        public static void Validate(DialogueDatabase database, Template template)
        {
            if (database == null || database.actors == null || database.conversations == null) return;
            if (database.actors.Count < 1) database.actors.Add(template.CreateActor(1, "Player", true));

            // Nodes that are already connected (link's isConnector is false):
            var alreadyConnected = new HashSet<string>();

            foreach (var conversation in database.conversations)
            {
                foreach (var field in conversation.fields)
                {
                    if (field.type == FieldType.Location && database.GetLocation(Tools.StringToInt(field.value)) == null)
                    {
                        if (database.locations.Count < 1) database.locations.Add(template.CreateLocation(1, "Nowhere"));
                        field.value = database.locations[0].id.ToString();
                    }
                    else if (field.type == FieldType.Item && database.GetItem(Tools.StringToInt(field.value)) == null)
                    {
                        if (database.items.Count < 1) database.items.Add(template.CreateItem(1, "No Item"));
                        field.value = database.items[0].id.ToString();
                    }
                }

                var conversationActorID = (database.GetActor(conversation.ActorID) != null) ? conversation.ActorID : database.actors[0].id;
                if (conversationActorID != conversation.ActorID) conversation.ActorID = conversationActorID;
                var conversationConversantID = (database.GetActor(conversation.ConversantID) != null) ? conversation.ConversantID : database.actors[0].id;
                if (conversationConversantID != conversation.ConversantID) conversation.ConversantID = conversationConversantID;

                foreach (var entry in conversation.dialogueEntries)
                {
                    if (database.GetActor(entry.ActorID) == null)
                    {
                        entry.ActorID = (entry.ConversantID == conversationConversantID) ? conversationActorID : conversationConversantID;
                    }
                    if (database.GetActor(entry.ConversantID) == null)
                    {
                        entry.ConversantID = (entry.ActorID == conversationConversantID) ? conversationActorID : conversationConversantID;
                    }
                    foreach (var link in entry.outgoingLinks)
                    {
                        link.originConversationID = conversation.id;
                        link.originDialogueID = entry.id;
                        link.isConnector = true;
                    }
                }

                // Traverse the tree, making the first link to each entry its non-connector link:
                var firstEntry = conversation.GetFirstDialogueEntry();
                if (firstEntry != null) AssignConnectors(firstEntry, conversation, alreadyConnected, new HashSet<int>(), 0);
            }
        }

        private static void AssignConnectors(DialogueEntry entry, Conversation conversation, HashSet<string> alreadyConnected, HashSet<int> entriesVisited, int level)
        {
            if (level > 10000) return; // Prevents runaway recursion.

            foreach (var link in entry.outgoingLinks)
            {
                if (link.originConversationID == link.destinationConversationID)
                {
                    var destination = string.Format("{0}.{1}", link.destinationConversationID, link.destinationDialogueID);
                    if (alreadyConnected.Contains(destination))
                    {
                        link.isConnector = true;
                    }
                    else
                    {
                        link.isConnector = false;
                        alreadyConnected.Add(destination);
                    }
                }
            }

            foreach (var link in entry.outgoingLinks)
            {
                if (link.originConversationID == link.destinationConversationID && !entriesVisited.Contains(link.destinationDialogueID))
                {
                    entriesVisited.Add(link.destinationDialogueID);
                    var childEntry = conversation.GetDialogueEntry(link.destinationDialogueID);
                    if (childEntry != null) AssignConnectors(childEntry, conversation, alreadyConnected, entriesVisited, level + 1);
                }
            }
        }

        /// <summary>The path of the template field list written next to an exported XML file.</summary>
        public static string GetTemplateFieldListPath(string xmlPath)
        {
            return Path.Combine(Path.GetDirectoryName(xmlPath) ?? string.Empty, Path.GetFileNameWithoutExtension(xmlPath) + "_Template.txt");
        }

        /// <summary>
        /// Writes the fields, in order, that the Chat Mapper project's template must have to
        /// import the exported XML. Returns false and logs an error if the file can't be written.
        /// </summary>
        public static bool WriteTemplateFieldList(string xmlPath, string templatePath, Template template)
        {
            try
            {
                using (var file = new StreamWriter(templatePath))
                {
                    file.WriteLine("Required Chat Mapper Template Fields for: " + xmlPath);
                    file.WriteLine("\nACTORS (in this order):");
                    WriteFields(file, template.actorFields);
                    file.WriteLine("\nITEMS (in this order):");
                    WriteFields(file, template.itemFields);
                    file.WriteLine("\nLOCATIONS (in this order):");
                    WriteFields(file, template.locationFields);
                    file.WriteLine("\nUSER VARIABLES (in this order):");
                    WriteFields(file, template.variableFields);
                    file.WriteLine("\nCONVERSATIONS (in this order):");
                    WriteFields(file, template.conversationFields);
                    file.WriteLine("\nDIALOGUE NODES (in this order):");
                    WriteFields(file, template.dialogueEntryFields);
                }
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError(string.Format("{0}: Error writing Chat Mapper template file {1}: {2}", DialogueDebug.Prefix, templatePath, e.Message));
                return false;
            }
        }

        private static void WriteFields(StreamWriter file, List<Field> fields)
        {
            for (int i = 0; i < fields.Count; i++)
            {
                file.WriteLine(string.Format("[{0}] {1}: {2}", i + 1, fields[i].title, fields[i].type.ToString()));
            }
        }

    }

}

#endif
