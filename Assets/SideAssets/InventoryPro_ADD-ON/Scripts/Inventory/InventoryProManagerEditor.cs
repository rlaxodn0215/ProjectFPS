#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace cowsins.Inventory
{
    [System.Serializable]
    [CustomEditor(typeof(InventoryProManager))]
    public class InventoryProManagerEditor : Editor
    {
        private string[] tabs = { "Main", "References","Sounds","Navigation & Inputs", "Events" };
        private int currentTab = 0;

        override public void OnInspectorGUI()
        {
            serializedObject.Update();
            InventoryProManager myScript = target as InventoryProManager;

            Texture2D myTexture = Resources.Load<Texture2D>("CustomEditor/inventoryProManager_CustomEditor") as Texture2D;
            GUILayout.Label(myTexture);

            // Check essential references
            if (myScript.PlayerDependencies == null || myScript._TooltipManager == null || myScript.WeaponGenericPickeable == null ||
                myScript.AttachmentGenericPickeable == null || myScript.BulletsGenericPickeable == null || myScript.ItemGenericPickeable == null)
            {
                EditorGUILayout.HelpBox("A required reference is null. Please assign the reference in the �References� tab.", MessageType.Error);
            }

            EditorGUILayout.BeginVertical();
            currentTab = GUILayout.Toolbar(currentTab, tabs);
            EditorGUILayout.Space(10f);
            EditorGUILayout.EndVertical();

            if (currentTab >= 0 || currentTab < tabs.Length)
            {
                switch (tabs[currentTab])
                {
                    case "Main":
                        EditorGUILayout.LabelField("MAIN SETTINGS", EditorStyles.boldLabel);
                        EditorGUILayout.HelpBox("New Options available under �References�tab.", MessageType.Info);
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("inventoryStyle"));
                        EditorGUI.indentLevel++;
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("gridGenerator"));
                        if(myScript.UseFavouritesRadialMenu)
                        {
                            EditorGUILayout.HelpBox("If the Favorites Radial Menu is used, Slot Right Click Events can only be assigned to the Context Menu. The settings have been updated accordingly.", MessageType.Warning);
                            myScript.SetRightClickEvent(InventoryProManager.SlotRightClickEvent.ContextMenu); 
                            GUI.enabled = false; 
                            EditorGUILayout.PropertyField(serializedObject.FindProperty("slotRightClickEvent"));
                            GUI.enabled = true; 
                        }
                        else 
                            EditorGUILayout.PropertyField(serializedObject.FindProperty("slotRightClickEvent"));
                        if(myScript._SlotRightClickEvent == InventoryProManager.SlotRightClickEvent.ContextMenu)
                        {
                            EditorGUI.indentLevel++;
                            EditorGUILayout.PropertyField(serializedObject.FindProperty("contextMenu"));
                            EditorGUILayout.PropertyField(serializedObject.FindProperty("contextMenuOffset"));
                            EditorGUILayout.PropertyField(serializedObject.FindProperty("contextMenuRaycastPadding"));
                            EditorGUI.indentLevel--;
                        }
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("useFavouritesRadialMenu"));
                        if (myScript.UseFavouritesRadialMenu)
                        {
                            EditorGUI.indentLevel++;
                            EditorGUILayout.PropertyField(serializedObject.FindProperty("favItemsMenu"));
                            EditorGUILayout.PropertyField(serializedObject.FindProperty("rightClickUnpinsItem"));
                            EditorGUI.indentLevel--;
                        }
                        GUILayout.Space(10);
                        EditorGUILayout.LabelField("CUSTOMIZATION", EditorStyles.boldLabel);
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("storeWeaponsIfHotbarFull"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("dragMode"));
                        if (myScript._InventoryStyle == InventoryProManager.InventoryStyle.Tetris)
                        {
                            EditorGUILayout.PropertyField(serializedObject.FindProperty("rotationDirections"));
                        }
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("showToastOnItemBreak"));
                        break;
                    case "References":
                        EditorGUILayout.LabelField("General References", EditorStyles.boldLabel);
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("playerDependencies"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("tooltipManager"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("weaponGenericPickeable"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("attachmentGenericPickeable"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("bulletsGenericPickeable"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("itemGenericPickeable"));

                        GUILayout.Space(10); 
                        EditorGUILayout.LabelField("Device Specific UI", EditorStyles.boldLabel);
                        EditorGUILayout.HelpBox("You can leave these unassigned if you do not require them.", MessageType.None);
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("closeButton"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("trashButton"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("gamepadSpecificUI"));

                        GUILayout.Space(10);
                        EditorGUILayout.LabelField("Slots Colors & Style", EditorStyles.boldLabel);
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("highlightSlotColor"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("availableSlotColor"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("occupiedSlotColor"));

                        GUILayout.Space(10); 
                        EditorGUILayout.LabelField("Examination", EditorStyles.boldLabel);
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("examinationUI"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("examinationText"));

                        GUILayout.Space(10);
                        EditorGUILayout.LabelField("Chests", EditorStyles.boldLabel);
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("allowLootAllChest"));
                        if(myScript.AllowLootAllChest)
                        {
                            EditorGUI.indentLevel++;
                            EditorGUILayout.PropertyField(serializedObject.FindProperty("lootAllChestButton"));
                            EditorGUI.indentLevel--;
                        }
                        break;
                    case "Sounds":
                        EditorGUILayout.LabelField("SOUND EFFECTS", EditorStyles.boldLabel);
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("openInventorySFX"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("closeInventorySFX"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("highlightItemSFX"));
                        if(myScript._InventoryStyle == InventoryProManager.InventoryStyle.Tetris) EditorGUILayout.PropertyField(serializedObject.FindProperty("rotateItemSFX"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("placeItemSFX"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("contextMenuClickSFX"));
                        break;
                    case "Navigation & Inputs":
                        EditorGUILayout.HelpBox("Adjust the UI Controls & Input Actions.", MessageType.Info);
                        EditorGUILayout.Space(10);
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("invInputActions"));
                        break;
                    case "Events":
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("events"));
                        break;

                }
            }

            EditorGUILayout.Space(10f);
            serializedObject.ApplyModifiedProperties();

        }
    }
}
#endif