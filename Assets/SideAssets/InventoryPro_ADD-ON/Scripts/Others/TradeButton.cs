using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

namespace cowsins.Inventory
{
    public abstract class TradeButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField, Title("TRADE BUTTON")] protected Image iconImage;
        [SerializeField, Tooltip("Button that performs the Trade Action")] protected HighlightInteractions actionButton;
        [SerializeField] protected TextMeshProUGUI nameText, callToActionText;
        [SerializeField, Tooltip("Input Field that displays the current amount to trade")] protected TMP_InputField amountInputField;
        [SerializeField, Tooltip("When hovered, the TradeButton Scale changes. Being 1 = default scale, " +
            "this defines the scale of the button when hovered.")] protected float highlightedScaleMultiplier = 1.2f;
        [SerializeField,Tooltip("Defines how fast the Button scales from the default scale to the highlightedScale when hovered and viceversa.")] protected float scaleLerpSpeed = 4;
        [SerializeField, Tooltip("UI that is enabled when the object to trade is locked.")] protected GameObject lockedContainer;
        [SerializeField, Tooltip("Text that displays the reason why the object to trade is locked.")] protected TextMeshProUGUI lockedText;
        [SerializeField, Tooltip("Buttons that handle amount increase or decrease.")] protected Button previousAmountButton, nextAmountButton;

        protected TradeHub tradeHub;
        public TradeHub _TradeHub => tradeHub;

        public HighlightInteractions ActionButton => actionButton;

        protected int currentAmount = 1;
        protected Vector2 targetScale, originalScale;

        protected virtual void Start()
        {
            originalScale = transform.localScale;
            targetScale = originalScale;
            actionButton.onClick.AddListener(HandleActionClick);
            previousAmountButton.onClick.AddListener(HandlePreviousAmountClick);
            nextAmountButton.onClick.AddListener(HandleNextAmountClick);
            amountInputField.onValueChanged.AddListener(InputFieldUpdateAmount);
        }

        protected virtual void OnDisable()
        {
            actionButton.onClick.RemoveListener(HandleActionClick);
            previousAmountButton.onClick.RemoveListener(HandlePreviousAmountClick);
            nextAmountButton.onClick.RemoveListener(HandleNextAmountClick);
            amountInputField.onValueChanged.RemoveListener(InputFieldUpdateAmount);
        }

        protected virtual void Update()
        {
            transform.localScale = Vector2.Lerp(transform.localScale, targetScale, Time.deltaTime * scaleLerpSpeed);
        }

        public void OnPointerEnter(PointerEventData eventData) => PointerEnter();

        public void OnPointerExit(PointerEventData eventData) => PointerExit();

        public virtual void PointerEnter()
        {
            targetScale = originalScale * highlightedScaleMultiplier;
            SoundManager.Instance.PlaySound(tradeHub.HoverSFX, 0, 0, false);
        }
        public virtual void PointerExit()
        {
            targetScale = originalScale;
        }

        protected (bool, int) AddItemToInventory(Item_SO item, int amount)
        {
            bool success = false;
            // Start with full amount & reduce on success
            int remainingAmount = amount;

            switch (item)
            {
                case AttachmentIdentifier_SO attachment:
                    bool attachmentSuccess;
                    int attachmentRemaining;
                    for (int i = 0; i < amount; i++)
                    {
                        // Store success & Item Amounts from the Tuple
                        (attachmentSuccess, attachmentRemaining) = InventoryProManager.instance.AddItem(attachment, 1);
                        if (attachmentSuccess) remainingAmount--; // Reduce remaining amount only when added
                    }
                    break;

                case BulletTypeIdentifier_SO bullets:
                    // Store success & Item Amounts from the Tuple
                    (success, remainingAmount) = InventoryProManager.instance.AddItem(bullets, amount);
                    // Since we are adding bullets to the inventory, we need to re-calculate available bullets
                    if (success) InventoryProManager.instance.CalculateTotalBullets(overrideCurrentBullets: false);
                    break;

                case Weapon_SO weapon:
                    bool weaponSuccess;
                    int weaponRemaining;
                    for (int i = 0; i < amount; i++)
                    {
                        // Store success & Item Amounts from the Tuple
                        (weaponSuccess, weaponRemaining) = InventoryProManager.instance.AddItem(weapon, 1);
                        if (weaponSuccess) remainingAmount--; // Reduce remaining amount only when added
                    }
                    break;

                default:
                    (success, remainingAmount) = InventoryProManager.instance.AddItem(item, amount);
                    break;
            }

            return (remainingAmount == 0, remainingAmount); // Success if all items were added
        }

        protected void InputFieldUpdateAmount(string input)
        {
            if (int.TryParse(input, out int amount))
            {
                UpdateCurrentAmount(amount);
            }
        }

        protected void HandleLockedItem(bool available, int minLevelRequired)
        {
            if (available)
            {
                if (tradeHub.progressionManager.experienceService.playerLevel < minLevelRequired)
                {
                    lockedContainer.SetActive(true);
                    lockedText.text = $"Required level {minLevelRequired + 1} to unlock";
                }
                else lockedContainer.SetActive(false);
            }
            else
            {
                lockedContainer.SetActive(true);
                lockedText.text = "This item is not available";
            }
        }
        protected abstract void UpdateCurrentAmount(int newAmount);

        protected abstract void HandleActionClick();

        protected void HandlePreviousAmountClick()
        {
            if (currentAmount <= 1)
            {
                return;
            }
            UpdateCurrentAmount(currentAmount - 1);
        }

        protected abstract void HandleNextAmountClick();

        public virtual void AddAmount(int amount)
        {
            currentAmount += amount;
            amountInputField.text = this.currentAmount.ToString();

            tradeHub.Events.onIncreaseAmount?.Invoke();
        }
        public virtual void ReduceAmount(int amount)
        {
            currentAmount -= amount;
            if (currentAmount < 1) currentAmount = 1;
            amountInputField.text = this.currentAmount.ToString();

            tradeHub.Events.onDecreaseAmount?.Invoke();
        }
    }
}
