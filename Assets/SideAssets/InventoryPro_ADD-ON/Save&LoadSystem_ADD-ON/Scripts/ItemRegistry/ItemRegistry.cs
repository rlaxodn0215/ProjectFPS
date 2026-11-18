using UnityEngine;
using System.Collections.Generic;

namespace cowsins.SaveLoad
{
    /// <summary>
    /// The Item Registry is a collection of all Item_SOs in your Project. Save & Load requires to access the stored references.
    /// </summary>
    [System.Serializable]
    public class ItemRegistry : ScriptableObject
    {
        private static ItemRegistry instance;
        public List<Item_SO> items = new List<Item_SO>();

        // High-performance O(1) lookup cache
        private static readonly Dictionary<string, Item_SO> lookupTable = new Dictionary<string, Item_SO>();

        public static ItemRegistry Instance
        {
            get
            {
                if (instance == null) Initialize();
                return instance;
            }
        }

        private void OnEnable()
        {
            RebuildLookupTable();
        }

        static void Initialize()
        {
            instance = Resources.Load<ItemRegistry>("ItemRegistry");

            if (instance == null)
            {
                instance = CreateInstance<ItemRegistry>();
            }

            instance.RebuildLookupTable();
        }

        public void RebuildLookupTable()
        {
            lookupTable.Clear();
            if (items == null) return;

            for (int i = 0; i < items.Count; i++)
            {
                Item_SO item = items[i];
                if (item != null && !string.IsNullOrEmpty(item.name))
                {
                    lookupTable[item.name] = item;
                }
            }
        }

        /// <summary>
        /// Runtime access to get an item by its name
        /// </summary>
        public static Item_SO GetItemByName(string itemName)
        {
            if (string.IsNullOrEmpty(itemName)) return null;

            // Ensure instance and lookup are populated
            if (lookupTable.Count == 0 && Instance.items != null && Instance.items.Count > 0)
            {
                Instance.RebuildLookupTable();
            }

            if (lookupTable.TryGetValue(itemName, out Item_SO cachedItem) && cachedItem != null)
            {
                return cachedItem;
            }

            // If not found in lookupTable, search items list
            if (Instance.items != null)
            {
                for (int i = 0; i < Instance.items.Count; i++)
                {
                    Item_SO item = Instance.items[i];
                    if (item != null && item.name == itemName)
                    {
                        lookupTable[itemName] = item;
                        return item;
                    }
                }
            }

#if UNITY_EDITOR
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:Item_SO");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[i]);
                Item_SO foundItem = UnityEditor.AssetDatabase.LoadAssetAtPath<Item_SO>(path);
                if (foundItem != null && foundItem.name == itemName)
                {
                    if (Instance.items != null && !Instance.items.Contains(foundItem))
                    {
                        Instance.items.Add(foundItem);
                        UnityEditor.EditorUtility.SetDirty(Instance);
                        UnityEditor.AssetDatabase.SaveAssets();
                    }
                    lookupTable[itemName] = foundItem;
                    Debug.Log($"<color=green>[COWSINS]</color> ItemRegistry: Auto-registered missing item '<b>{itemName}</b>' from project assets.");
                    return foundItem;
                }
            }
#endif

            return null;
        }
    }
}