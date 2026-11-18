#if UNITY_EDITOR

using UnityEditor;

namespace cowsins.SaveLoad
{
    /// <summary>
    /// When a file in your project is imported, deleted or moved, we need to check if it was an Item_SO, and properly reflect the changes into the ItemRegistry.
    /// </summary>
    public class ItemRegistryAssetProcessor : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            bool shouldRebuild = false;

            // Check each asset that has been imported or deleted, since we don´t mind if Item_SOs are moved within the project.
            foreach (string assetPath in importedAssets)
            {
                if (AssetDatabase.LoadAssetAtPath<Item_SO>(assetPath) != null)
                {
                    shouldRebuild = true;
                    break;
                }
            }

            foreach (string assetPath in deletedAssets)
            {
                
                System.Type assetType = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
                if (assetType != null && typeof(Item_SO).IsAssignableFrom(assetType))
                {
                    shouldRebuild = true;
                    break;
                }
            }

            if (shouldRebuild)
            {
                // Rebuilds the entire Item Registry
                ItemRegistryEditor.BuildItemRegistry();
            }
        }
    }
}
#endif
