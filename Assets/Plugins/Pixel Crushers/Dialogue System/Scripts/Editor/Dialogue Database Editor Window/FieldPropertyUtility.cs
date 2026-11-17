// Copyright (c) Pixel Crushers. All rights reserved.

#if UNITY_2022_1_OR_NEWER

using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;

namespace PixelCrushers.DialogueSystem.DialogueEditor
{

    /// <summary>
    /// Utility methods for working with an asset's List<Field> through SerializedProperty,
    /// and for creating UI Toolkit editors for field values.
    /// </summary>
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public static class FieldPropertyUtility
    {

        public const string TitlePropertyName = "title";
        public const string ValuePropertyName = "value";
        public const string TypePropertyName = "type";
        public const string TypeStringPropertyName = "typeString";

        public const string NoIDString = "-1";

        /// <summary>Shown for a field whose value differs across multiple selected assets, like the IMGUI editor.</summary>
        public const string MixedValue = "-";

        private const int MixedID = int.MinValue;

        public static readonly List<string> QuestStateStrings = new List<string> { "(None)", "unassigned", "active", "success", "failure", "done", "abandoned", "grantable", "returnToNPC" };

        private static readonly List<string> TextAreaFieldTitles = new List<string> { "Description", "Success Description", "Failure Description" };

        #region Field Lookup and Modification

        public static int FindFieldIndex(SerializedProperty fieldsProperty, string title)
        {
            if (fieldsProperty == null || !fieldsProperty.isArray) return -1;
            for (int i = 0; i < fieldsProperty.arraySize; i++)
            {
                var titleProperty = fieldsProperty.GetArrayElementAtIndex(i).FindPropertyRelative(TitlePropertyName);
                if (string.Equals(titleProperty.stringValue, title)) return i;
            }
            return -1;
        }

        public static SerializedProperty FindField(SerializedProperty fieldsProperty, string title)
        {
            var index = FindFieldIndex(fieldsProperty, title);
            return (index >= 0) ? fieldsProperty.GetArrayElementAtIndex(index) : null;
        }

        public static string LookupValue(SerializedProperty fieldsProperty, string title)
        {
            var fieldProperty = FindField(fieldsProperty, title);
            return (fieldProperty != null) ? fieldProperty.FindPropertyRelative(ValuePropertyName).stringValue : null;
        }

        /// <summary>
        /// Appends a field. Caller is responsible for ApplyModifiedProperties.
        /// </summary>
        public static SerializedProperty AddField(SerializedProperty fieldsProperty, string title, string value, FieldType type, string typeString = "")
        {
            var index = fieldsProperty.arraySize;
            fieldsProperty.InsertArrayElementAtIndex(index);
            var fieldProperty = fieldsProperty.GetArrayElementAtIndex(index);
            SetField(fieldProperty, title, value, type, typeString);
            return fieldProperty;
        }

        public static void SetField(SerializedProperty fieldProperty, string title, string value, FieldType type, string typeString)
        {
            fieldProperty.FindPropertyRelative(TitlePropertyName).stringValue = title ?? string.Empty;
            fieldProperty.FindPropertyRelative(ValuePropertyName).stringValue = value ?? string.Empty;
            fieldProperty.FindPropertyRelative(TypePropertyName).intValue = (int)type;
            fieldProperty.FindPropertyRelative(TypeStringPropertyName).stringValue = typeString ?? string.Empty;
        }

        /// <summary>
        /// Returns a field, adding it first if it doesn't exist. Applies the change if a field is added.
        /// </summary>
        public static SerializedProperty GetOrAddField(SerializedProperty fieldsProperty, string title, string defaultValue, FieldType type)
        {
            var fieldProperty = FindField(fieldsProperty, title);
            if (fieldProperty != null) return fieldProperty;
            fieldProperty = AddField(fieldsProperty, title, defaultValue, type);
            fieldsProperty.serializedObject.ApplyModifiedProperties();
            return fieldProperty;
        }

        /// <summary>
        /// Sets a field's value, adding the field if it doesn't exist. Applies the change
        /// unless apply is false, so callers can batch several changes into one undo step.
        /// </summary>
        public static void SetFieldValue(SerializedProperty fieldsProperty, string title, string value, FieldType type, bool apply = true)
        {
            var fieldProperty = FindField(fieldsProperty, title);
            if (fieldProperty == null) fieldProperty = AddField(fieldsProperty, title, value, type);
            fieldProperty.FindPropertyRelative(ValuePropertyName).stringValue = value ?? string.Empty;
            if (apply) fieldsProperty.serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// Removes a field if it exists. Applies the change unless apply is false.
        /// </summary>
        public static void RemoveField(SerializedProperty fieldsProperty, string title, bool apply = true)
        {
            var index = FindFieldIndex(fieldsProperty, title);
            if (index < 0) return;
            fieldsProperty.DeleteArrayElementAtIndex(index);
            if (apply) fieldsProperty.serializedObject.ApplyModifiedProperties();
        }

        public static Field ToField(SerializedProperty fieldProperty)
        {
            return new Field(fieldProperty.FindPropertyRelative(TitlePropertyName).stringValue,
                fieldProperty.FindPropertyRelative(ValuePropertyName).stringValue,
                (FieldType)fieldProperty.FindPropertyRelative(TypePropertyName).intValue,
                fieldProperty.FindPropertyRelative(TypeStringPropertyName).stringValue);
        }

        public static List<Field> ToFieldList(SerializedProperty fieldsProperty)
        {
            var list = new List<Field>();
            for (int i = 0; i < fieldsProperty.arraySize; i++)
            {
                list.Add(ToField(fieldsProperty.GetArrayElementAtIndex(i)));
            }
            return list;
        }

        /// <summary>
        /// Replaces the contents of a fields array. Caller is responsible for ApplyModifiedProperties.
        /// </summary>
        public static void AssignFields(SerializedProperty fieldsProperty, List<Field> fields)
        {
            fieldsProperty.ClearArray();
            if (fields == null) return;
            foreach (var field in fields)
            {
                AddField(fieldsProperty, field.title, field.value, field.type, field.typeString);
            }
        }

        /// <summary>
        /// Adds any template fields that are missing and fixes types that differ from the
        /// template, like DialogueEditorWindow.ApplyTemplate. Applies the change.
        /// </summary>
        public static void ApplyTemplate(SerializedProperty fieldsProperty, List<Field> templateFields)
        {
            if (fieldsProperty == null || templateFields == null) return;
            foreach (var templateField in templateFields)
            {
                if (string.IsNullOrEmpty(templateField.title)) continue;
                var fieldProperty = FindField(fieldsProperty, templateField.title);
                if (fieldProperty != null)
                {
                    // Variables' Initial Value should never be applied from template.
                    var typeProperty = fieldProperty.FindPropertyRelative(TypePropertyName);
                    if (typeProperty.intValue != (int)templateField.type && !string.Equals(templateField.title, "Initial Value"))
                    {
                        typeProperty.intValue = (int)templateField.type;
                        fieldProperty.FindPropertyRelative(TypeStringPropertyName).stringValue = string.Empty;
                    }
                }
                else
                {
                    AddField(fieldsProperty, templateField.title, templateField.value, templateField.type, templateField.typeString);
                }
            }
            fieldsProperty.serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// Copies values from source fields, adding any that are missing, like
        /// DialogueEditorWindow.PasteFields. Applies the change.
        /// </summary>
        public static void PasteFields(SerializedProperty fieldsProperty, List<Field> sourceFields)
        {
            if (fieldsProperty == null || sourceFields == null) return;
            foreach (var sourceField in sourceFields)
            {
                var fieldProperty = FindField(fieldsProperty, sourceField.title);
                if (fieldProperty != null)
                {
                    fieldProperty.FindPropertyRelative(ValuePropertyName).stringValue = sourceField.value ?? string.Empty;
                    fieldProperty.FindPropertyRelative(TypePropertyName).intValue = (int)sourceField.type;
                }
                else
                {
                    AddField(fieldsProperty, sourceField.title, sourceField.value, sourceField.type, sourceField.typeString);
                }
            }
            fieldsProperty.serializedObject.ApplyModifiedProperties();
        }

        public static bool IsTextAreaField(string title)
        {
            if (string.IsNullOrEmpty(title)) return false;
            return TextAreaFieldTitles.Contains(title) || IsQuestEntryDescription(title);
        }

        private static bool IsQuestEntryDescription(string title)
        {
            return title.StartsWith("Entry ") && !title.EndsWith(" State") &&
                !title.EndsWith(" Count") && !title.EndsWith(" End") && !title.EndsWith(" ID");
        }

        private static bool IsQuestStateField(string title)
        {
            return string.Equals(title, "State") || (!string.IsNullOrEmpty(title) && title.EndsWith(" State"));
        }

        /// <summary>
        /// Returns the field's custom field type class name. If typeString isn't set yet,
        /// derives it the same way CustomFieldTypeService does, without modifying the field.
        /// </summary>
        public static string GetEffectiveTypeString(SerializedProperty fieldProperty)
        {
            return GetEffectiveTypeString(fieldProperty.FindPropertyRelative(TypeStringPropertyName).stringValue,
                fieldProperty.FindPropertyRelative(TitlePropertyName).stringValue,
                (FieldType)fieldProperty.FindPropertyRelative(TypePropertyName).intValue);
        }

        private static string GetEffectiveTypeString(string typeString, string title, FieldType type)
        {
            if (!string.IsNullOrEmpty(typeString)) return typeString;
            if (IsQuestStateField(title)) return "CustomFieldType_QuestState";
            return "CustomFieldType_" + type.ToString();
        }

        #endregion

        #region Value Editors

        /// <summary>
        /// Creates an editor for a field's value based on its type. Built-in types use native
        /// UI Toolkit controls. Custom field types fall back to their IMGUI drawers.
        /// </summary>
        /// <param name="fieldProperty">The Field element.</param>
        /// <param name="database">Database used for actor/item/location popups.</param>
        /// <param name="label">Optional label. Null for an unlabeled editor.</param>
        /// <param name="multiline">Use a multiline text field for text types.</param>
        public static VisualElement CreateValueEditor(SerializedProperty fieldProperty, DialogueDatabase database, string label = null, bool multiline = false)
        {
            var valueProperty = fieldProperty.FindPropertyRelative(ValuePropertyName);
            switch (GetEffectiveTypeString(fieldProperty))
            {
                case "CustomFieldType_Text":
                case "CustomFieldType_Files":
                case "CustomFieldType_Localization":
                    return CreateTextField(label, valueProperty, multiline);
                case "CustomFieldType_Number":
                    return CreateNumberField(label, valueProperty);
                case "CustomFieldType_Boolean":
                    return CreateBooleanField(label, valueProperty);
                case "CustomFieldType_QuestState":
                    return CreateQuestStateField(label, valueProperty);
                case "CustomFieldType_Actor":
                    return CreateAssetPopup(label, valueProperty, (database != null) ? database.actors : null);
                case "CustomFieldType_Item":
                    return CreateAssetPopup(label, valueProperty, (database != null) ? database.items : null);
                case "CustomFieldType_Location":
                    return CreateAssetPopup(label, valueProperty, (database != null) ? database.locations : null);
                default:
                    return CreateCustomTypeField(label, fieldProperty, database);
            }
        }

        /// <summary>
        /// Creates an editor that isn't bound to a property, for editing one field on several
        /// assets at once. Value edits are reported as strings, in the field's stored format.
        /// </summary>
        /// <param name="field">Provides the field's title and type.</param>
        /// <param name="value">Initial value, or MixedValue if the assets' values differ.</param>
        /// <param name="valueChanged">Called with the new value when the user changes it.</param>
        public static VisualElement CreateUnboundValueEditor(Field field, string value, DialogueDatabase database, string label, bool multiline, Action<string> valueChanged)
        {
            switch (GetEffectiveTypeString(field.typeString, field.title, field.type))
            {
                case "CustomFieldType_Text":
                case "CustomFieldType_Files":
                case "CustomFieldType_Localization":
                    var textField = CreateTextField(label, multiline);
                    textField.SetValueWithoutNotify(value);
                    textField.RegisterValueChangedCallback(evt => valueChanged(evt.newValue));
                    return textField;
                case "CustomFieldType_Number":
                    var floatField = new FloatField(label);
                    floatField.SetValueWithoutNotify(StringToFloat(value));
                    floatField.RegisterValueChangedCallback(evt => valueChanged(evt.newValue.ToString(CultureInfo.InvariantCulture)));
                    return floatField;
                case "CustomFieldType_Boolean":
                    var toggle = new UnityEngine.UIElements.Toggle(label);
                    toggle.SetValueWithoutNotify(StringToBool(value));
                    toggle.RegisterValueChangedCallback(evt => valueChanged(BoolToString(evt.newValue)));
                    return toggle;
                case "CustomFieldType_QuestState":
                    return CreateUnboundQuestStatePopup(label, value, valueChanged);
                case "CustomFieldType_Actor":
                    return CreateUnboundAssetPopup(label, value, (database != null) ? database.actors : null, valueChanged);
                case "CustomFieldType_Item":
                    return CreateUnboundAssetPopup(label, value, (database != null) ? database.items : null, valueChanged);
                case "CustomFieldType_Location":
                    return CreateUnboundAssetPopup(label, value, (database != null) ? database.locations : null, valueChanged);
                default:
                    return CreateUnboundCustomTypeField(label, field, value, database, valueChanged);
            }
        }

        private static VisualElement CreateUnboundQuestStatePopup(string label, string value, Action<string> valueChanged)
        {
            var choices = new List<string>(QuestStateStrings);
            if (value == MixedValue) choices.Insert(0, MixedValue);
            var current = choices.Contains(value) ? value : QuestStateStrings[0];
            var popup = new PopupField<string>(label, choices, current);
            popup.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue == MixedValue) return;
                valueChanged((evt.newValue == QuestStateStrings[0]) ? string.Empty : evt.newValue);
            });
            return popup;
        }

        private static VisualElement CreateUnboundAssetPopup<T>(string label, string value, List<T> assets, Action<string> valueChanged) where T : Asset
        {
            var ids = new List<int>();
            var names = new Dictionary<int, string>();
            AddAssetChoices(ids, names, assets);
            int currentID;
            if (value == MixedValue)
            {
                currentID = MixedID;
                ids.Insert(0, MixedID);
                names.Add(MixedID, MixedValue);
            }
            else if (!int.TryParse(value, out currentID))
            {
                currentID = -1;
            }
            else if (!names.ContainsKey(currentID))
            {
                ids.Add(currentID);
                names.Add(currentID, string.Format("(Missing) [{0}]", currentID));
            }
            Func<int, string> format = id => names.TryGetValue(id, out var name) ? name : id.ToString();
            // The int argument is an index into ids, not a default value, since T is int.
            var popup = new PopupField<int>(label, ids, ids.IndexOf(currentID), format, format);
            popup.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue != MixedID) valueChanged(evt.newValue.ToString());
            });
            return popup;
        }

        private static VisualElement CreateUnboundCustomTypeField(string label, Field field, string value, DialogueDatabase database, Action<string> valueChanged)
        {
            var labelContent = string.IsNullOrEmpty(label) ? null : new GUIContent(label);
            var currentField = new Field(field) { value = value };
            return new IMGUIContainer(() =>
            {
                var newValue = (labelContent != null)
                    ? CustomFieldTypeService.DrawField(labelContent, currentField, database)
                    : CustomFieldTypeService.DrawField(currentField, database);
                if (newValue == currentField.value) return;
                currentField.value = newValue;
                valueChanged(newValue);
            });
        }

        public static TextField CreateTextField(string label, SerializedProperty valueProperty, bool multiline)
        {
            var textField = CreateTextField(label, multiline);
            textField.BindProperty(valueProperty);
            return textField;
        }

        /// <summary>
        /// Creates an unbound text field with the same styling as the bound version.
        /// </summary>
        public static TextField CreateTextField(string label, bool multiline)
        {
            var textField = new TextField(label) { multiline = multiline };
            if (multiline)
            {
                textField.style.whiteSpace = WhiteSpace.Normal;
                textField.style.minHeight = 48;
            }
            return textField;
        }

        private static VisualElement CreateNumberField(string label, SerializedProperty valueProperty)
        {
            var floatField = new FloatField(label);
            BindStringValue(floatField, valueProperty, StringToFloat, value => value.ToString(CultureInfo.InvariantCulture));
            return floatField;
        }

        private static VisualElement CreateBooleanField(string label, SerializedProperty valueProperty)
        {
            var toggle = new UnityEngine.UIElements.Toggle(label);
            BindStringValue(toggle, valueProperty, StringToBool, BoolToString);
            return toggle;
        }

        private static VisualElement CreateQuestStateField(string label, SerializedProperty valueProperty)
        {
            var popup = new PopupField<string>(label, new List<string>(QuestStateStrings), 0);
            BindStringValue(popup, valueProperty,
                s => (string.IsNullOrEmpty(s) || !QuestStateStrings.Contains(s)) ? QuestStateStrings[0] : s,
                s => (s == QuestStateStrings[0]) ? string.Empty : s);
            return popup;
        }

        private static VisualElement CreateAssetPopup<T>(string label, SerializedProperty valueProperty, List<T> assets) where T : Asset
        {
            var ids = new List<int>();
            var names = new Dictionary<int, string>();
            AddAssetChoices(ids, names, assets);
            Func<string, int> stringToID = s =>
            {
                int id;
                if (!int.TryParse(s, out id)) return -1;
                if (!names.ContainsKey(id))
                {
                    // Keep references to missing assets visible instead of silently showing (None).
                    ids.Add(id);
                    names.Add(id, string.Format("(Missing) [{0}]", id));
                }
                return id;
            };
            Func<int, string> format = id => names.TryGetValue(id, out var name) ? name : id.ToString();
            var currentID = stringToID(valueProperty.stringValue); // May add a missing asset to ids.
            var popup = new PopupField<int>(label, ids, ids.IndexOf(currentID), format, format);
            BindStringValue(popup, valueProperty, stringToID, id => (id == -1) ? NoIDString : id.ToString());
            return popup;
        }

        /// <summary>Adds (None) and the assets to the popup choices.</summary>
        private static void AddAssetChoices<T>(List<int> ids, Dictionary<int, string> names, List<T> assets) where T : Asset
        {
            ids.Add(-1);
            names.Add(-1, "(None)");
            if (assets == null) return;
            foreach (var asset in assets)
            {
                if (names.ContainsKey(asset.id)) continue;
                ids.Add(asset.id);
                names.Add(asset.id, string.Format("{0} [{1}]", asset.Name, asset.id));
            }
        }

        private static VisualElement CreateCustomTypeField(string label, SerializedProperty fieldProperty, DialogueDatabase database)
        {
            // Custom field types only provide IMGUI drawers, so host them in an IMGUIContainer.
            // The drawer edits a temporary Field, and changes are written back through the SerializedProperty.
            var labelContent = string.IsNullOrEmpty(label) ? null : new GUIContent(label);
            var container = new IMGUIContainer(() =>
            {
                var serializedObject = fieldProperty.serializedObject;
                if (serializedObject == null || serializedObject.targetObject == null) return;
                var field = ToField(fieldProperty);
                var newValue = (labelContent != null)
                    ? CustomFieldTypeService.DrawField(labelContent, field, database)
                    : CustomFieldTypeService.DrawField(field, database);
                if (newValue != field.value)
                {
                    fieldProperty.FindPropertyRelative(ValuePropertyName).stringValue = newValue;
                    serializedObject.ApplyModifiedProperties();
                }
            });
            return container;
        }

        /// <summary>
        /// Binds a field whose value type isn't string to a string SerializedProperty,
        /// converting in both directions and tracking external changes.
        /// </summary>
        private static void BindStringValue<T>(BaseField<T> field, SerializedProperty valueProperty, Func<string, T> fromString, Func<T, string> toString)
        {
            field.SetValueWithoutNotify(fromString(valueProperty.stringValue));
            field.RegisterValueChangedCallback(evt =>
            {
                var newValue = toString(evt.newValue);
                if (newValue == valueProperty.stringValue) return;
                valueProperty.stringValue = newValue;
                valueProperty.serializedObject.ApplyModifiedProperties();
            });
            field.TrackPropertyValue(valueProperty, property => field.SetValueWithoutNotify(fromString(property.stringValue)));
        }

        public static float StringToFloat(string s)
        {
            float result;
            return float.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out result) ? result : 0;
        }

        public static bool StringToBool(string s)
        {
            return string.Equals(s, "true", StringComparison.OrdinalIgnoreCase);
        }

        public static string BoolToString(bool value)
        {
            return value ? "True" : "False";
        }

        /// <summary>
        /// Creates a popup that selects a field's custom field type. Changing the type clears
        /// the value, like CustomFieldTypeService.DrawFieldType.
        /// </summary>
        public static PopupField<string> CreateTypePopup(SerializedProperty fieldProperty, Action onTypeChanged)
        {
            var typeNames = CustomFieldTypeService.GetDialogueSystemTypes();
            var publicNames = CustomFieldTypeService.GetDialogueSystemPublicNames();
            var choices = new List<string>(typeNames);
            var current = GetEffectiveTypeString(fieldProperty);
            if (!choices.Contains(current)) current = (choices.Count > 0) ? choices[0] : current;
            Func<string, string> format = typeName =>
            {
                var index = Array.IndexOf(typeNames, typeName);
                return (index >= 0 && index < publicNames.Length) ? publicNames[index] : typeName;
            };
            var popup = new PopupField<string>(choices, current, format, format);
            popup.RegisterValueChangedCallback(evt =>
            {
                CustomFieldType customFieldType;
                if (!CustomFieldTypeService.GetTypesDictionary().TryGetValue(evt.newValue, out customFieldType)) return;
                fieldProperty.FindPropertyRelative(TypeStringPropertyName).stringValue = evt.newValue;
                fieldProperty.FindPropertyRelative(TypePropertyName).intValue = (int)customFieldType.storeFieldAsType;
                fieldProperty.FindPropertyRelative(ValuePropertyName).stringValue = string.Empty;
                fieldProperty.serializedObject.ApplyModifiedProperties();
                if (onTypeChanged != null) onTypeChanged();
            });
            return popup;
        }

        #endregion

    }

}

#endif
