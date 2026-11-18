#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace cowsins.Inventory
{
    [CustomEditor(typeof(InventorySlot))]
    [CanEditMultipleObjects]
    public class InventorySlotEditor : Editor
    {
        private SerializedProperty slotTypeProp;
        private SerializedProperty acceptedItemTypeProp;
        private SerializedProperty uniqueIdProp;

        private SerializedProperty iconImageProp;
        private SerializedProperty amountTextProp;
        private SerializedProperty backgroundImageProp;
        private SerializedProperty durabilityBarProp;
        private SerializedProperty durabilityBarParentProp;
        private SerializedProperty highlightedScaleProp;
        private SerializedProperty highlightedBorderProp;
        private SerializedProperty lerpSpeedProp;
        private SerializedProperty iconImageMarginsProp;
        private SerializedProperty inventoryContainerProp;

        private string[] availableTypeNames;
        private int selectedTypeIndex = 0;

        private void OnEnable()
        {
            slotTypeProp = serializedObject.FindProperty("slotType");
            acceptedItemTypeProp = serializedObject.FindProperty("acceptedItemType");
            uniqueIdProp = serializedObject.FindProperty("uniqueID");

            iconImageProp = serializedObject.FindProperty("iconImage");
            amountTextProp = serializedObject.FindProperty("amountText");
            backgroundImageProp = serializedObject.FindProperty("backgroundImage");
            durabilityBarProp = serializedObject.FindProperty("durabilityBar");
            durabilityBarParentProp = serializedObject.FindProperty("durabilityBarParent");
            highlightedScaleProp = serializedObject.FindProperty("highlightedScale");
            highlightedBorderProp = serializedObject.FindProperty("highlightedBorder");
            lerpSpeedProp = serializedObject.FindProperty("lerpSpeed");
            iconImageMarginsProp = serializedObject.FindProperty("iconImageMargins");
            inventoryContainerProp = serializedObject.FindProperty("inventoryContainer");

            RefreshAvailableTypes();
        }

        private void RefreshAvailableTypes()
        {
            var types = TypeCache.GetTypesDerivedFrom<Item_SO>().Where(t => !t.IsAbstract).OrderBy(t => t.Name).Select(t => t.Name).ToList();

            var list = new List<string> { "Any (Item_SO)" };
            list.AddRange(types);
            availableTypeNames = list.ToArray();

            string currentType = acceptedItemTypeProp?.stringValue;
            if (string.IsNullOrEmpty(currentType) || currentType == nameof(Item_SO))
            {
                selectedTypeIndex = 0;
            }
            else
            {
                selectedTypeIndex = Array.IndexOf(availableTypeNames, currentType);
                if (selectedTypeIndex < 0) selectedTypeIndex = 0;
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("SLOT CONFIGURATION", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(slotTypeProp);

            SlotType currentSlotType = (SlotType)slotTypeProp.enumValueIndex;
            if (currentSlotType == SlotType.Custom)
            {
                EditorGUILayout.HelpBox("Spare Slot: Place anywhere in your UI. It will auto-initialize and accept defined items", MessageType.Info);
                if (targets.Length == 1)
                {
                    if (uniqueIdProp != null && string.IsNullOrEmpty(uniqueIdProp.stringValue))
                    {
                        uniqueIdProp.stringValue = Guid.NewGuid().ToString();
                    }

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Identifiable ID", uniqueIdProp != null ? uniqueIdProp.stringValue : "N/A");
                    if (GUILayout.Button("Regenerate", GUILayout.Width(85)))
                    {
                        if (uniqueIdProp != null)
                        {
                            uniqueIdProp.stringValue = Guid.NewGuid().ToString();
                            GUI.changed = true;
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                }
                else
                {
                    EditorGUILayout.LabelField("Identifiable ID", "Multiple objects selected");
                }
            }

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("ITEM ACCEPTANCE", EditorStyles.boldLabel);

            if (availableTypeNames == null || availableTypeNames.Length == 0)
                RefreshAvailableTypes();

            int newIndex = EditorGUILayout.Popup("Accepted Item Type", selectedTypeIndex, availableTypeNames);
            if (newIndex != selectedTypeIndex)
            {
                selectedTypeIndex = newIndex;
                string chosen = availableTypeNames[newIndex];
                acceptedItemTypeProp.stringValue = (chosen == "Any (Item_SO)") ? nameof(Item_SO) : chosen;
            }

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("UI REFERENCES", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(iconImageProp);
            EditorGUILayout.PropertyField(amountTextProp);
            EditorGUILayout.PropertyField(backgroundImageProp);
            EditorGUILayout.PropertyField(durabilityBarProp);
            EditorGUILayout.PropertyField(durabilityBarParentProp);

            EditorGUILayout.Space(5);
            EditorGUILayout.PropertyField(highlightedScaleProp);
            EditorGUILayout.PropertyField(highlightedBorderProp);
            EditorGUILayout.PropertyField(lerpSpeedProp);
            EditorGUILayout.PropertyField(iconImageMarginsProp);
            EditorGUILayout.PropertyField(inventoryContainerProp);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
