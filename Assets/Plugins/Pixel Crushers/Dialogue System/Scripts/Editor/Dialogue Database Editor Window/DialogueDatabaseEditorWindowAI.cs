// Copyright (c) Pixel Crushers. All rights reserved.

#if UNITY_2022_1_OR_NEWER

using System.Collections.Generic;
using UnityEngine.UIElements;
using UnityEditor;

namespace PixelCrushers.DialogueSystem.DialogueEditor
{

    /// <summary>
    /// Identifies a text field that an AI addon button acts on. Panels pass IDs, not objects,
    /// since they edit the database through SerializedProperties. The addon resolves the
    /// target to objects with DialogueDatabaseEditorWindow.ResolveAITarget when clicked.
    /// </summary>
    public struct AIFieldTarget
    {
        public enum Kind { Actor, Item, DialogueEntry }

        public Kind kind;
        public int assetID; // Actor or item ID.
        public int conversationID;
        public int entryID;
        public string fieldTitle;

        public static AIFieldTarget ForActor(int actorID, string fieldTitle)
        {
            return new AIFieldTarget() { kind = Kind.Actor, assetID = actorID, fieldTitle = fieldTitle };
        }

        public static AIFieldTarget ForItem(int itemID, string fieldTitle)
        {
            return new AIFieldTarget() { kind = Kind.Item, assetID = itemID, fieldTitle = fieldTitle };
        }

        public static AIFieldTarget ForEntry(int conversationID, int entryID, string fieldTitle)
        {
            return new AIFieldTarget() { kind = Kind.DialogueEntry, conversationID = conversationID, entryID = entryID, fieldTitle = fieldTitle };
        }
    }

    /// <summary>
    /// This part of the UI Toolkit dialogue database editor window has the parts of the AI
    /// addon hooks that don't depend on whether an addon is installed. The hooks themselves
    /// (CreateAI*, DrawAI*, AddAI*) are in DialogueDatabaseEditorWindowNoAI.cs, which has empty
    /// stubs, or in the addon's DialogueDatabaseEditorWindowOpenAI.cs if USE_OPENAI is defined.
    /// </summary>
    public partial class DialogueDatabaseEditorWindow
    {

        /// <summary>
        /// Resolves an AI target to the database's objects, adding the field if it doesn't
        /// exist, like the IMGUI editor. Applies pending SerializedObject changes first so the
        /// objects are current. Returns false if the asset or entry doesn't exist.
        /// </summary>
        /// <param name="asset">The actor or item, or null for a dialogue entry.</param>
        /// <param name="entry">The dialogue entry, or null for an actor or item.</param>
        public bool ResolveAITarget(AIFieldTarget target, out Asset asset, out DialogueEntry entry, out Field field)
        {
            asset = null;
            entry = null;
            field = null;
            if (database == null) return false;
            if (serializedObject != null && serializedObject.targetObject != null) serializedObject.ApplyModifiedProperties();
            List<Field> fields = null;
            switch (target.kind)
            {
                case AIFieldTarget.Kind.Actor:
                    asset = database.GetActor(target.assetID);
                    break;
                case AIFieldTarget.Kind.Item:
                    asset = database.GetItem(target.assetID);
                    break;
                case AIFieldTarget.Kind.DialogueEntry:
                    entry = database.GetDialogueEntry(target.conversationID, target.entryID);
                    break;
            }
            if (asset != null) fields = asset.fields;
            else if (entry != null) fields = entry.fields;
            if (fields == null) return false;
            if (string.IsNullOrEmpty(target.fieldTitle)) return true;
            field = Field.Lookup(fields, target.fieldTitle);
            if (field == null)
            {
                Undo.RecordObject(database, "Add Field " + target.fieldTitle);
                field = new Field(target.fieldTitle, string.Empty, FieldType.Text);
                fields.Add(field);
                EditorUtility.SetDirty(database);
                if (serializedObject != null && serializedObject.targetObject != null) serializedObject.Update();
            }
            return true;
        }

        /// <summary>
        /// Applies pending SerializedObject changes so the database's objects are current
        /// before an AI addon window reads them.
        /// </summary>
        public void ApplyPendingChanges()
        {
            if (serializedObject != null && serializedObject.targetObject != null) serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// The dialogue entries selected in the Conversations panel's node graph.
        /// </summary>
        public List<DialogueEntry> GetSelectedDialogueEntries()
        {
            var conversationsPanel = GetPanel(Category.Conversations) as ConversationsPanel;
            return (conversationsPanel != null) ? conversationsPanel.GetSelectedEntryObjects() : new List<DialogueEntry>();
        }

        /// <summary>
        /// Arranges all nodes in the conversation shown in the Conversations panel, if it's
        /// the specified conversation.
        /// </summary>
        public void ArrangeConversationNodes(int conversationID, bool vertically = true)
        {
            var conversationsPanel = GetPanel(Category.Conversations) as ConversationsPanel;
            if (conversationsPanel != null) conversationsPanel.ArrangeAllNodes(conversationID, vertically);
        }

        #region Layout Helpers

        /// <summary>
        /// Puts an AI button to the right of a single-line field. Returns the field itself if
        /// the button is null, such as when no AI addon is installed.
        /// </summary>
        public static VisualElement WithAIButton(VisualElement fieldElement, VisualElement aiButton)
        {
            if (aiButton == null) return fieldElement;
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            fieldElement.style.flexGrow = 1;
            fieldElement.style.flexShrink = 1;
            aiButton.style.flexShrink = 0;
            row.Add(fieldElement);
            row.Add(aiButton);
            return row;
        }

        /// <summary>
        /// For a label-above-text-area container whose first child is the label, puts an AI
        /// button at the right end of the label's row. Does nothing if the button is null.
        /// </summary>
        public static void AddAIButtonToLabelRow(VisualElement container, VisualElement aiButton)
        {
            if (aiButton == null || container == null || container.childCount == 0) return;
            var label = container[0] as Label;
            if (label == null)
            {
                container.Insert(0, aiButton);
                aiButton.style.alignSelf = Align.FlexEnd;
                return;
            }
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.FlexEnd;
            container.Remove(label);
            label.style.flexGrow = 1;
            aiButton.style.flexShrink = 0;
            row.Add(label);
            row.Add(aiButton);
            container.Insert(0, row);
        }

        #endregion

    }

}

#endif
