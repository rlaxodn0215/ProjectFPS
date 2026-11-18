using System.Collections.Generic;
using Newtonsoft.Json;

namespace cowsins.SaveLoad
{
    [System.Serializable]
    public class GameData
    {
        // GLOBAL HEADER
        public long lastUpdated;
        public string timeSaved;
        public string currentScene;

        // DOMAIN MODELS
        public PlayerSaveData player = new PlayerSaveData();
        public InventorySaveData inventory = new InventorySaveData();
        public WorldObjectsSaveData world = new WorldObjectsSaveData();

        // Extensible storage for custom third-party modules
        public Dictionary<string, string> customModules = new Dictionary<string, string>();

        [System.Serializable]
        public class PlayerSaveData
        {
            public Dictionary<string, SerializablePlayerTransforms> playerTransforms = new Dictionary<string, SerializablePlayerTransforms>();
            public float playerHealth = 100f;
            public float playerShield = 50f;
            public float maxHealth = 100f;
            public float maxShield = 50f;
            public float damageMultiplier = 1f;
            public float healMultiplier = 1f;
            public float totalExperience = 0f;
            public int playerLevel = 0;
            public int coins = 0;
            public int currentWeaponInt = 0;
            public SerializableInventoryData[] serializableHotbarData = new SerializableInventoryData[0];
        }

        [System.Serializable]
        public class InventorySaveData
        {
            public int rows;
            public int cols;
            public SerializableInventoryData[] serializableInventoryData = new SerializableInventoryData[0];
            public SerializableCustomSlotData[] serializableCustomSlotData = new SerializableCustomSlotData[0];
            public SerializableFavItemReference[] favItems = new SerializableFavItemReference[0];
        }

        [System.Serializable]
        public class WorldObjectsSaveData
        {
            public Dictionary<string, CustomSaveData> triggeredObjects = new Dictionary<string, CustomSaveData>();
            public Dictionary<string, CustomSaveData> interactablesObjects = new Dictionary<string, CustomSaveData>();
            public Dictionary<string, CustomSaveData> enemyObjects = new Dictionary<string, CustomSaveData>();
            public Dictionary<string, CustomSaveData> destructibleObjects = new Dictionary<string, CustomSaveData>();
            public Dictionary<string, CustomSaveData> customObjects = new Dictionary<string, CustomSaveData>();
            public Dictionary<string, CustomSaveData> instantiatedObjects = new Dictionary<string, CustomSaveData>();
        }

        [System.Serializable]
        public class SerializableCustomSlotData
        {
            public string customSlotId;
            public int slotIndex;
            public SerializableInventoryData slotData;

            public SerializableCustomSlotData() { }

            public SerializableCustomSlotData(string customSlotId, int slotIndex, SerializableInventoryData slotData)
            {
                this.customSlotId = customSlotId;
                this.slotIndex = slotIndex;
                this.slotData = slotData;
            }
        }

        [System.Serializable]
        public class SerializableInventoryData
        {
            public string SOPath;
            public int amount;
            public int bulletsLeftInMagazine, totalBullets;
            public bool isOriented;
            public int rotationAngle;
            public float currentDurability;

            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public string barrel,
                scope,
                stock,
                grip,
                magazine,
                flashlight,
                laser;

            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public SerializableInventoryData[] containedItems;
        }

        [System.Serializable]
        public class SerializableFavItemReference
        {
            public int row, col;
            public string itemName;
            public bool isInventorySlot;
        }

        [System.Serializable]
        public class SerializablePlayerTransforms
        {
            public float[] playerPosition;
            public float[] playerRotation;
            public bool isCrouching;

            public SerializablePlayerTransforms()
            {
                playerPosition = new float[3] { 0, 0, 0 };
                playerRotation = new float[4] { 0, 0, 0, 1 };
                isCrouching = false;
            }
        }

        public GameData()
        {
            timeSaved = System.DateTime.Now.ToString("dd MMMM, yyyy HH:mm");
            currentScene = "Level One";
            player = new PlayerSaveData();
            inventory = new InventorySaveData();
            world = new WorldObjectsSaveData();
            customModules = new Dictionary<string, string>();
        }
    }
}