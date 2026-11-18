using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace cowsins.SaveLoad
{
    /// <summary>
    /// This Button is used to create, load or delete games in the Save & Load Main Menu
    /// </summary>
    public class SaveSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField, Title("Profile")] private string profileId = "";
        [SerializeField] SaveSlotsMenu saveSlotsMenu;

        [SerializeField, Title("Content")] private GameObject noDataContent;
        [SerializeField] private GameObject hasDataContent;
        [SerializeField] private TextMeshProUGUI saveName;
        [SerializeField] private TextMeshProUGUI playerLevelText;
        [SerializeField] private TextMeshProUGUI coinsText;
        [SerializeField] private TextMeshProUGUI lastPlayedText;

        [SerializeField, Title("Clear Data Button")] private Button clearDataButton;

        private Button saveSlotButton;

        private void OnEnable()
        {
            saveSlotButton = this.GetComponent<Button>();
            saveSlotButton.onClick.AddListener(() => saveSlotsMenu.OnSaveSlotClicked(this));
            clearDataButton.onClick.AddListener(() => saveSlotsMenu.OnClearClicked(this));

            // Initialy disable the Clear Data Button, as we only want to display it if hovering this
            clearDataButton.gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            saveSlotButton.onClick.RemoveAllListeners();
            clearDataButton.onClick.RemoveAllListeners();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (noDataContent.activeSelf) return;

            // Display Clear Data Button only if hovering this
            clearDataButton.gameObject.SetActive(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            clearDataButton.gameObject.SetActive(false);
        }

        public void SetData(SaveSlotMetadata metadata)
        {
            if (metadata == null)
            {
                noDataContent.SetActive(true);
                hasDataContent.SetActive(false);
                clearDataButton.gameObject.SetActive(false);
            }
            else
            {
                noDataContent.SetActive(false);
                hasDataContent.SetActive(true);
                saveName.text = metadata.currentScene;
                playerLevelText.text = $"LEVEL: {(metadata.playerLevel + 1).ToString()}";
                coinsText.text = $"COINS: {metadata.coins.ToString()}";
                lastPlayedText.text = $"LAST SAVED: {metadata.timeSaved}";
            }
        }

        public void SetData(GameData data)
        {
            SetData(data != null ? new SaveSlotMetadata(profileId, data) : null);
        }

        public string GetProfileId()
        {
            return this.profileId;
        }

        public void SetInteractable(bool interactable)
        {
            saveSlotButton.interactable = interactable;
            clearDataButton.interactable = interactable;
        }
    }
}