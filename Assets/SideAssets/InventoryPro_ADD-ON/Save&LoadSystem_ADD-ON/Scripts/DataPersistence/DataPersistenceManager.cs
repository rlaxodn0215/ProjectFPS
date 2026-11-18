using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Events;

namespace cowsins.SaveLoad
{
    public class DataPersistenceManager : MonoBehaviour
    {
        public static DataPersistenceManager instance;

        // DATA PERSISTENCE SO
        [SerializeField] private DataPersistence_SO dataPersistenceSettings;

        // GETTERS
        public DataPersistence_SO DataPersistenceSettings => dataPersistenceSettings;
        public string FirstLevelName => dataPersistenceSettings.FirstLevelName;

        // OTHERS
        private GameData gameData;
        private FileDataHandler fileDataHandler;
        private GameDataManager gameDataManager;
        private string selectedProfileId = "";
        private string originalScene;
        private Coroutine autoSaveCoroutine;

        // EVENTS
        [System.Serializable]
        public class Events
        {
            public UnityEvent onGameDataCreated, onSaveGame, onLoadGame,
                              onSelectedProfileIDChanged, onGameDataDeleted, onSceneLoaded;
        }

        public Events events;

        private void OnEnable()
        {
            // Handle Singleton
            if (instance != null)
            {
                Destroy(this.gameObject);
                return;
            }
            instance = this;

            // Save the original Scene, that corresponds to the scene where the DataPersistenceManager is located, to keep track whenever we come back to such scene.
            originalScene = SceneManager.GetActiveScene().name;

            // Keep DataPersistenceManager between Scenes.
            DontDestroyOnLoad(this.gameObject);

            // Initialize a FileDataHandler. FileDataHandler contains useful helper methods to handle files and data.
            bool useEncryption = dataPersistenceSettings != null ? dataPersistenceSettings.UseEncryption : true;
            this.fileDataHandler = new FileDataHandler($"{dataPersistenceSettings.FileName}.json", useEncryption);

            SceneManager.sceneLoaded += OnSceneLoaded;

            gameDataManager = GetComponent<GameDataManager>();
        }
        private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

        public void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Reset the selectedProfileID only if we come back to a non-saveable scene 
            if (IsNonSaveableScene(scene.name))
            {
                selectedProfileId = string.Empty;
            }

#if UNITY_EDITOR
            // In the Editor, if testing a gameplay level directly without selecting a slot in the Main Menu,
            // automatically load the most recent profile to enable seamless testing.
            if (SelectedProfileIdIsNull() && !IsNonSaveableScene(scene.name))
            {
                string recent = fileDataHandler?.GetMostRecentlyUpdatedProfileId();
                if (!string.IsNullOrEmpty(recent))
                {
                    selectedProfileId = recent;
                    Debug.Log($"<color=cyan>[COWSINS]</color> Direct PlayMode test in '{scene.name}': automatically selected most recent profile '{selectedProfileId}'.");
                }
            }
#endif

            if (SelectedProfileIdIsNull()) return;

            StartCoroutine(LoadGameNextFrame());

            if (autoSaveCoroutine != null)
                StopCoroutine(autoSaveCoroutine);

            if (dataPersistenceSettings.AutoSave)
                autoSaveCoroutine = StartCoroutine(AutoSave());

            events.onSceneLoaded?.Invoke();
        }
        private IEnumerator LoadGameNextFrame()
        {
            yield return null;
            LoadGame();
        }

        /// <summary>
        /// Load a Game Save Data based on its profile ID.
        /// </summary>
        /// <param name="newProfileId"></param>
        public void ChangeSelectedProfileId(string newProfileId)
        {
            this.selectedProfileId = newProfileId;
            events.onSelectedProfileIDChanged?.Invoke();
            LoadGame();
        }

        /// <summary>
        /// Delete a Game Save Data based on its profile ID.
        /// </summary>
        /// <param name="profileId"></param>
        public void DeleteProfileData(string profileId)
        {
            fileDataHandler.Delete(profileId);

            events.onGameDataDeleted?.Invoke();

            LoadGame();
        }

        /// <summary>
        /// Gets the most recently played profile ID from File Data Handler
        /// </summary>
        public void InitializeSelectedProfileId() => this.selectedProfileId = fileDataHandler.GetMostRecentlyUpdatedProfileId();

        /// <summary>
        /// Starts a new Game
        /// </summary>
        public void NewGame()
        {
            this.gameData = new GameData();
            events.onGameDataCreated?.Invoke();
        }

        /// <summary>
        /// Loads a game based on the selected profile ID, only if the scene is saveable.
        /// </summary>
        public void LoadGame()
        {
            this.gameData = fileDataHandler.Load(selectedProfileId);

            if (this.gameData == null)
                NewGame();

            gameDataManager.LoadData(gameData, dataPersistenceSettings);

            events.onLoadGame?.Invoke();
        }

        /// <summary>
        /// Saves the game based on the selected profile ID.
        /// </summary>
        public void SaveGame()
        {
            if (SelectedProfileIdIsNull() || IsNonSaveableScene()) return;

            if (this.gameData == null)
            {
                Debug.LogWarning("No data was found. A New Game needs to be started before data can be saved!");
                return;
            }

            if (dataPersistenceSettings.ShowToastOnGameSaved)
            {
                if (ToastManager.Instance)
                    ToastManager.Instance.ShowToast(dataPersistenceSettings.ToastMessage);
            }

            gameDataManager.SaveData(gameData, dataPersistenceSettings);

            gameData.lastUpdated = System.DateTime.Now.ToBinary();

            if (fileDataHandler != null && dataPersistenceSettings != null)
                fileDataHandler.UseEncryption = dataPersistenceSettings.UseEncryption;

            fileDataHandler.Save(gameData, selectedProfileId);

            events.onSaveGame?.Invoke();
        }

        // On leaving the game, save it
        private void OnApplicationQuit() => SaveGame();

        /// <summary>
        /// Helper method to indicate if gameData is null or not
        /// </summary>
        /// <returns></returns>
        public bool HasGameData() { return gameData != null; }

        /// <summary>
        /// Return Game Data from all Profiles.
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, GameData> GetAllProfilesGameData() { return fileDataHandler.LoadAllProfiles(); }

        /// <summary>
        /// Return lightweight SaveSlotMetadata from all Profiles for fast UI
        /// </summary>
        public Dictionary<string, SaveSlotMetadata> GetAllProfilesMetadata() { return fileDataHandler.LoadAllProfilesMetadata(); }

        public string GetLastUsedProfileId()
        {
            return string.IsNullOrEmpty(selectedProfileId) ? fileDataHandler.GetMostRecentlyUpdatedProfileId() : selectedProfileId;
        }

        public bool IsAutoSaveEnabled()
        {
            return autoSaveCoroutine != null;
        }
        public void EnableAutoSave()
        {
            if (dataPersistenceSettings.AutoSave)
                autoSaveCoroutine = StartCoroutine(AutoSave());
        }
        public void DisableAutoSave()
        {
            if (autoSaveCoroutine != null)
            {
                StopCoroutine(autoSaveCoroutine);
                autoSaveCoroutine = null;
            }
        }
        private IEnumerator AutoSave()
        {
            while (true)
            {
                yield return new WaitForSeconds(dataPersistenceSettings.AutoSaveTimeSeconds);
                SaveGame();
            }
        }

        public bool SelectedProfileIdIsNull() { return string.IsNullOrWhiteSpace(selectedProfileId) || selectedProfileId == ""; }

        public bool IsNonSaveableScene()
        {
            return IsNonSaveableScene(SceneManager.GetActiveScene().name);
        }

        public bool IsNonSaveableScene(string sceneName)
        {
            if (dataPersistenceSettings == null || dataPersistenceSettings.nonSaveableSceneNames == null) return false;

            for (int i = 0; i < dataPersistenceSettings.nonSaveableSceneNames.Count; i++)
            {
                if (dataPersistenceSettings.nonSaveableSceneNames[i] == sceneName)
                {
                    return true;
                }
            }

            return false;
        }

        public bool IsSceneExcludedFromSpecificData()
        {
            string currentSceneName = SceneManager.GetActiveScene().name;

            foreach (var sceneAsset in dataPersistenceSettings.excludedScenesFromSpecificSaveDataNames)
            {
                if (sceneAsset == currentSceneName)
                {
                    return true;
                }
            }

            return false;
        }

        public GameData GetCurrentGameData()
        {
            return gameData;
        }
    }
}