using System;
using UnityEngine;
using Newtonsoft.Json;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using System.Collections;

namespace cowsins.SaveLoad
{
    [Serializable]
    public class CustomSaveData
    {
        // Stores the scene of the object to save
        // Useful to filter items that need to be loaded when playing the game,
        // to avoid trying to load objects that are not part of the current scene.
        [JsonProperty] public string SceneName { get; set; }

        // Position of the object to save. If no value is passed, it will be ignored and not serialized into the Json file.
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)] public SerializableVector3 Position { get; set; }

        // Stable Prefab Identifier. Takes precedence over Type name when instantiating saved objects.
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)] public string PrefabId { get; set; }

        public string Type { get; set; }

        // Dictionary that contains all the saved variables and properties. 
        public Dictionary<string, object> savedFields = new Dictionary<string, object>();

        // Caches
        private static readonly Dictionary<Type, FieldInfo[]> cachedSaveFields = new Dictionary<Type, FieldInfo[]>();
        private static readonly Dictionary<Type, FieldInfo> cachedSceneNameFields = new Dictionary<Type, FieldInfo>();
        private static readonly Dictionary<Type, bool> cachedSerializableTypes = new Dictionary<Type, bool>();

        // Default Constructor
        public CustomSaveData()
        {

        }

        // Recommended Constructor
        public CustomSaveData(string sceneName, Vector3? position = null)
        {
            SceneName = sceneName;
            Position = position.HasValue ? new SerializableVector3(position.Value) : null;
            Type = this.GetType().Name;
        }

        private static FieldInfo[] GetSaveFieldsForType(Type type)
        {
            if (cachedSaveFields.TryGetValue(type, out FieldInfo[] fields))
            {
                return fields;
            }

            var allFields = type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            var saveFieldsList = new List<FieldInfo>();

            for (int i = 0; i < allFields.Length; i++)
            {
                if (allFields[i].GetCustomAttribute<SaveFieldAttribute>() != null)
                {
                    saveFieldsList.Add(allFields[i]);
                }
            }

            fields = saveFieldsList.ToArray();
            cachedSaveFields[type] = fields;
            return fields;
        }

        private static FieldInfo GetSceneNameField(Type type)
        {
            if (cachedSceneNameFields.TryGetValue(type, out FieldInfo field))
            {
                return field;
            }

            field = type.GetField("SceneName", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            cachedSceneNameFields[type] = field;
            return field;
        }

        // Stores all saveable obj's fields into saveFields
        // For a variable to be saved, add the [SaveField] and ensure the class inherits from Identifiable
        public object SaveFields(object obj)
        {
            if (obj == null) return null;

            var saveData = new CustomSaveData();
            Type objType = obj.GetType();
            FieldInfo[] fields = GetSaveFieldsForType(objType);

            // Loop through each cached [SaveField]
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                object fieldValue = field.GetValue(obj);

                // Skip null values
                if (fieldValue == null) continue;

                // Check for invalid numerical values to avoid unexpected serialization errors
                if (fieldValue is float f && (float.IsNaN(f) || float.IsInfinity(f)) ||
                    fieldValue is double d && (double.IsNaN(d) || double.IsInfinity(d)))
                {
                    continue;
                }

                // If its an Item_SO or a subclass, save only its name, since ScriptableObjects cannot be serialized directly.
                // We'll then load it using ItemRegistry.
                if (fieldValue is Item_SO item)
                {
                    saveData.savedFields[field.Name] = item.name;
                }
                else
                {
                    // Check for non-serializable types
                    if (!IsSerializable(fieldValue))
                    {
                        continue;
                    }
                    saveData.savedFields[field.Name] = fieldValue;
                }
            }

            // Ensure Scene & Type are filled. Position is optional and wont be included if null
            saveData.SceneName = SceneManager.GetActiveScene().name;
            saveData.Type = objType.Name;
            return saveData;
        }

        public void LoadFields(object obj, CustomSaveData saveData)
        {
            if (obj == null || saveData == null) return;

            Type objType = obj.GetType();
            FieldInfo[] fields = GetSaveFieldsForType(objType);

            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                if (saveData.savedFields.TryGetValue(field.Name, out object savedValue))
                {
                    // Convert Ints
                    if (field.FieldType == typeof(int) && savedValue is long lVal)
                    {
                        savedValue = Convert.ToInt32(lVal);
                    }
                    // Convert Floats
                    else if (field.FieldType == typeof(float) && savedValue is double dVal)
                    {
                        savedValue = Convert.ToSingle(dVal);
                    }
                    // Handle Item_SO cases, since we cannot directly serialize-deserialize them
                    else if (typeof(Item_SO).IsAssignableFrom(field.FieldType))
                    {
                        if (savedValue is string itemName)
                        {
                            Item_SO item = ItemRegistry.GetItemByName(itemName);
                            field.SetValue(obj, item);
                            continue;
                        }
                    }

                    // Set the field value after conversion
                    field.SetValue(obj, savedValue);
                }
            }

            // Handle scene name
            FieldInfo sceneNameField = GetSceneNameField(objType);
            if (sceneNameField != null && saveData.savedFields.TryGetValue("SceneName", out object sceneVal))
            {
                sceneNameField.SetValue(obj, sceneVal);
            }
        }

        // Check if an object is serializable with type-level caching
        private bool IsSerializable(object obj)
        {
            if (obj == null) return true;

            Type type = obj.GetType();

            if (cachedSerializableTypes.TryGetValue(type, out bool isCachedSerializable))
            {
                if (!isCachedSerializable) return false;
            }
            else
            {
                bool serializable = type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal) || type.IsSerializable;
                cachedSerializableTypes[type] = serializable;
                if (!serializable) return false;
            }

            if (obj is IEnumerable enumerable)
            {
                foreach (var item in enumerable)
                {
                    if (item != null && !IsSerializable(item))
                        return false;
                }
            }

            return true;
        }
    }
}