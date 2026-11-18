using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using cowsins;

#if INVENTORY_PRO_ADD_ON
using cowsins.Inventory;
#endif

namespace cowsins.SaveLoad
{
    public class GameDataManager : MonoBehaviour
    {
        public static GameDataManager instance { get; private set; }

        // MODULE REGISTRY
        private readonly List<ISaveModule> saveModules = new List<ISaveModule>();

        // CORE MODULES
        public PlayerSaveModule PlayerModule { get; private set; }
        public InventoryProSaveModule InventoryModule { get; private set; }
        public WorldObjectsSaveModule WorldObjectsModule { get; private set; }

        public Dictionary<string, CustomSaveData> triggersData
        {
            get => WorldObjectsModule != null ? WorldObjectsModule.triggersData : _fallbackTriggers;
            set { if (WorldObjectsModule != null) WorldObjectsModule.triggersData = value; else _fallbackTriggers = value; }
        }
        public Dictionary<string, CustomSaveData> interactablesData
        {
            get => WorldObjectsModule != null ? WorldObjectsModule.interactablesData : _fallbackInteractables;
            set { if (WorldObjectsModule != null) WorldObjectsModule.interactablesData = value; else _fallbackInteractables = value; }
        }
        public Dictionary<string, CustomSaveData> enemyHealthData
        {
            get => WorldObjectsModule != null ? WorldObjectsModule.enemyHealthData : _fallbackEnemies;
            set { if (WorldObjectsModule != null) WorldObjectsModule.enemyHealthData = value; else _fallbackEnemies = value; }
        }
        public Dictionary<string, CustomSaveData> destructiblesData
        {
            get => WorldObjectsModule != null ? WorldObjectsModule.destructiblesData : _fallbackDestructibles;
            set { if (WorldObjectsModule != null) WorldObjectsModule.destructiblesData = value; else _fallbackDestructibles = value; }
        }
        public Dictionary<string, CustomSaveData> customObjectsData
        {
            get => WorldObjectsModule != null ? WorldObjectsModule.customObjectsData : _fallbackCustomObjects;
            set { if (WorldObjectsModule != null) WorldObjectsModule.customObjectsData = value; else _fallbackCustomObjects = value; }
        }
        public Dictionary<string, CustomSaveData> instantiatedObjects
        {
            get => WorldObjectsModule != null ? WorldObjectsModule.instantiatedObjects : _fallbackInstantiated;
            set { if (WorldObjectsModule != null) WorldObjectsModule.instantiatedObjects = value; else _fallbackInstantiated = value; }
        }

        private Dictionary<string, CustomSaveData> _fallbackTriggers = new Dictionary<string, CustomSaveData>();
        private Dictionary<string, CustomSaveData> _fallbackInteractables = new Dictionary<string, CustomSaveData>();
        private Dictionary<string, CustomSaveData> _fallbackEnemies = new Dictionary<string, CustomSaveData>();
        private Dictionary<string, CustomSaveData> _fallbackDestructibles = new Dictionary<string, CustomSaveData>();
        private Dictionary<string, CustomSaveData> _fallbackCustomObjects = new Dictionary<string, CustomSaveData>();
        private Dictionary<string, CustomSaveData> _fallbackInstantiated = new Dictionary<string, CustomSaveData>();

        // PREFABS & TYPE TO PREFAB MAPPING
        [System.Serializable]
        public class TypeToPrefabMapping
        {
            public string TypeName;
            public Identifiable Prefab;
        }

        [SerializeField] private List<TypeToPrefabMapping> typeToPrefabMappings = new List<TypeToPrefabMapping>();
        private Dictionary<string, Identifiable> prefabDictionary = new Dictionary<string, Identifiable>();

        #region Initialization & Lifecycle
        private void Awake()
        {
            if (instance != null)
            {
                Destroy(this.gameObject);
                return;
            }

            instance = this;
            PopulatePrefabDictionary();
            InitializeCoreModules();
        }

        private void InitializeCoreModules()
        {
            PlayerModule = new PlayerSaveModule();
            RegisterSaveModule(PlayerModule);

            InventoryModule = new InventoryProSaveModule();
            RegisterSaveModule(InventoryModule);

            WorldObjectsModule = new WorldObjectsSaveModule(() => prefabDictionary);
            RegisterSaveModule(WorldObjectsModule);
        }

        private void OnEnable() => SceneManager.activeSceneChanged += OnSceneLoaded;
        private void OnDisable() => SceneManager.activeSceneChanged -= OnSceneLoaded;

        private void OnSceneLoaded(Scene oldScene, Scene newScene)
        {
            PlayerModule?.RefreshPlayerReferences();
            WorldObjectsModule?.ResetDictionaries();

            bool isInventoryAvailable = false;
#if INVENTORY_PRO_ADD_ON
            isInventoryAvailable = InventoryProManager.instance != null;
#endif
            var playerMovement = PlayerModule?.CurrentPlayerMovement;
            if (playerMovement != null && !isInventoryAvailable)
            {
                var interactManager = playerMovement.GetComponent<InteractManager>();
                if (interactManager != null)
                {
                    interactManager.userEvents.onDrop.RemoveAllListeners();
                    interactManager.userEvents.onDrop.AddListener(OnDropHotbar);
                }
            }
        }

        public void PopulatePrefabDictionary()
        {
            prefabDictionary = new Dictionary<string, Identifiable>();
            if (typeToPrefabMappings == null) return;

            for (int i = 0; i < typeToPrefabMappings.Count; i++)
            {
                var mapping = typeToPrefabMappings[i];
                if (mapping != null && !string.IsNullOrEmpty(mapping.TypeName) && mapping.Prefab != null)
                {
                    prefabDictionary[mapping.TypeName] = mapping.Prefab;
                }
            }
        }

        /// <summary>
        /// Registers a prefab by its unique PrefabId or TypeName at runtime
        /// </summary>
        public void RegisterPrefab(string key, Identifiable prefab)
        {
            if (!string.IsNullOrEmpty(key) && prefab != null) prefabDictionary[key] = prefab;
        }
        #endregion

        #region Module Registry
        /// <summary>
        /// Registers a new save module into the persistence pipeline
        /// </summary>
        public void RegisterSaveModule(ISaveModule module)
        {
            if (module != null && !saveModules.Contains(module)) saveModules.Add(module);
        }

        /// <summary>
        /// Unregisters an existing save module from the persistence pipeline.
        /// </summary>
        public void UnregisterSaveModule(ISaveModule module)
        {
            if (module != null)
            {
                saveModules.Remove(module);
            }
        }

        /// <summary>
        /// Retrieves a registered save module of type T.
        /// </summary>
        public T GetSaveModule<T>() where T : class, ISaveModule
        {
            for (int i = 0; i < saveModules.Count; i++)
            {
                if (saveModules[i] is T typedModule)
                {
                    return typedModule;
                }
            }
            return null;
        }
        #endregion

        #region Save & Load Orchestration
        public void SaveData(GameData gameData, DataPersistence_SO dataPersistenceSettings)
        {
            if (dataPersistenceSettings == null)
            {
                Debug.LogError("<color=red>[COWSINS]</color> <b><color=yellow>Data Persistence Settings not found!</color></b> " +
                    "Please configure the <b><color=cyan>Data Persistence Settings file</color></b> and assign it in the " +
                    "<b><color=cyan>Data Persistence Manager</color></b> object to fix this error.");
                return;
            }

            // Save Global Parameters: Scene, Date & Time
            string sceneName = SceneManager.GetActiveScene().name;
            bool isSceneExcluded = DataPersistenceManager.instance != null && DataPersistenceManager.instance.IsSceneExcludedFromSpecificData();
            gameData.timeSaved = System.DateTime.Now.ToString("dd MMMM, yyyy HH:mm");
            if (!isSceneExcluded) gameData.currentScene = sceneName;

            // Execute all registered save modules
            for (int i = 0; i < saveModules.Count; i++)
            {
                saveModules[i].SaveData(gameData, dataPersistenceSettings);
            }
        }

        public void LoadData(GameData gameData, DataPersistence_SO dataPersistenceSettings)
        {
            if (dataPersistenceSettings == null)
            {
                Debug.LogError("<color=red>[COWSINS]</color> <b><color=yellow>Data Persistence Settings not found!</color></b> " +
                    "Please configure the <b><color=cyan>Data Persistence Settings file</color></b> and assign it in the " +
                    "<b><color=cyan>Data Persistence Manager</color></b> object to fix this error.");
                return;
            }

            // Execute all registered save modules
            for (int i = 0; i < saveModules.Count; i++)
            {
                saveModules[i].LoadData(gameData, dataPersistenceSettings);
            }
        }
        #endregion

        #region Backward Compatibility Static & Instance Helpers
#if INVENTORY_PRO_ADD_ON
        public static GameData.SerializableInventoryData SerializeSlotData(SlotData slotData)
        {
            return InventoryProSaveModule.SerializeSlotData(slotData);
        }

        public static GameData.SerializableInventoryData[] SerializeContainedItems(SlotData[] slots)
        {
            return InventoryProSaveModule.SerializeContainedItems(slots);
        }
#endif

        public void OnDropHotbar(Pickeable pick)
        {
            if (pick == null) return;
            if (instantiatedObjects == null)
                instantiatedObjects = new Dictionary<string, CustomSaveData>();

#if SAVE_LOAD_ADD_ON
            pick.SaveInstance();
#endif
        }
        #endregion
    }
}