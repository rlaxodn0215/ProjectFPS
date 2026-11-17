// Copyright (c) Pixel Crushers. All rights reserved.

#if UNITY_2022_1_OR_NEWER

using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace PixelCrushers.DialogueSystem.DialogueEditor
{

    /// <summary>
    /// Edits a conversation's dialogue entries and links through SerializedProperty, keyed by
    /// entry ID rather than array index. Methods don't apply changes; callers call
    /// ApplyModifiedProperties so several edits form one undo step.
    /// </summary>
    public static class ConversationPropertyUtility
    {

        public const string EntriesPropertyName = "dialogueEntries";
        public const string LinksPropertyName = "outgoingLinks";
        public const string EntryGroupsPropertyName = "entryGroups";
        public const int StartEntryID = 0;
        public const float GroupHeadingHeight = 20;

        public static int GetConversationID(SerializedProperty conversationProperty)
        {
            return conversationProperty.FindPropertyRelative("id").intValue;
        }

        public static SerializedProperty GetEntries(SerializedProperty conversationProperty)
        {
            return conversationProperty.FindPropertyRelative(EntriesPropertyName);
        }

        public static int GetEntryID(SerializedProperty entryProperty)
        {
            return entryProperty.FindPropertyRelative("id").intValue;
        }

        public static int FindEntryIndex(SerializedProperty conversationProperty, int entryID)
        {
            var entries = GetEntries(conversationProperty);
            for (int i = 0; i < entries.arraySize; i++)
            {
                if (GetEntryID(entries.GetArrayElementAtIndex(i)) == entryID) return i;
            }
            return -1;
        }

        public static SerializedProperty FindEntry(SerializedProperty conversationProperty, int entryID)
        {
            var index = FindEntryIndex(conversationProperty, entryID);
            return (index >= 0) ? GetEntries(conversationProperty).GetArrayElementAtIndex(index) : null;
        }

        /// <summary>
        /// One higher than the highest entry ID, like DialogueEditorWindow.GetNextDialogueEntryID.
        /// </summary>
        public static int GetNextEntryID(SerializedProperty conversationProperty)
        {
            var highestID = -1;
            var entries = GetEntries(conversationProperty);
            for (int i = 0; i < entries.arraySize; i++)
            {
                highestID = Mathf.Max(highestID, GetEntryID(entries.GetArrayElementAtIndex(i)));
            }
            return highestID + 1;
        }

        /// <summary>
        /// Appends a new entry built from the template. The whole entry is assigned with
        /// boxedValue, so nothing is inherited from the array element it was copied from.
        /// </summary>
        public static SerializedProperty AddEntry(SerializedProperty conversationProperty, Template template, string title,
            Rect canvasRect, int actorID, int conversantID)
        {
            var conversationID = GetConversationID(conversationProperty);
            var entry = template.CreateDialogueEntry(GetNextEntryID(conversationProperty), conversationID, title ?? string.Empty);
            entry.ActorID = actorID;
            entry.ConversantID = conversantID;
            entry.canvasRect = canvasRect;
            var entries = GetEntries(conversationProperty);
            var index = entries.arraySize;
            entries.InsertArrayElementAtIndex(index);
            var entryProperty = entries.GetArrayElementAtIndex(index);
            entryProperty.boxedValue = entry;
            return entryProperty;
        }

        /// <summary>
        /// Appends a copy of an entry with the next ID, below the original and keeping its
        /// outgoing links, like DialogueEditorWindow.DuplicateEntry.
        /// </summary>
        public static SerializedProperty AddEntryCopy(SerializedProperty conversationProperty, DialogueEntry source)
        {
            var copy = new DialogueEntry(source);
            copy.id = GetNextEntryID(conversationProperty);
            foreach (var link in copy.outgoingLinks)
            {
                link.originDialogueID = copy.id;
            }
            copy.canvasRect.x = source.canvasRect.x + 10;
            copy.canvasRect.y = source.canvasRect.yMax + 20;
            GiveCopyItsOwnSceneEvent(copy);
            var entries = GetEntries(conversationProperty);
            var index = entries.arraySize;
            entries.InsertArrayElementAtIndex(index);
            var entryProperty = entries.GetArrayElementAtIndex(index);
            entryProperty.boxedValue = copy;
            return entryProperty;
        }

        /// <summary>
        /// A copy can't share the original's scene event, so give it a new, empty one, like
        /// DialogueEditorWindow.DuplicateEntrySceneEventIfPresent. The new event goes in the
        /// same scene as the original. If the original isn't in a loaded scene, the copy has none.
        /// </summary>
        private static void GiveCopyItsOwnSceneEvent(DialogueEntry copy)
        {
            var guidField = Field.Lookup(copy.fields, DialogueSystemFields.SceneEventGuidFieldName);
            if (guidField == null || string.IsNullOrEmpty(guidField.value)) return;
            int index;
            var instance = EntryEventsElement.FindSceneEvents(guidField.value, out index);
            if (instance == null)
            {
                copy.fields.Remove(guidField);
                return;
            }
            Undo.RecordObject(instance, "Duplicate Scene Event");
            string newGuid;
            DialogueSystemSceneEvents.AddNewDialogueEntrySceneEvent(out newGuid, instance);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(instance.gameObject.scene);
            guidField.value = newGuid;
        }

        /// <summary>
        /// Deletes entries and every link in the conversation that points to them. The START
        /// entry is never deleted. Returns the IDs that were deleted.
        /// </summary>
        public static List<int> DeleteEntries(SerializedProperty conversationProperty, ICollection<int> entryIDs)
        {
            var deleted = new List<int>();
            var entries = GetEntries(conversationProperty);
            for (int i = entries.arraySize - 1; i >= 0; i--)
            {
                var id = GetEntryID(entries.GetArrayElementAtIndex(i));
                if (id == StartEntryID || !entryIDs.Contains(id)) continue;
                entries.DeleteArrayElementAtIndex(i);
                deleted.Add(id);
            }
            if (deleted.Count > 0) RemoveLinksTo(conversationProperty, deleted);
            return deleted;
        }

        /// <summary>
        /// Removes links in this conversation whose destination is one of the given entries in it.
        /// </summary>
        public static void RemoveLinksTo(SerializedProperty conversationProperty, ICollection<int> entryIDs)
        {
            var conversationID = GetConversationID(conversationProperty);
            var entries = GetEntries(conversationProperty);
            for (int i = 0; i < entries.arraySize; i++)
            {
                var links = entries.GetArrayElementAtIndex(i).FindPropertyRelative(LinksPropertyName);
                for (int j = links.arraySize - 1; j >= 0; j--)
                {
                    var link = links.GetArrayElementAtIndex(j);
                    if (link.FindPropertyRelative("destinationConversationID").intValue == conversationID &&
                        entryIDs.Contains(link.FindPropertyRelative("destinationDialogueID").intValue))
                    {
                        links.DeleteArrayElementAtIndex(j);
                    }
                }
            }
        }

        public static int FindLinkIndex(SerializedProperty entryProperty, int destinationConversationID, int destinationEntryID)
        {
            var links = entryProperty.FindPropertyRelative(LinksPropertyName);
            for (int i = 0; i < links.arraySize; i++)
            {
                var link = links.GetArrayElementAtIndex(i);
                if (link.FindPropertyRelative("destinationConversationID").intValue == destinationConversationID &&
                    link.FindPropertyRelative("destinationDialogueID").intValue == destinationEntryID)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// Adds a link from an entry unless one to the same destination exists. Returns the
        /// new link, or null if it was a duplicate.
        /// </summary>
        public static SerializedProperty AddLink(SerializedProperty entryProperty, int destinationConversationID, int destinationEntryID)
        {
            if (FindLinkIndex(entryProperty, destinationConversationID, destinationEntryID) >= 0) return null;
            var links = entryProperty.FindPropertyRelative(LinksPropertyName);
            var index = links.arraySize;
            links.InsertArrayElementAtIndex(index);
            var linkProperty = links.GetArrayElementAtIndex(index);
            linkProperty.boxedValue = new Link
            {
                originConversationID = entryProperty.FindPropertyRelative("conversationID").intValue,
                originDialogueID = GetEntryID(entryProperty),
                destinationConversationID = destinationConversationID,
                destinationDialogueID = destinationEntryID,
                isConnector = false,
                priority = ConditionPriority.Normal
            };
            return linkProperty;
        }

        public static bool RemoveLink(SerializedProperty entryProperty, int destinationConversationID, int destinationEntryID)
        {
            var index = FindLinkIndex(entryProperty, destinationConversationID, destinationEntryID);
            if (index < 0) return false;
            entryProperty.FindPropertyRelative(LinksPropertyName).DeleteArrayElementAtIndex(index);
            return true;
        }

        /// <summary>
        /// Changes which entries a link within the conversation connects, keeping its priority.
        /// A link that keeps its origin also keeps its place in the origin's link order. Does
        /// nothing if the link doesn't exist or the new origin already links to the new
        /// destination. Returns true if the link changed.
        /// </summary>
        public static bool MoveLink(SerializedProperty conversationProperty, int oldOriginID, int oldDestinationID, int newOriginID, int newDestinationID)
        {
            var conversationID = GetConversationID(conversationProperty);
            var oldOrigin = FindEntry(conversationProperty, oldOriginID);
            var newOrigin = FindEntry(conversationProperty, newOriginID);
            if (oldOrigin == null || newOrigin == null || FindEntry(conversationProperty, newDestinationID) == null) return false;
            var linkIndex = FindLinkIndex(oldOrigin, conversationID, oldDestinationID);
            if (linkIndex < 0 || FindLinkIndex(newOrigin, conversationID, newDestinationID) >= 0) return false;
            var link = oldOrigin.FindPropertyRelative(LinksPropertyName).GetArrayElementAtIndex(linkIndex);
            if (oldOriginID == newOriginID)
            {
                link.FindPropertyRelative("destinationDialogueID").intValue = newDestinationID;
                return true;
            }
            var priority = link.FindPropertyRelative("priority").intValue;
            RemoveLink(oldOrigin, conversationID, oldDestinationID);
            AddLink(newOrigin, conversationID, newDestinationID).FindPropertyRelative("priority").intValue = priority;
            return true;
        }

        /// <summary>
        /// Moves an entry's canvas position, keeping the rect's size for the IMGUI editor.
        /// </summary>
        public static void SetEntryPosition(SerializedProperty entryProperty, Vector2 position)
        {
            var rectProperty = entryProperty.FindPropertyRelative("canvasRect");
            var rect = rectProperty.rectValue;
            rect.position = position;
            rectProperty.rectValue = rect;
        }

        public static SerializedProperty GetEntryGroup(SerializedProperty conversationProperty, int groupIndex)
        {
            var groups = conversationProperty.FindPropertyRelative(EntryGroupsPropertyName);
            return (0 <= groupIndex && groupIndex < groups.arraySize) ? groups.GetArrayElementAtIndex(groupIndex) : null;
        }

        /// <summary>Appends a node group and returns its index.</summary>
        public static int AddEntryGroup(SerializedProperty conversationProperty, string name, Rect rect)
        {
            var groups = conversationProperty.FindPropertyRelative(EntryGroupsPropertyName);
            var index = groups.arraySize;
            groups.InsertArrayElementAtIndex(index);
            groups.GetArrayElementAtIndex(index).boxedValue = new EntryGroup(name, rect);
            return index;
        }

        public static void DeleteEntryGroups(SerializedProperty conversationProperty, IEnumerable<int> groupIndices)
        {
            var groups = conversationProperty.FindPropertyRelative(EntryGroupsPropertyName);
            var indices = new List<int>(groupIndices);
            indices.Sort();
            for (int i = indices.Count - 1; i >= 0; i--) // Last first, so earlier indices stay valid.
            {
                groups.DeleteArrayElementAtIndex(indices[i]);
            }
        }

        public static void SetEntryGroupRect(SerializedProperty conversationProperty, int groupIndex, Rect rect)
        {
            var groupProperty = GetEntryGroup(conversationProperty, groupIndex);
            if (groupProperty != null) groupProperty.FindPropertyRelative("rect").rectValue = rect;
        }

        /// <summary>
        /// Builds a new conversation with a START entry, like DialogueEditorWindow.InitializeConversation.
        /// </summary>
        public static Conversation CreateConversation(Template template, int id, string title, int actorID, int conversantID)
        {
            var conversation = template.CreateConversation(id, title);
            conversation.ActorID = actorID;
            conversation.ConversantID = conversantID;
            var startEntry = template.CreateDialogueEntry(StartEntryID, id, "START");
            startEntry.isRoot = true;
            startEntry.Sequence = "None()";
            startEntry.ActorID = actorID;
            startEntry.ConversantID = conversantID;
            startEntry.canvasRect = new Rect(DialogueEntry.CanvasRectWidth, DialogueEntry.CanvasRectHeight, DialogueEntry.CanvasRectWidth, DialogueEntry.CanvasRectHeight);
            conversation.dialogueEntries.Add(startEntry);
            return conversation;
        }

        /// <summary>
        /// Gives a conversation object a new ID. Its entries and its links within the conversation
        /// follow; links to other conversations are left alone.
        /// </summary>
        public static void SetConversationID(Conversation conversation, int newID)
        {
            var oldID = conversation.id;
            conversation.id = newID;
            foreach (var entry in conversation.dialogueEntries)
            {
                entry.conversationID = newID;
                foreach (var link in entry.outgoingLinks)
                {
                    if (link.originConversationID == oldID) link.originConversationID = newID;
                    if (link.destinationConversationID == oldID) link.destinationConversationID = newID;
                }
            }
        }

    }

}

#endif
