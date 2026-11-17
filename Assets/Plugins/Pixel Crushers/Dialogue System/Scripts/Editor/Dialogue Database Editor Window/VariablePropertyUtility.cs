// Copyright (c) Pixel Crushers. All rights reserved.

#if UNITY_2022_1_OR_NEWER

using UnityEditor;

namespace PixelCrushers.DialogueSystem.DialogueEditor
{

    /// <summary>
    /// Helpers for editing the database's variables through its SerializedObject.
    /// </summary>
    public static class VariablePropertyUtility
    {

        /// <summary>
        /// Adds a variable with the template's fields, unless one with that name already exists.
        /// Applies the change, so it's undoable.
        /// </summary>
        public static void AddVariable(SerializedObject serializedObject, DialogueDatabase database, Template template, string name, FieldType type)
        {
            if (serializedObject == null || database == null || template == null) return;
            if (string.IsNullOrWhiteSpace(name) || database.GetVariable(name) != null) return;
            serializedObject.Update();
            var variable = template.CreateVariable(template.GetNextVariableID(database), name, GetDefaultValue(type), type);
            var listProperty = serializedObject.FindProperty("variables");
            var index = listProperty.arraySize;
            listProperty.InsertArrayElementAtIndex(index);
            var variableProperty = listProperty.GetArrayElementAtIndex(index);
            variableProperty.FindPropertyRelative("id").intValue = variable.id;
            FieldPropertyUtility.AssignFields(variableProperty.FindPropertyRelative("fields"), variable.fields);
            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// Same initial values as the IMGUI Lua wizards use for new variables.
        /// </summary>
        private static string GetDefaultValue(FieldType type)
        {
            switch (type)
            {
                case FieldType.Boolean:
                    return "false";
                case FieldType.Actor:
                case FieldType.Item:
                case FieldType.Location:
                    return "0";
                default:
                    return string.Empty;
            }
        }

    }

}

#endif
