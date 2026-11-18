#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;

namespace cowsins.SaveLoad
{
    [InitializeOnLoad]
    public class ItemRegistryEditor : EditorWindow
    {
        private const string PrimaryRegistryPath = "Assets/Resources/ItemRegistry.asset";

        static ItemRegistryEditor()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                BuildItemRegistry();
            }
        }

        [MenuItem("Cowsins/Rebuild Item Registry", priority = 100)]
        public static void BuildItemRegistry()
        {
            string[] duplicateGuids = AssetDatabase.FindAssets("ItemRegistry t:ItemRegistry");
            foreach (string guid in duplicateGuids)
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(p) && p != PrimaryRegistryPath)
                {
                    Debug.Log($"<color=yellow>[COWSINS]</color> Removing duplicate ItemRegistry at: {p}");
                    AssetDatabase.DeleteAsset(p);
                }
            }

            ItemRegistry itemRegistry = AssetDatabase.LoadAssetAtPath<ItemRegistry>(PrimaryRegistryPath);

            if (itemRegistry == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                {
                    AssetDatabase.CreateFolder("Assets", "Resources");
                }

                itemRegistry = CreateInstance<ItemRegistry>();
                AssetDatabase.CreateAsset(itemRegistry, PrimaryRegistryPath);
                AssetDatabase.SaveAssets();
            }

            // Collect all Item_SO assets in the project
            List<Item_SO> itemList = new List<Item_SO>();
            HashSet<string> seenNames = new HashSet<string>();
            string[] guids = AssetDatabase.FindAssets("t:Item_SO");

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Item_SO item = AssetDatabase.LoadAssetAtPath<Item_SO>(path);
                if (item != null && !seenNames.Contains(item.name))
                {
                    seenNames.Add(item.name);
                    itemList.Add(item);
                }
            }

            itemList.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));

            // Assign the collected items to the ItemRegistry
            itemRegistry.items = itemList;
            itemRegistry.RebuildLookupTable();

            // Mark the ItemRegistry asset as dirty and save it
            EditorUtility.SetDirty(itemRegistry);
            AssetDatabase.SaveAssets();

            Debug.Log($"<color=green>[COWSINS]</color> ItemRegistry successfully built with {itemList.Count} items.");
        }
    }
}
#endif
