#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;
using System.Linq;

namespace cowsins.SaveLoad
{
    [CustomPropertyDrawer(typeof(GameDataManager.TypeToPrefabMapping))]
    public class TypeToPrefabMappingDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var typeNameProperty = property.FindPropertyRelative("TypeName");
            var prefabProperty = property.FindPropertyRelative("Prefab");

            // Get all available types from the Type Registry
            var availableTypes = TypeRegistry.GetAvailableTypes();

            // Find the current type index based on the passed property
            int currentIndex = availableTypes.IndexOf(typeNameProperty.stringValue);

            // Create dropdown to easily display & select types
            var dropdownRect = new Rect(position.x, position.y, position.width * 0.6f, EditorGUIUtility.singleLineHeight);
            int selectedIndex = EditorGUI.Popup(dropdownRect, currentIndex, availableTypes.Select(type => GetTypeDisplayName(type)).ToArray());

            if (selectedIndex >= 0 && selectedIndex < availableTypes.Count)
            {
                typeNameProperty.stringValue = availableTypes[selectedIndex];
            }

            // Prefab field to easily assign a prefab that corresponds to the selected property
            var prefabRect = new Rect(position.x + dropdownRect.width + 5, position.y, position.width * 0.35f, EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(prefabRect, prefabProperty, GUIContent.none);

            EditorGUI.EndProperty();
        }

        private string GetTypeDisplayName(string assemblyQualifiedName)
        {
            var type = System.Type.GetType(assemblyQualifiedName);
            return type != null ? type.Name : assemblyQualifiedName;
        }
    }
}
#endif