using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using cowsins.SaveLoad;

namespace cowsins
{
    public class AddonShowcaseMainMenu : MonoBehaviour
    {
        [Header("First Selected Button")]
        [SerializeField, Tooltip("First Button to select for Controller Navigation")] Button firstSelected;

        [Header("Menu Navigation")]
        [SerializeField] private GameObject mainMenuSection;
        [SerializeField] private SaveSlotsMenu saveSlotsSection;

        [Header("Menu Buttons")]
        [SerializeField] private Button newGameButton;
        [SerializeField] private Button continueGameButton;
        [SerializeField] private Button loadGameButton;
        [SerializeField] private Button exitButton;

        private void OnEnable() => EventSystem.current.SetSelectedGameObject(firstSelected.gameObject);

        private void Start()
        {
            newGameButton.onClick.AddListener(() => OpenSaveSlotsMenu(false));
            loadGameButton.onClick.AddListener(() => OpenSaveSlotsMenu(true));
            continueGameButton.onClick.AddListener(() => OnContinueGameClicked());
            exitButton.onClick.AddListener(() => ExitToDesktop());
            DisableButtonsDependingOnData();
        }

        private void OnDisable()
        {
            newGameButton.onClick.RemoveAllListeners();
            loadGameButton.onClick.RemoveAllListeners();
            continueGameButton.onClick.RemoveAllListeners();
            exitButton.onClick.RemoveAllListeners();
        }

        public void DisableButtonsDependingOnData()
        {
            // Disable Quick Access button if there is no last played game
            if (string.IsNullOrEmpty(DataPersistenceManager.instance.GetLastUsedProfileId()))
            {
                continueGameButton.interactable = false;
            }
            else continueGameButton.interactable = true;
        }

        public void OpenSaveSlotsMenu(bool isLoadingMenuEnabled)
        {
            saveSlotsSection.ActivateMenu(isLoadingMenuEnabled);
            this.DeactivateMenu();
        }

        public void OnContinueGameClicked()
        {
            // Another check before loading a game, in case it does not exist
            string profileID = DataPersistenceManager.instance.GetLastUsedProfileId();
            if (string.IsNullOrEmpty(profileID))
                return;

            DataPersistenceManager.instance.ChangeSelectedProfileId(profileID);

            DisableMenuButtons();

            DataPersistenceManager.instance.SaveGame();

            SceneManager.LoadSceneAsync(DataPersistenceManager.instance.GetCurrentGameData().currentScene);
        }

        void DisableMenuButtons()
        {
            newGameButton.interactable = false;
            continueGameButton.interactable = false;
        }

        public void ActivateMenu()
        {
            mainMenuSection.SetActive(true);
            DisableButtonsDependingOnData();
        }

        public void DeactivateMenu() => mainMenuSection.SetActive(false);

        private void ExitToDesktop() => Application.Quit();
    }

}