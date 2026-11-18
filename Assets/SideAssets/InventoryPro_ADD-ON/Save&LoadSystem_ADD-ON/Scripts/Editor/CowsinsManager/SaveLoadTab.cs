#if UNITY_EDITOR

using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;
using System;
using UnityEditor;

namespace cowsins.SaveLoad
{
    public class SaveLoadTab : ITab
    {
        // CONSTANTS 
        public string TabName => "Save & Load";
        public int Order => 10;
        public bool IsVisible => true;

        // GUI-RELATED 
        private Vector2 scrollPos;
        private int selectedTab = 0;
        private readonly string[] tabs = { "Main", "Settings", "Help" };
        private GUIStyle bigHeadingStyle;
        private bool showFullJson = false;
        private bool showPlayerData = false;
        private bool showInventoryData = false;
        private bool showWeaponData = false;
        private bool showObjectsData = false;

        // SAVE & LOAD SYSTEM
        private int selectedSlot = -1;
        private string[] slotDirectories;
        private string fileContents;

        // JSON & DATA MANAGEMENT
        private JObject jsonData;

        // SERIALIZATION
        private SerializedObject serializedDataPersistence;
        private DataPersistence_SO dataPersistenceSO;

        // Initialize Save & Load Tab
        public void StartTab()
        {
            // Data Persistence SO defines Save & Load Settings, we need to retrieve it from Resources first
            dataPersistenceSO = Resources.Load<DataPersistence_SO>("DataPersistence_SO");
            if (dataPersistenceSO != null)
            {
                serializedDataPersistence = new SerializedObject(dataPersistenceSO);
            }
            if (dataPersistenceSO == null)
            {
                Debug.LogError("<color=red>[COWSINS]</color> <b><color=yellow>DataPersistence_SO not found!</color></b> " +
                "Please, ensure <b><color=cyan>DataPersistence_SO</color></b> is located in the Resources folder and properly named.");
            }

            bigHeadingStyle = CowsinsEditorWindowUtilities.BigHeadingStyle();

            // Initially, load the first save slot (if available)
            selectedSlot = 0;
            RefreshSlotFolders();
            if (slotDirectories != null && slotDirectories.Length > 0)
                LoadSlotData(slotDirectories[selectedSlot]);
        }

        public void OnGUI()
        {
            // If, for whatever reason, slot directories is null, re-initialize the Tab to ensure the directories are populated.
            if (slotDirectories == null) StartTab();

            EditorGUILayout.BeginHorizontal();

            // Vertical Tab Buttons
            LeftPanel();

            // Save Slots Content
            RightPanel();

            EditorGUILayout.EndHorizontal();
        }
        #region PANELS

        private void LeftPanel()
        {
            EditorGUILayout.BeginVertical(GUI.skin.GetStyle("HelpBox"), GUILayout.Width(100));
            EditorGUILayout.LabelField("Save & Load Manager", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);
            for (int i = 0; i < tabs.Length; i++)
            {
                if (GUILayout.Toggle(selectedTab == i, tabs[i], "Button"))
                {
                    selectedTab = i;
                }
                EditorGUILayout.Space(3);
            }
            EditorGUILayout.EndVertical();
        }

        private void RightPanel()
        {
            EditorGUILayout.BeginVertical();

            switch (selectedTab)
            {
                case 0: DrawSaveSlotsTab(); break;
                case 1: DrawSettingsTab(); break;
                case 2: DrawHelpTab(); break;
            }

            EditorGUILayout.EndVertical();
        }
        #endregion

        #region TABS
        private void DrawSaveSlotsTab()
        {
            EditorGUILayout.BeginHorizontal();

            // LEFT SECTION - SAVE SLOTS LIST
            EditorGUILayout.BeginVertical(GUILayout.Width(EditorGUIUtility.currentViewWidth / 4));

            // Display the Persistent Data Path for Reference. Allows to easily locate where the save slots are located in your computer.
            EditorGUILayout.LabelField("Persistent Path:", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            // Disable the TextArea so it cannot be edited.
            GUI.enabled = false;
            EditorGUILayout.TextArea(Application.persistentDataPath, GUILayout.Width(EditorGUIUtility.currentViewWidth / 4));
            GUI.enabled = true;
            // Button to copy the persistent path to clipboard
            if (GUILayout.Button("Copy Path"))
            {
                GUIUtility.systemCopyBuffer = Application.persistentDataPath;
                Debug.Log("<color=green>[COWSINS]</color> Persistent path copied to clipboard.");
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // SAVE SLOTS LISTING
            EditorGUILayout.LabelField("Save Slots:", EditorStyles.boldLabel);

            if (slotDirectories == null || slotDirectories.Length <= 0)
            {
                EditorGUILayout.LabelField("Oops, no Save Slots were found.");
            }
            else
            {
                for (int i = 0; i < slotDirectories.Length; i++)
                {
                    string folderName = Path.GetFileName(slotDirectories[i]);

                    if (GUILayout.Button($"Slot {folderName}"))
                    {
                        selectedSlot = i;
                        LoadSlotData(slotDirectories[i]);
                    }
                }
            }

            EditorGUILayout.Space(15);

            if (GUILayout.Button("Refresh Slots"))
            {
                RefreshSlotFolders();
            }

            EditorGUILayout.EndVertical();

            // RIGHT SECTION - SELECTED SAVE SLOT DATA CONTENT
            EditorGUILayout.BeginVertical();

            EditorGUILayout.BeginHorizontal();
            if (jsonData != null)
            {
                GUI.enabled = !Application.isPlaying;
                EditorGUILayout.LabelField(!Application.isPlaying ? $"Contents of Slot {selectedSlot}" : $"Can't delete Slot {selectedSlot} in playmode", EditorStyles.boldLabel);

                if (GUILayout.Button($"Delete Slot {selectedSlot}", GUILayout.MaxWidth(200)))
                {
                    DeleteSelectedSlot();
                }
                GUI.enabled = true;
            }
            else EditorGUILayout.LabelField("Select a Slot to Inspect its Data", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            // Display selected save slot content if it is not null. 
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            if (jsonData != null)
            {
                // Displays the entire Json content
                EditorGUILayout.BeginVertical(GUI.skin.GetStyle("HelpBox"));
                showFullJson = EditorGUILayout.Foldout(showFullJson, "Full JSON");
                if (showFullJson)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.HelpBox($"Filters do not affect Full Json", MessageType.Info);
                    GUI.enabled = false;
                    EditorGUILayout.TextArea(jsonData.ToString(), GUILayout.ExpandHeight(true));
                    GUI.enabled = true;
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.EndVertical();

                // Displays Player Data extracted from the entire Json
                EditorGUILayout.BeginVertical(GUI.skin.GetStyle("HelpBox"));
                showPlayerData = EditorGUILayout.Foldout(showPlayerData, "Player Data");
                if (showPlayerData)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField("Player Position:", EditorStyles.boldLabel);
                    DisplayField("Player Position", "player.playerTransforms", "");
                    EditorGUILayout.LabelField("Health:", EditorStyles.boldLabel);
                    DisplayField("Health", "player.playerHealth", "");
                    EditorGUILayout.LabelField("Shield:", EditorStyles.boldLabel);
                    DisplayField("Shield", "player.playerShield", "");
                    EditorGUILayout.LabelField("Level:", EditorStyles.boldLabel);
                    DisplayField("Level", "player.playerLevel", "");
                    EditorGUILayout.LabelField("Coins:", EditorStyles.boldLabel);
                    DisplayField("Coins", "player.coins", "");
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.EndVertical();

                // Displays Inventory Data extracted from the entire Json
                EditorGUILayout.BeginVertical(GUI.skin.GetStyle("HelpBox"));
                showInventoryData = EditorGUILayout.Foldout(showInventoryData, "Inventory Data");
                if (showInventoryData)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField("Hotbar Items:", EditorStyles.boldLabel);
                    DisplayField("Hotbar Items", "player.serializableHotbarData", "");
                    EditorGUILayout.LabelField("Inventory Items:", EditorStyles.boldLabel);
                    DisplayField("Inventory Items", "inventory.serializableInventoryData", "");
                    EditorGUILayout.LabelField("Fav Items:", EditorStyles.boldLabel);
                    DisplayField("Fav Items", "inventory.favItems", "");
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.EndVertical();

                // Displays Weapon Data extracted from the entire Json
                EditorGUILayout.BeginVertical(GUI.skin.GetStyle("HelpBox"));
                showWeaponData = EditorGUILayout.Foldout(showWeaponData, "Weapon Data");
                if (showWeaponData)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField("Current Weapon:", EditorStyles.boldLabel);
                    DisplayField("Current Weapon", "player.currentWeaponInt", "");
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.EndVertical();

                // Displays Game World extracted from the entire Json
                EditorGUILayout.BeginVertical(GUI.skin.GetStyle("HelpBox"));
                showObjectsData = EditorGUILayout.Foldout(showObjectsData, "Game World Objects");
                if (showObjectsData)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField("Triggered Objects:", EditorStyles.boldLabel);
                    DisplayField("Triggered Objects", "world.triggeredObjects", "");
                    EditorGUILayout.LabelField("Interactable Objects:", EditorStyles.boldLabel);
                    DisplayField("Interactable Objects", "world.interactablesObjects", "");
                    EditorGUILayout.LabelField("Enemy Objects:", EditorStyles.boldLabel);
                    DisplayField("Enemy Objects", "world.enemyObjects", "");
                    EditorGUILayout.LabelField("Destructible Objects:", EditorStyles.boldLabel);
                    DisplayField("Destructible Objects", "world.destructibleObjects", "");
                    EditorGUILayout.LabelField("Custom Objects:", EditorStyles.boldLabel);
                    DisplayField("Custom Objects", "world.customObjects", "");
                    EditorGUILayout.LabelField("Instantiated Objects:", EditorStyles.boldLabel);
                    DisplayField("Instantiated Objects", "world.instantiatedObjects", "");
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }
        private void DrawSettingsTab()
        {
            GUILayout.Label("Save & Load Settings", bigHeadingStyle);

            // Settings are based on Data Persistence SO. If it doesn't exist, stop here.
            if (dataPersistenceSO == null) return;

            // Header
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Data Persistence Settings", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Data Persistence Settings stores the current name of the assigned Scenes. If the naming of any assigned scene changes, please visit this settings section and click on 'Save Changes' to update the name accordingly.", MessageType.Info);
            if (dataPersistenceSO.nonSaveableSceneNames.Contains(dataPersistenceSO.FirstLevelName))
                EditorGUILayout.HelpBox("This scene is part of the Non-Saveable Scenes list. You may experience unexpected issues, especially when trying to save/load your game or access the Inventory (if available with Inventory Pro Add-On).", MessageType.Warning);

            EditorGUILayout.Space();

            // Display Data Persistence Settings
            SerializedProperty property = serializedDataPersistence.GetIterator();
            property.NextVisible(true);

            while (property.NextVisible(false))
            {
                if (property.name == "autoSaveTimeSeconds" && serializedDataPersistence.FindProperty("autoSave").boolValue == false
                    || property.name == "toastMessage" && serializedDataPersistence.FindProperty("showToastOnGameSaved").boolValue == false) continue;
                EditorGUILayout.PropertyField(property, true);
            }

            EditorGUILayout.Space();

            // Once the first level is defined, changing the name may cause a mismatch between the scene and the stored name. 
            if (dataPersistenceSO == null ||
                string.IsNullOrEmpty(dataPersistenceSO.FirstLevelName) ||
                dataPersistenceSO.FirstLevelScene == null ||
                dataPersistenceSO.FirstLevelName != dataPersistenceSO.FirstLevelScene.name)
            {
                EditorGUILayout.HelpBox("First Level Scene name mismatch occurred, please click on 'Save Changes'.", MessageType.Error);
            }

            // Changes should happen automatically, however, this button ensures it correctly saves.
            if (GUILayout.Button("Save Changes"))
            {
                EditorUtility.SetDirty(dataPersistenceSO);
                AssetDatabase.SaveAssets();
            }

            serializedDataPersistence.ApplyModifiedProperties();
        }

        private void DrawHelpTab()
        {
            GUIStyle wrapStyle = new GUIStyle(GUI.skin.label);
            wrapStyle.wordWrap = true;

            GUILayout.Label("Help & Tutorials", bigHeadingStyle);

            GUILayout.Space(5);
            EditorGUILayout.BeginVertical(GUI.skin.GetStyle("HelpBox"));
            {
                if (GUILayout.Button("[RELATED DOCUMENTATION] WORKING WITH SAVE & LOAD IN COWSINS MANAGER", EditorStyles.linkLabel))
                {
                    Application.OpenURL("https://cowsinss-organization.gitbook.io/save-and-load-add-on/how-to-use-and-guides/working-with-save-and-load-in-cowsins-manager");
                }
                GUILayout.Space(5);
                GUILayout.Label("Save & Load Add-On provides a Powerful tool inside the Cowsins Manager, " +
                    "which you can use to visualize different Save Slots, Delete them, access persistent path in your computer, " +
                    "read decrypted Json files & configure Save & Load parameters!", wrapStyle);
            }
            EditorGUILayout.EndVertical();

            GUILayout.Space(5);
            EditorGUILayout.HelpBox("Cowsins is currently facing some issues with the YouTube channel. Tutorials have been backed up on cowsins.com.", MessageType.Warning);
            GUILayout.Space(5);

            GUILayout.BeginHorizontal();

            GUI.backgroundColor = new Color(.5f, .5f, .5f, 1);

            CowsinsEditorWindowUtilities.DrawLinkCard(Resources.Load<Texture2D>("CustomEditor/CowsinsManager/documentationIcon"), "Documentation", "https://cowsinss-organization.gitbook.io/save-and-load-add-on", .7f, .3f);
            GUILayout.FlexibleSpace();
            CowsinsEditorWindowUtilities.DrawLinkCard(Resources.Load<Texture2D>("CustomEditor/CowsinsManager/tutorialsIcon"), "Tutorials", "https://www.cowsins.com/", .7f, .3f);
            GUILayout.FlexibleSpace();
            CowsinsEditorWindowUtilities.DrawLinkCard(Resources.Load<Texture2D>("CustomEditor/CowsinsManager/supportIcon"), "Support", "https://discord.gg/759gSeTT9m", .7f, .3f);
            GUILayout.Space(10);

            GUILayout.EndHorizontal();
        }
        #endregion

        #region UTILITIES
        private void DisplayField(string label, string jsonKey, string prefix)
        {
            if (jsonData != null)
            {
                JToken token = jsonData.SelectToken(jsonKey);
                if (token == null && !jsonKey.Contains("."))
                {
                    token = jsonData.SelectToken($"player.{jsonKey}")
                         ?? jsonData.SelectToken($"inventory.{jsonKey}")
                         ?? jsonData.SelectToken($"world.{jsonKey}");
                }

                if (token != null)
                {
                    GUI.enabled = false;
                    EditorGUILayout.TextArea(prefix + token.ToString(), GUILayout.ExpandHeight(false));
                    GUI.enabled = true;
                    return;
                }
            }

            EditorGUILayout.LabelField(label, "N/A");
        }

        // Loads fileContents and Json Data 
        private void LoadSlotData(string folderPath)
        {
            string filePath = Path.Combine(folderPath, "data.json");

            if (File.Exists(filePath))
            {
                // Read the text and safely decrypt it. 
                // EncryptDecryptAESEditorOnly is only accessible in the Editor. Therefore, EncryptDecryptAESEditorOnly is not included in the build of your game, so
                // not-allowed users cannot use it to decrypt your data and access Save Slots information.
                fileContents = File.ReadAllText(filePath);
                FileDataHandler fileDataHandler = new FileDataHandler($"{dataPersistenceSO.FileName}.json");
                fileContents = fileDataHandler.EncryptDecryptAESEditorOnly(fileContents);
                // "Dispose" fileDataHandler after fileContents have been decrypted.
                fileDataHandler = null;
                jsonData = JObject.Parse(fileContents);
            }
            else
            {
                fileContents = "No data.json file found in this slot.";
                jsonData = null;
            }
        }

        // Populate Slot Directories
        private void RefreshSlotFolders()
        {
            string persistentPath = Application.persistentDataPath;

            if (Directory.Exists(persistentPath))
            {
                slotDirectories = Directory.GetDirectories(persistentPath);
            }
            else
            {
                slotDirectories = new string[0];
            }

            fileContents = string.Empty;
            jsonData = null;
        }

        private void DeleteSelectedSlot()
        {
            if (selectedSlot < 0 || selectedSlot >= slotDirectories.Length)
            {
                Debug.LogWarning("No slot selected or invalid slot index.");
                return;
            }

            string folderPath = slotDirectories[selectedSlot];

            // Confirmation dialog
            bool acceptDelete = EditorUtility.DisplayDialog(
                "Delete Save Slot",
                $"Are you sure you want to delete the save slot: {Path.GetFileName(folderPath)}?",
                "Yes", "No");

            if (acceptDelete)
            {
                try
                {
                    Directory.Delete(folderPath, true);

                    // Refresh the slot list
                    RefreshSlotFolders();

                    // Confirmation dialog
                    bool confirmDelete = EditorUtility.DisplayDialog(
                        $"Slot Deleted.",
                        $"Slot {selectedSlot} has been successfully deleted.",
                        "Ok");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"<color=red>[COWSINS]</color> Error deleting slot: {ex.Message}");
                }
            }
        }
        #endregion
    }
}
#endif
