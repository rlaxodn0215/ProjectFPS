#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace cowsins.Inventory
{
    [System.Serializable]
    [CustomEditor(typeof(ToastManager))]
    public class ToastManagerEditor : Editor
    {
        private string[] tabs = { "Main", "Toast Configuration","Messages Customization"};
        private int currentTab = 0;

        override public void OnInspectorGUI()
        {
            serializedObject.Update();
            ToastManager myScript = target as ToastManager;

            Texture2D myTexture = Resources.Load<Texture2D>("CustomEditor/toastManager_CustomEditor") as Texture2D;
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
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("toastPrefab"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("toastPosition"));
                        break;
                    case "Toast Configuration":
#if INVENTORY_PRO_ADD_ON
                        GUI.enabled = true;
#else 
                        EditorGUILayout.HelpBox("These options are restricted to the Inventory Pro Add-On.", MessageType.Info);
                        GUI.enabled = false;
#endif
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("showToastOnTrade"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("showAmountTraded"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("showToastOnInsufficientSpace"));
                        break;
                    case "Messages Customization":
#if INVENTORY_PRO_ADD_ON
                        GUI.enabled = true;
#else 
                        EditorGUILayout.HelpBox("These options are restricted to the Inventory Pro Add-On.", MessageType.Info);
                        GUI.enabled = false;
#endif
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("collectedMsg"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("purchaseMsg"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("inventoryIsFullMsg"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("alreadyPinnedToastMsg"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("itemPinnedToastMsg"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("attachmentNotCompatibleMsg"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("attachmentSuccessfullyAddedMsg"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("weaponIsAlreadyUnholsteredMsg"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("itemDeleted"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("itemBroke"));

                        GUI.enabled = true;
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("fullHealthMsg"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("enableAutoSaveMsg"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("disableAutoSaveMsg"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("dataPersistenceManagerNotAvailableMsg"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("gameLoaded"));
                        break;

                }
            }

            EditorGUILayout.Space(10f);
            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif