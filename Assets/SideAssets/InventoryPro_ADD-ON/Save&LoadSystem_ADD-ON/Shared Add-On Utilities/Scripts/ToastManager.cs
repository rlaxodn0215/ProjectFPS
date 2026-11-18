using UnityEngine;
using System.Collections;
using TMPro;
using UnityEngine.UI;

namespace cowsins
{
    /// <summary>
    /// Toast Manager allows to send Notifications during the game for better feedback and UI Response
    /// </summary>
    public class ToastManager : MonoBehaviour
    {
        public static ToastManager Instance;

        [SerializeField, Header("MAIN SETTINGS")] private CanvasGroup toastPrefab;
        [SerializeField, Tooltip("Define a Position in the UI for the Toast to be displayed (2D Space)")] private Vector2 toastPosition;

        [SerializeField, Header("TOAST CONFIGURATION"), Tooltip("If true, it will display toast messages on trading (Crafting or Purchasing)")] private bool showToastOnTrade;
        [SerializeField, Tooltip("If true, Toast Messages will display the amount traded (ex: Collected x3 Gears)")] private bool showAmountTraded;
        [SerializeField, Tooltip("If true, Toast Messages will be displayed if the Inventory Space is not enough for an item to be picked up.")] private bool showToastOnInsufficientSpace;

        [SerializeField, Header("MESSAGES CUSTOMIZATION")] private string collectedMsg = "Collected";
        [SerializeField] private string purchaseMsg = "Purchased";
        [SerializeField] private string inventoryIsFullMsg = "Inventory Is Full";
        [SerializeField] private string favPinnedMenuIsFullMsg = "Fav Pinned Menu is Full";
        [SerializeField] private string alreadyPinnedToastMsg = "This item is already pinned";
        [SerializeField] private string itemPinnedToastMsg = " Pinned";
        [SerializeField] private string itemUnpinnedToastMsg = " Unpinned";
        [SerializeField] private string itemUsed = "Used";
        [SerializeField] private string attachmentNotCompatibleMsg = "This attachment is not compatible";
        [SerializeField] private string attachmentSuccessfullyAddedMsg = "attached on";
        [SerializeField] private string weaponIsAlreadyUnholsteredMsg = "A weapon is already unholstered.";
        [SerializeField] private string itemDeleted = "Deleted";
        [SerializeField] private string itemBroke = "broke";
        [SerializeField] private string fullHealthMsg = "Player Health is already full";
        [SerializeField] private string enableAutoSaveMsg = "Auto Save Enabled";
        [SerializeField] private string disableAutoSaveMsg = "Auto Save Disabled";
        [SerializeField] private string dataPersistenceManagerNotAvailableMsg = "Data Persistence Manager Not Found";
        [SerializeField] private string gameLoaded = "Game Loaded";
        [SerializeField] private string gameSaved = "Game Saved";

        // GETTERS
        public bool ShowToastOnTrade => showToastOnTrade;
        public bool ShowAmountTraded => showAmountTraded;
        public bool ShowToastOnInsufficientSpace => showToastOnInsufficientSpace;

        public string CollectedMsg => collectedMsg;
        public string PurchaseMsg => purchaseMsg;
        public string InventoryIsFullMsg => inventoryIsFullMsg;
        public string FavPinnedMenuIsFullMsg => favPinnedMenuIsFullMsg;
        public string AlreadyPinnedToastMsg => alreadyPinnedToastMsg;
        public string ItemPinnedToastMsg => itemPinnedToastMsg;
        public string ItemUnpinnedToastMsg => itemUnpinnedToastMsg;
        public string ItemUsed => itemUsed;
        public string AttachmentNotCompatibleMsg => attachmentNotCompatibleMsg;
        public string AttachmentSuccessfullyAddedMsg => attachmentSuccessfullyAddedMsg;
        public string FullHealthMsg => fullHealthMsg;
        public string DisableAutoSaveMsg => disableAutoSaveMsg;
        public string EnableAutoSaveMsg => enableAutoSaveMsg;
        public string DataPersistenceManagerNotAvailableMsg => dataPersistenceManagerNotAvailableMsg;
        public string GameLoaded => gameLoaded;
        public string GameSaved => gameSaved;
        public string WeaponIsAlreadyUnholsteredMsg => weaponIsAlreadyUnholsteredMsg;

        public string ItemDeleted => itemDeleted;
        public string ItemBroke => string.IsNullOrEmpty(itemBroke) ? "broke" : itemBroke;

        // INTERNAL USE
        private CanvasGroup toast;
        private TextMeshProUGUI toastText;
        private Image toastBG;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }

            toast = Instantiate(toastPrefab, transform);
            toastText = toast.GetComponentInChildren<TextMeshProUGUI>();
            toastBG = toast.GetComponentInChildren<Image>();
            toast.GetComponent<RectTransform>().localPosition = toastPosition;
            toast.gameObject.SetActive(false);
        }


        /// <summary>
        /// Shows a Toast ( Notification ) with a custom Message
        /// </summary>
        /// <param name="message"></param>
        public void ShowToast(string message)
        {
            StopAllCoroutines();
            StartCoroutine(ShowToastCoroutine(message, 2));
        }

        /// <summary>
        /// Shows a Toast ( Notification ) with a custom Message and stablished duration
        /// </summary>
        /// <param name="message"></param>
        /// <param name="duration"></param>
        public void ShowToast(string message, float duration = 2f)
        {
            StopAllCoroutines();
            StartCoroutine(ShowToastCoroutine(message, duration));
        }

        private IEnumerator ShowToastCoroutine(string message, float duration)
        {
            toastText.text = message;
            float textWidth = toastText.preferredWidth;
            float textHeight = toastText.preferredHeight;
            float paddingX = 20f;
            float paddingY = 20f;
            RectTransform bgRectTransform = toastBG.GetComponent<RectTransform>();
            bgRectTransform.sizeDelta = new Vector2(textWidth + paddingX, textHeight + paddingY);
            toast.alpha = 0;
            toast.gameObject.SetActive(true);
            yield return FadeToast(1f, 0.5f);
            yield return new WaitForSeconds(duration);
            yield return FadeToast(0f, 0.5f);
            toast.gameObject.SetActive(false);
        }

        private IEnumerator FadeToast(float targetAlpha, float duration)
        {
            float startAlpha = toast.alpha;
            float elapsedTime = 0f;

            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime * 2;
                toast.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsedTime / duration);
                yield return null;
            }

            toast.alpha = targetAlpha;
        }
    }
}