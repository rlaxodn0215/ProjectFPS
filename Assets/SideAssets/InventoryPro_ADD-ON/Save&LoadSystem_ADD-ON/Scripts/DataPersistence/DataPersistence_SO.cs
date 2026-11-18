#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using System.Collections.Generic;

namespace cowsins.SaveLoad
{
    /// <summary>
    /// Defines General Settings for the Save & Load System.
    /// These settings can be configured in the Save & Load Tab of the Cowsins Manager.
    /// </summary>
    public class DataPersistence_SO : ScriptableObject
    {
        // PUBLIC GETTERS
#if UNITY_EDITOR
        public SceneAsset FirstLevelScene => firstLevelScene;
#endif
        public string FirstLevelName => firstLevelName;
        public string FileName => fileName;
        public bool UseEncryption => useEncryption;
        public bool SavePlayerTransforms => savePlayerTransforms;
        public bool SavePlayerStats => savePlayerStats;
        public bool SaveExperience => saveExperience;
        public bool SaveCoins => saveCoins;
        public bool SaveHotbar => saveHotbar;
        public bool SaveInventory => saveInventory;
        public bool SaveObjects => saveObjects;
        public bool AutoSave => autoSave;
        public float AutoSaveTimeSeconds => autoSaveTimeSeconds;
        public bool ShowToastOnGameSaved => showToastOnGameSaved;
        public string ToastMessage => toastMessage;

        // VARIABLES
#if UNITY_EDITOR
        [SerializeField, Tooltip("When starting a new game, this is the first level to play.")] private SceneAsset firstLevelScene;
#endif
        [SerializeField, HideInInspector] private string firstLevelName;

        [Header("File Storage Config")]
        [SerializeField, Tooltip("Assign a name to the file that contains Save Slot Data")] private string fileName;
        [SerializeField, Tooltip("If enabled, Save Files will be encrypted with AES. When disabled, save files are stored as plain readable JSON.")] private bool useEncryption = true;
        [Header("Global Save Data")][SerializeField, Tooltip("If enabled, Player Health & Shield will be saved")] private bool savePlayerStats = true;
        [SerializeField, Tooltip("If enabled, Player Level & Experience will be saved")] private bool saveExperience = true;
        [SerializeField, Tooltip("If enabled, Player currency will be saved")] private bool saveCoins = true;
        [SerializeField, Tooltip("If enabled, Weapons in the Hotbar will be saved.")] private bool saveHotbar = true;
        [SerializeField, Tooltip("If enabled & The Inventory Pro Add-On is installed, the entire inventory will be saved.")] private bool saveInventory = true;
        [Header("Scene-Specific Save Data")]
        [SerializeField, Tooltip("If enabled, Player position and orientation will be saved")] private bool savePlayerTransforms = true;
        [SerializeField, Tooltip("If enabled, Custom Save Data will be saved.")] private bool saveObjects = true;

        [Header("Auto Saving Configuration")]
        [SerializeField, Tooltip("Enables or disables automatic game saving at regular intervals.")] private bool autoSave;
        [SerializeField, Tooltip("Time interval between automatic game saves.")] private float autoSaveTimeSeconds = 60f;


        [Header("Toast Settings")]
        [SerializeField, Tooltip("Enables or disables Toast Messages when the game is saved.")] private bool showToastOnGameSaved;
        [SerializeField, Tooltip("Toast Message to display on the game being saved.")] private string toastMessage;

        [HideInInspector] public List<string> nonSaveableSceneNames = new List<string>();
        [HideInInspector] public List<string> excludedScenesFromSpecificSaveDataNames = new List<string>();

        // This code only works in the Editor, & it is responsible for translating the SceneAssets assigned by the user to strings based on the scene names.
#if UNITY_EDITOR
        [SerializeField, Header("Non-Saveable Scenes")] private List<SceneAsset> nonSaveableScenes = new List<SceneAsset>();
        [SerializeField, Header("Scenes Excluded from Scene-Specific Save Data")] private List<SceneAsset> excludedScenesFromSpecificSaveData = new List<SceneAsset>();

        private void OnValidate()
        {
            // Ensure the first level scene is not null and then store its name string
            if (firstLevelScene != null) firstLevelName = firstLevelScene.name;

            nonSaveableSceneNames.Clear();
            foreach (var scene in nonSaveableScenes)
            {
                if (scene != null)
                {
                    nonSaveableSceneNames.Add(scene.name);
                }
            }
            excludedScenesFromSpecificSaveDataNames.Clear();
            foreach (var scene in excludedScenesFromSpecificSaveData)
            {
                if (scene != null)
                {
                    excludedScenesFromSpecificSaveDataNames.Add(scene.name);
                }
            }
        }
#endif
    }
}