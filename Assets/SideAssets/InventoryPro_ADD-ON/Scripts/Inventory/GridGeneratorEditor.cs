#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace cowsins.Inventory
{
    [System.Serializable]
    [CustomEditor(typeof(GridGenerator))]
    public class GridGeneratorEditor : Editor
    {
        private string[] tabs = { "Main", "References", "Styling & Customization"};
        private int currentTab = 0;

        private int gridWidth = 5;
        private int gridHeight = 5;
        override public void OnInspectorGUI()
        {
            serializedObject.Update();
            GridGenerator myScript = target as GridGenerator;

            Texture2D myTexture = Resources.Load<Texture2D>("CustomEditor/gridGenerator_CustomEditor") as Texture2D;
            GUILayout.Label(myTexture);

            EditorGUILayout.BeginVertical();
            currentTab = GUILayout.Toolbar(currentTab, tabs);
            EditorGUILayout.Space(5f);
            EditorGUILayout.EndVertical();

            if (currentTab >= 0 || currentTab < tabs.Length)
            {
                switch (tabs[currentTab])
                {
                    case "Main":
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("inventoryGridData"));
                        break;
                    case "References":
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("inventoryManager"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("hotbarParent"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("buttonParent"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("chestParent"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("backpackParent"));
                        break;
                    case "Styling & Customization":
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("hotbarPadding"));
                        GUILayout.Space(10); 
            
                        EditorGUILayout.HelpBox("The Gap is the Distance between Slots. Below you can see an approximation of the final look.", MessageType.Info);
                        GUILayout.Space(5); 
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("gapX"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("gapY"));
                        ShowGridPreview(myScript);
                        break;

                }
            }

            EditorGUILayout.Space(10f);
            serializedObject.ApplyModifiedProperties();
        }

      private void ShowGridPreview(GridGenerator gridGen)
        {
            GUILayout.Space(20); 
            GUI.enabled = false;

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            float gapX = gridGen.GapX * 0.1f; 
            float gapY = gridGen.GapY * 0.1f; 
            float paddingX = gridGen.HotbarPadding.horizontal;
            float paddingY = gridGen.HotbarPadding.vertical;

            gridWidth = 5;
            gridHeight = 5;

            float width = (gridWidth * 50f) + ((gridWidth - 1) * gapX + paddingX * 2);
            float height = (gridHeight * 50f) + ((gridHeight - 1) * gapY + paddingY * 2);

            GUILayout.BeginVertical(GUILayout.Width(width), GUILayout.Height(height));

            for (int row = 0; row < gridHeight; row++)
            {
                GUILayout.BeginHorizontal();
                for (int col = 0; col < gridWidth; col++)
                {
                    GUILayout.Button($"Slot {row * gridWidth + col + 1}", GUILayout.Width(50), GUILayout.Height(50));
                    if (col < gridWidth - 1) GUILayout.Space(gapX);
                }
                GUILayout.EndHorizontal();
                if (row < gridHeight - 1) GUILayout.Space(gapY);
            }

            GUILayout.EndVertical();
            GUILayout.FlexibleSpace(); 
            GUILayout.EndHorizontal();
            GUI.enabled = true;
        }
    }
}
#endif