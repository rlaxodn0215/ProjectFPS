using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using cowsins;

namespace cowsins.SaveLoad
{
    public class WorldObjectsSaveModule : ISaveModule
    {
        public string ModuleId => "WorldObjects";

        public Dictionary<string, CustomSaveData> triggersData = new Dictionary<string, CustomSaveData>();
        public Dictionary<string, CustomSaveData> interactablesData = new Dictionary<string, CustomSaveData>();
        public Dictionary<string, CustomSaveData> enemyHealthData = new Dictionary<string, CustomSaveData>();
        public Dictionary<string, CustomSaveData> destructiblesData = new Dictionary<string, CustomSaveData>();
        public Dictionary<string, CustomSaveData> customObjectsData = new Dictionary<string, CustomSaveData>();
        public Dictionary<string, CustomSaveData> instantiatedObjects = new Dictionary<string, CustomSaveData>();

        private readonly Func<Dictionary<string, Identifiable>> getPrefabDictionary;

        public WorldObjectsSaveModule(Func<Dictionary<string, Identifiable>> getPrefabDictionary)
        {
            this.getPrefabDictionary = getPrefabDictionary;
        }

        public void ResetDictionaries()
        {
            triggersData = new Dictionary<string, CustomSaveData>();
            interactablesData = new Dictionary<string, CustomSaveData>();
            enemyHealthData = new Dictionary<string, CustomSaveData>();
            destructiblesData = new Dictionary<string, CustomSaveData>();
            customObjectsData = new Dictionary<string, CustomSaveData>();
            instantiatedObjects = new Dictionary<string, CustomSaveData>();
        }

        public void SaveData(GameData gameData, DataPersistence_SO settings)
        {
            if (settings == null || !settings.SaveObjects || gameData == null) return;

            bool isSceneExcluded = DataPersistenceManager.instance != null && DataPersistenceManager.instance.IsSceneExcludedFromSpecificData();
            if (isSceneExcluded) return;

            var worldData = gameData.world ?? (gameData.world = new GameData.WorldObjectsSaveData());

            worldData.triggeredObjects = triggersData;
            worldData.interactablesObjects = interactablesData;
            worldData.enemyObjects = enemyHealthData;
            worldData.customObjects = customObjectsData;
            worldData.destructibleObjects = destructiblesData;
            worldData.instantiatedObjects = instantiatedObjects;
        }

        public void LoadData(GameData gameData, DataPersistence_SO settings)
        {
            if (settings == null || !settings.SaveObjects || gameData == null || gameData.world == null) return;

            string currentSceneName = SceneManager.GetActiveScene().name;
            var worldData = gameData.world;

#if SAVE_LOAD_ADD_ON
            // Triggers
            triggersData = worldData.triggeredObjects ?? new Dictionary<string, CustomSaveData>();
#pragma warning disable CS0618
            LoadAndCleanupObjects(UnityEngine.Object.FindObjectsOfType<Trigger>(), triggersData, currentSceneName, (trigger, data) => trigger.LoadFields(data));

            // Interactables
            interactablesData = worldData.interactablesObjects ?? new Dictionary<string, CustomSaveData>();
            LoadAndCleanupObjects(UnityEngine.Object.FindObjectsOfType<Interactable>(), interactablesData, currentSceneName, (interactable, data) => interactable.LoadFields(data));

            // Destructibles
            destructiblesData = worldData.destructibleObjects ?? new Dictionary<string, CustomSaveData>();
            LoadAndCleanupObjects(UnityEngine.Object.FindObjectsOfType<Destructible>(), destructiblesData, currentSceneName, (destructible, data) => destructible.LoadFields(data));

            // Enemy Health
            enemyHealthData = worldData.enemyObjects ?? new Dictionary<string, CustomSaveData>();
            LoadAndCleanupObjects(UnityEngine.Object.FindObjectsOfType<EnemyHealth>(), enemyHealthData, currentSceneName, (enemy, data) => enemy.LoadFields(data));

            // Custom Identifiable Objects
            customObjectsData = worldData.customObjects ?? new Dictionary<string, CustomSaveData>();
            LoadAndCleanupObjects(UnityEngine.Object.FindObjectsOfType<Identifiable>(), customObjectsData, currentSceneName, (customObj, data) => customObj.LoadFields(data));
#pragma warning restore CS0618

            // Instantiated Objects
            instantiatedObjects = worldData.instantiatedObjects ?? new Dictionary<string, CustomSaveData>();
            LoadInstantiatedObjects();
#endif
        }

        private void LoadAndCleanupObjects<TObject, TData>(
            TObject[] sceneObjects,
            Dictionary<string, TData> savedData,
            string currentSceneName,
            Action<TObject, TData> loadAction)
            where TObject : MonoBehaviour
            where TData : class
        {
            if (savedData == null || sceneObjects == null) return;

            // Map scene objects by UniqueIDValue
            Dictionary<string, TObject> objectLookup = new Dictionary<string, TObject>();
            for (int i = 0; i < sceneObjects.Length; i++)
            {
                var obj = sceneObjects[i];
                var id = (obj as Identifiable)?.UniqueIDValue;
                if (!string.IsNullOrEmpty(id) && !objectLookup.ContainsKey(id))
                {
                    objectLookup.Add(id, obj);
                }
            }

            // Iterate through saved objects safely
            List<string> keys = savedData.Keys.ToList();
            for (int i = 0; i < keys.Count; i++)
            {
                string key = keys[i];
                if (savedData.TryGetValue(key, out TData value))
                {
                    if ((value as CustomSaveData)?.SceneName == currentSceneName)
                    {
                        if (objectLookup.TryGetValue(key, out TObject sceneObject))
                        {
                            loadAction(sceneObject, value);
                        }
                    }
                }
            }
        }

        private void LoadInstantiatedObjects()
        {
            if (instantiatedObjects == null) return;

            Dictionary<string, Identifiable> prefabs = getPrefabDictionary != null ? getPrefabDictionary() : null;
            if (prefabs == null) return;

            foreach (var pickeable in instantiatedObjects)
            {
                string prefabKey = !string.IsNullOrEmpty(pickeable.Value.PrefabId) ? pickeable.Value.PrefabId : pickeable.Value.Type;

                if (!prefabs.TryGetValue(prefabKey, out Identifiable prefab))
                {
                    // Fallback to Type if PrefabId lookup missed
                    if (!string.IsNullOrEmpty(pickeable.Value.Type) && prefabs.TryGetValue(pickeable.Value.Type, out Identifiable fallbackPrefab))
                    {
                        prefab = fallbackPrefab;
                    }
                    else
                    {
                        Debug.LogWarning($"<color=yellow>[COWSINS]</color> No prefab registered for key '{prefabKey}'. Please assign a mapping in GameDataManager.");
                        continue;
                    }
                }

                if (prefab == null)
                {
                    Debug.LogError($"<color=red>[COWSINS]</color> Prefab is null for key '{prefabKey}'.");
                    continue;
                }

                Vector3 spawnPosition = pickeable.Value.Position != null ? pickeable.Value.Position.ToVector3() : Vector3.zero;
                Quaternion spawnRotation = Quaternion.identity;

                var instantiatedObject = UnityEngine.Object.Instantiate(prefab, spawnPosition, spawnRotation);
                instantiatedObject.ReplaceUniqueID(pickeable.Key);
                instantiatedObject.gameObject.SetActive(true);

#if SAVE_LOAD_ADD_ON
                instantiatedObject.LoadFields(pickeable.Value);
#endif
            }
        }
    }
}
