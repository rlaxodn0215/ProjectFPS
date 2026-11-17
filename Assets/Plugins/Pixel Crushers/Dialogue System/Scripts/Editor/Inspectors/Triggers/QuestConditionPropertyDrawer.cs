// Copyright (c) Pixel Crushers. All rights reserved.

using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace PixelCrushers.DialogueSystem
{

    [CustomPropertyDrawer(typeof(QuestCondition))]
    public class QuestConditionDrawer : PropertyDrawer
    {

        private bool useEntryPicker = true;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var height = 2 * EditorGUIUtility.singleLineHeight;
            if (property.FindPropertyRelative("checkQuestEntry").boolValue)
            {
                height += EditorGUIUtility.singleLineHeight;
            }
            return height;
        }

        // Draw the property inside the given rect
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {

            // Using BeginProperty / EndProperty on the parent property means that
            // prefab override logic works on the entire property.
            EditorGUI.BeginProperty(position, label, property);

            // Calculate rects:
            float halfWidth = position.width / 2;
            float questStateWidth = Mathf.Min(halfWidth, 120f);
            float questNameWidth = position.width - questStateWidth - 2;
            Rect questNameRect = new Rect(position.x, position.y, questNameWidth, EditorGUIUtility.singleLineHeight); //position.height);
            Rect questStateRect = new Rect(questNameRect.x + questNameRect.width + 2, position.y, questStateWidth, EditorGUIUtility.singleLineHeight); //position.height);
            Rect checkQuestEntryRect = new Rect(position.x, position.y + EditorGUIUtility.singleLineHeight, position.width, EditorGUIUtility.singleLineHeight);

            EditorTools.SetInitialDatabaseIfNull();

            // Draw fields - pass GUIContent.none to each so they are drawn without labels
            var questName = property.FindPropertyRelative("questName");
            if (EditorTools.selectedDatabase == null)
            {
                EditorGUI.PropertyField(questNameRect, questName, GUIContent.none);
            }
            else
            {
                int questNameIndex;
                string[] questNames = GetQuestNames(questName.stringValue, out questNameIndex);
                int newQuestNameIndex = EditorGUI.Popup(questNameRect, questNameIndex, questNames);
                if (newQuestNameIndex != questNameIndex)
                {
                    questName.stringValue = GetQuestName(questNames, newQuestNameIndex);
                }
            }

            var questState = property.FindPropertyRelative("questState");
            EditorGUI.PropertyField(questStateRect, questState, GUIContent.none, false);

            var checkQuestEntry = property.FindPropertyRelative("checkQuestEntry");
            EditorGUI.BeginChangeCheck();
            var newCheckQuestEntry = EditorGUI.ToggleLeft(checkQuestEntryRect, "Check Quest Entry State", checkQuestEntry.boolValue);
            if (EditorGUI.EndChangeCheck()) checkQuestEntry.boolValue = newCheckQuestEntry;
            if (checkQuestEntry.boolValue)
            {
                float lineY = position.y + 2 * EditorGUIUtility.singleLineHeight;
                float lineH = EditorGUIUtility.singleLineHeight;
                const float toggleWidth = 18f;
                Rect questEntryRect = new Rect(position.x, lineY, questNameWidth - toggleWidth - 2, lineH);
                Rect toggleRect = new Rect(questEntryRect.xMax + 2, lineY, toggleWidth, lineH);
                Rect questEntryStateRect = new Rect(questNameRect.xMax + 2, lineY, questStateWidth, lineH);
                var questEntryNumber = property.FindPropertyRelative("entryNumber");

                // Draw popup or plain text field:
                if (useEntryPicker)
                {
                    // Rebuild every time (cheap) so the list always reflects the current database and quest.
                    // If the stored entry number is out of range, append a placeholder so the popup shows it
                    // instead of drawing blank.
                    var currentIndex = questEntryNumber.intValue;
                    var entryNames = GetQuestEntryNames(questName.stringValue);
                    int validCount = entryNames.Count;
                    int popupIndex = currentIndex;
                    if (currentIndex < 0 || currentIndex >= validCount)
                    {
                        entryNames.Add(currentIndex + ". (missing)");
                        popupIndex = entryNames.Count - 1;
                    }
                    var newIndex = EditorGUI.Popup(questEntryRect, popupIndex, entryNames.ToArray());
                    if ((newIndex != popupIndex) && (0 <= newIndex && newIndex < validCount))
                    {
                        questEntryNumber.intValue = newIndex;
                        GUI.changed = true;
                    }
                }
                else
                {
                    EditorGUI.BeginChangeCheck();
                    var newEntryNumber = EditorGUI.IntField(questEntryRect, questEntryNumber.intValue);
                    if (EditorGUI.EndChangeCheck()) questEntryNumber.intValue = newEntryNumber;
                }

                // Radio button toggle between popup and plain text field:
                var newToggleValue = EditorGUI.Toggle(toggleRect, useEntryPicker, EditorStyles.radioButton);
                if (newToggleValue != useEntryPicker)
                {
                    useEntryPicker = newToggleValue;
                    if (useEntryPicker && (EditorTools.selectedDatabase == null)) EditorTools.selectedDatabase = EditorTools.FindInitialDatabase();
                }

                EditorGUI.PropertyField(questEntryStateRect, property.FindPropertyRelative("questEntryState"), GUIContent.none);
            }

            EditorGUI.EndProperty();
        }

        private string[] GetQuestNames(string currentQuestName, out int questNameIndex)
        {
            questNameIndex = -1;
            var database = EditorTools.selectedDatabase;
            if (database == null || database.items == null)
            {
                return new string[0];
            }
            else
            {
                List<string> questNames = new List<string>();
                foreach (var item in database.items)
                {
                    if (!item.IsItem)
                    {
                        string questName = item.Name;
                        if (string.Equals(currentQuestName, questName))
                        {
                            questNameIndex = questNames.Count;
                        }
                        questNames.Add(questName);
                    }
                }
                return questNames.ToArray();
            }
        }

        private string GetQuestName(string[] questNames, int questNameIndex)
        {
            return (0 <= questNameIndex && questNameIndex < questNames.Length) ? questNames[questNameIndex] : string.Empty;
        }

        private List<string> GetQuestEntryNames(string questName)
        {
            var list = new List<string>();
            var database = EditorTools.selectedDatabase;
            if (database == null || database.items == null) return list;
            var quest = database.items.Find(x => x.Name == questName);
            if (quest != null)
            {
                list.Add("(None)");
                var entryCount = quest.LookupInt("Entry Count");
                for (int i = 1; i <= entryCount; i++)
                {
                    list.Add(i + ". " + quest.LookupValue("Entry " + i));
                }
            }
            return list;
        }

    }

}
