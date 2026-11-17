// Copyright (c) Pixel Crushers. All rights reserved.

using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using PixelCrushers.DialogueSystem.DialogueEditor;

namespace PixelCrushers.DialogueSystem
{

    [CustomPropertyDrawer(typeof(DialogueEntryPopupAttribute))]
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public class DialogueEntryPopupDrawer : PropertyDrawer
    {

        private DialogueDatabase database = null;
        private Conversation conversation = null;
        private string[] entryTexts = null;
        private Dictionary<int, int> idToIndex = new Dictionary<int, int>();
        private Dictionary<int, int> indexToId = new Dictionary<int, int>();
        private bool usePicker = true;
        private bool showFilter = false;
        private string filter = string.Empty;
        private static string currentConversationTitle;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            showFilter = (attribute as DialogueEntryPopupAttribute).showFilter;
            return base.GetPropertyHeight(property, label) +
                (showFilter ? EditorGUIUtility.singleLineHeight : 0);
        }

        public override void OnGUI(Rect position, SerializedProperty prop, GUIContent label)
        {
            // Set up property drawer:
            EditorGUI.BeginProperty(position, GUIContent.none, prop);
            position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

            // Show database field if specified:
            if (EditorTools.selectedDatabase == null) EditorTools.SetInitialDatabaseIfNull();
            showFilter = (attribute as DialogueEntryPopupAttribute).showFilter;

            // Set up titles array:
            var conversationProp = FindConversationProperty(prop);
            if (conversationProp != null && conversationProp.stringValue != currentConversationTitle)
            {
                currentConversationTitle = conversationProp.stringValue;
                entryTexts = null;
            }
            if (database != EditorTools.selectedDatabase) entryTexts = null;

            // Filter:
            if (showFilter)
            {
                var filterLabelWidth = 48;
                var openButtonWidth = 48;
                EditorGUI.LabelField(new Rect(position.x, position.y, filterLabelWidth, EditorGUIUtility.singleLineHeight), "Filter:");
                EditorGUI.BeginChangeCheck();
                filter = EditorGUI.TextField(new Rect(position.x + filterLabelWidth, position.y, position.width - filterLabelWidth - openButtonWidth, EditorGUIUtility.singleLineHeight), filter);
                if (EditorGUI.EndChangeCheck())
                {
                    entryTexts = null; // Need to update filtered entry list.
                }
                var wasEnabled = GUI.enabled;
                GUI.enabled = wasEnabled && conversation != null && database != null;
                if (GUI.Button(new Rect(position.x + position.width - openButtonWidth, position.y, openButtonWidth, EditorGUIUtility.singleLineHeight), "Open"))
                {
                    var entry = conversation.GetDialogueEntry(prop.intValue) ?? conversation.GetFirstDialogueEntry();
                    if (entry != null)
                    {
                        DialogueEditorBridge.OpenDialogueEntry(database, conversation.id, entry.id);
                        GUIUtility.ExitGUI();
                    }
                }
                GUI.enabled = wasEnabled;
                position.y += EditorGUIUtility.singleLineHeight;
                position.height -= EditorGUIUtility.singleLineHeight;
            }

            if (entryTexts == null) UpdateDialogueEntryList(conversationProp, prop.intValue, filter);

            // Update current index:
            var currentIndex = GetIndex(prop.intValue);

            // Draw popup or plain text field:
            var rect = new Rect(position.x, position.y, position.width - 30, EditorGUIUtility.singleLineHeight);
            if (usePicker)
            {
                var newIndex = EditorGUI.Popup(rect, currentIndex, entryTexts);
                if ((newIndex != currentIndex) && (0 <= newIndex && newIndex < entryTexts.Length))
                {
                    prop.intValue = GetID(newIndex);
                    GUI.changed = true;
                }
            }
            else
            {
                EditorGUI.PropertyField(rect, prop, GUIContent.none, true);
            }

            // Radio button toggle between popup and plain text field:
            rect = new Rect(position.x + position.width - 22, position.y, 22, EditorGUIUtility.singleLineHeight);
            var newToggleValue = EditorGUI.Toggle(rect, usePicker, EditorStyles.radioButton);
            if (newToggleValue != usePicker)
            {
                usePicker = newToggleValue;
                if (usePicker && (EditorTools.selectedDatabase == null)) EditorTools.selectedDatabase = EditorTools.FindInitialDatabase();
                entryTexts = null;
            }

            EditorGUI.EndProperty();
        }

        private SerializedProperty FindConversationProperty(SerializedProperty prop)
        {
            return prop.serializedObject.FindProperty("conversation") ??
                prop.serializedObject.FindProperty("conversationName") ??
                prop.serializedObject.FindProperty("conversationTitle") ??
                prop.serializedObject.FindProperty("Conversation") ??
                prop.serializedObject.FindProperty("ConversationName") ??
                prop.serializedObject.FindProperty("ConversationTitle") ??
                prop.serializedObject.FindProperty("m_conversation") ??
                prop.serializedObject.FindProperty("m_conversationName") ??
                prop.serializedObject.FindProperty("m_conversationTitle") ??
                prop.serializedObject.FindProperty("m_Conversation") ??
                prop.serializedObject.FindProperty("m_ConversationName") ??
                prop.serializedObject.FindProperty("m_ConversationTitle") ??
                prop.serializedObject.FindProperty("_conversation") ??
                prop.serializedObject.FindProperty("_conversationName") ??
                prop.serializedObject.FindProperty("_conversationTitle") ??
                prop.serializedObject.FindProperty("_Conversation") ??
                prop.serializedObject.FindProperty("_ConversationName") ??
                prop.serializedObject.FindProperty("_ConversationTitle");
        }

        public void UpdateDialogueEntryList(SerializedProperty conversationProp, int currentID, string filter)
        {
            database = EditorTools.selectedDatabase;
            conversation = (conversationProp == null || database == null || database.conversations == null) ? null
                : database.conversations.Find(x => x.Title == conversationProp.stringValue);
            var list = new List<string>();
            idToIndex = new Dictionary<int, int>();
            indexToId = new Dictionary<int, int>();
            if (conversation != null)
            {
                list.Add("(None)");
                var hasFilter = !string.IsNullOrEmpty(filter);
                var index = 1;
                for (int i = 0; i < conversation.dialogueEntries.Count; i++)
                {
                    var entry = conversation.dialogueEntries[i];
                    if (entry == null || idToIndex.ContainsKey(entry.id)) continue;
                    if (hasFilter && entry.id != currentID && !DialogueEntryPicker.MatchesFilter(entry, filter)) continue;
                    list.Add("[" + entry.id + "] " + ((entry.id == 0) ? "<START>" : entry.subtitleText).Replace("/", "∕"));
                    idToIndex.Add(entry.id, index);
                    indexToId.Add(index, entry.id);
                    index++;
                }
            }
            entryTexts = list.ToArray();
        }

        private int GetID(int index)
        {
            return indexToId.ContainsKey(index) ? indexToId[index] : -1;
        }

        private int GetIndex(int id)
        {
            return idToIndex.ContainsKey(id) ? idToIndex[id] : -1;
        }

    }

}
