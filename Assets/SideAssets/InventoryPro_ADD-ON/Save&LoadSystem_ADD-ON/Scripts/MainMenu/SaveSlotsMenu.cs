using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace cowsins.SaveLoad
{
    public class SaveSlotsMenu : MonoBehaviour
    {
        [SerializeField, Title("First Selected Button")] Button firstSelected;

        [SerializeField, Title("Menu Buttons")] private Button backButton;
        [SerializeField] private AddonShowcaseMainMenu inventoryShowcaseMainMenu;

        private SaveSlot[] saveSlots;

        private bool isLoadingMenuEnabled = false;

        private void OnEnable() => firstSelected.Select();

        private void Awake()
        {
            // Gather all SaveSlot Buttons
            saveSlots = this.GetComponentsInChildren<SaveSlot>();
            backButton.onClick.AddListener(() => inventoryShowcaseMainMenu.DisableButtonsDependingOnData());
        }

        public void OnSaveSlotClicked(SaveSlot saveSlot)
        {
            // Disable all buttons to prevent being able to click on a different slot after clicking on one.
            DisableMenuButtons();

            // Update the selected profile ID 
            DataPersistenceManager.instance.ChangeSelectedProfileId(saveSlot.GetProfileId());

            string sceneToLoad;

            // Start a new game
            if (!isLoadingMenuEnabled) 
            {
                // Initialize Data
                DataPersistenceManager.instance.NewGame();
                // When creating a new game, we always load the First Level 
                sceneToLoad = DataPersistenceManager.instance.FirstLevelName; 
            }
            else 
            {
                // Load a Game
                DataPersistenceManager.instance.LoadGame();

                // Get the target scene 
                sceneToLoad = DataPersistenceManager.instance.HasGameData()
                    ? DataPersistenceManager.instance.GetCurrentGameData().currentScene
                    : DataPersistenceManager.instance.FirstLevelName; 
            }

            SceneManager.LoadSceneAsync(sceneToLoad);
        }


        public void OnClearClicked(SaveSlot saveSlot)
        {
            DataPersistenceManager.instance.DeleteProfileData(saveSlot.GetProfileId());
            ActivateMenu(isLoadingMenuEnabled);
        }

        public void ActivateMenu(bool isLoadingMenuEnabled)
        {
            this.gameObject.SetActive(true);
            this.isLoadingMenuEnabled = isLoadingMenuEnabled;
            Dictionary<string, SaveSlotMetadata> profilesMetadata = DataPersistenceManager.instance.GetAllProfilesMetadata();

            GameObject firstSelected = backButton.gameObject;
            foreach (SaveSlot saveSlot in saveSlots)
            {
                SaveSlotMetadata profileMetadata = null;
                profilesMetadata.TryGetValue(saveSlot.GetProfileId(), out profileMetadata);
                saveSlot.SetData(profileMetadata);
                if (profileMetadata == null && isLoadingMenuEnabled)
                {
                    saveSlot.SetInteractable(false);
                }
                else
                {
                    saveSlot.SetInteractable(true);
                    if (firstSelected.Equals(backButton.gameObject))
                    {
                        firstSelected = saveSlot.gameObject;
                    }
                }
            }

            // Set the First Selected button for Navigation purposes.
            EventSystem.current.SetSelectedGameObject(firstSelected.gameObject);
        }

        public void DeactivateMenu()
        {
            this.gameObject.SetActive(false);
        }

        private void DisableMenuButtons()
        {
            foreach (SaveSlot saveSlot in saveSlots)
            {
                saveSlot.SetInteractable(false);
            }
            backButton.interactable = false;
        }
    }

}