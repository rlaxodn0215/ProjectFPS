#if USE_TIMELINE
#if UNITY_2017_1_OR_NEWER
using UnityEngine;
using UnityEditor;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEditor.Timeline;

namespace PixelCrushers.DialogueSystem
{

    [CustomPropertyDrawer(typeof(StartConversationBehaviour))]
    public class StartConversationBehaviourDrawer : PropertyDrawer
    {
        private DialogueEntryPicker entryPicker = null;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            int fieldCount = 7;
            var jumpToSpecificEntryProp = property.FindPropertyRelative(nameof(StartConversationBehaviour.jumpToSpecificEntry));
            if (jumpToSpecificEntryProp != null && jumpToSpecificEntryProp.boolValue) fieldCount += 2; // Filter + Entry ID.
            return fieldCount * EditorGUIUtility.singleLineHeight + 2;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty conversationProp = property.FindPropertyRelative(nameof(StartConversationBehaviour.conversation));
            SerializedProperty jumpToSpecificEntryProp = property.FindPropertyRelative(nameof(StartConversationBehaviour.jumpToSpecificEntry));
            SerializedProperty entryIDProp = property.FindPropertyRelative(nameof(StartConversationBehaviour.entryID));
            SerializedProperty exclusiveProp = property.FindPropertyRelative(nameof(StartConversationBehaviour.exclusive));
            SerializedProperty conversantProp = property.FindPropertyRelative(nameof(StartConversationBehaviour.conversant));

            Rect singleFieldRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

            if (GUI.Button(singleFieldRect, "Update Duration"))
            {
                UpdateLength(conversationProp.stringValue, jumpToSpecificEntryProp.boolValue, entryIDProp.intValue);
            }

            singleFieldRect.y += EditorGUIUtility.singleLineHeight;
            EditorGUI.PropertyField(new Rect(singleFieldRect.x, singleFieldRect.y, singleFieldRect.width, 2 * EditorGUIUtility.singleLineHeight), conversationProp);

            singleFieldRect.y += 2 * EditorGUIUtility.singleLineHeight;
            EditorGUI.PropertyField(singleFieldRect, jumpToSpecificEntryProp);

            if (jumpToSpecificEntryProp.boolValue)
            {
                singleFieldRect.y += EditorGUIUtility.singleLineHeight;
                if (entryPicker == null || entryPicker.conversationTitle != conversationProp.stringValue)
                {
                    var previousFilter = (entryPicker != null) ? entryPicker.filter : string.Empty;
                    entryPicker = new DialogueEntryPicker(conversationProp.stringValue);
                    entryPicker.filter = previousFilter;
                }
                if (entryPicker.isValid)
                {
                    entryPicker.DrawFilter(singleFieldRect);
                    singleFieldRect.y += EditorGUIUtility.singleLineHeight;
                    entryIDProp.intValue = entryPicker.Draw(singleFieldRect, "Entry ID", entryIDProp.intValue);
                }
                else
                {
                    EditorGUI.PropertyField(singleFieldRect, entryIDProp);
                }
            }

            singleFieldRect.y += EditorGUIUtility.singleLineHeight;
            EditorGUI.PropertyField(singleFieldRect, exclusiveProp);
            singleFieldRect.y += EditorGUIUtility.singleLineHeight;
            singleFieldRect.y += EditorGUIUtility.singleLineHeight;
            EditorGUI.HelpBox(singleFieldRect, "Actor is GameObject assigned to this track.", MessageType.None);
        }

        private void UpdateLength(string conversation, bool jumpToSpecificEntry, int entryID = -1)
        {
            var duration = ConversationTimelineUtility.GetSequenceDuration(conversation, jumpToSpecificEntry ? entryID : -1);
            Debug.Log("Best estimate duration: " + duration + " seconds");
            if (TimelineEditor.selectedClip == null) return;
            TimelineEditor.selectedClip.duration = duration;
            EditorUtility.SetDirty(TimelineEditor.inspectedAsset);
        }
    }
}
#endif
#endif
