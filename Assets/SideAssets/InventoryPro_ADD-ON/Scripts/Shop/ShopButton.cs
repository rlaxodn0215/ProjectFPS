using UnityEngine;

namespace cowsins.Inventory
{
    /// <summary>
    /// ShopButton allows the interaction between the Shop and the Player, enabling the user to purchase Items by exchanging coins.
    /// </summary>
    public class ShopButton : TradeButton
    {
        [SerializeField, Tooltip("Text to display on the Purchase Button")] private string callToAction = "Purchase";

        // INTERNAL USE + GETTERS
        private ShopItemData shopItemData;
        private ShopUI shopUI;
        private int price;
        public ShopItemData ShopItemData => shopItemData;

        // Initialize Basic References & Settings
        public void Initialize(ShopUI shopUI, ShopItemData shopItem, TradeHub shop, bool available)
        {
            // Required References
            this.shopUI = shopUI;
            this.tradeHub = shop;
            this.shopItemData = shopItem;
            this.price = shopItem.price;
            // Pass this Button for Highlight Interactions
            this.actionButton.tradeButton = this;

            if (!((Shop)shop as Shop).decreaseAvailableAmountOnPurchase)
            {
                shopItemData.amountAvailable = 99;
            }
            else if (shopItem.amountAvailable <= 1) // If no more stock available, disable interaction
                amountInputField.interactable = false;

            // Visuals
            iconImage.sprite = shopItem.item.icon;
            nameText.text = shopItem.item._name;
            UpdateCurrentAmount(1);
            UpdatePrice();
            HandleLockedItem(available, shopItem.minLevelRequired);
        }

        // Highlights the button on pointer enter
        public override void PointerEnter()
        {
            shopUI.UpdateHighlightedItemUI(shopItemData.item);
            base.PointerEnter();
        }
        // Unhighlights the button on pointer exit
        public override void PointerExit()
        {
            shopUI.UpdateHighlightedItemUI(null);
            base.PointerExit();
        }

        // Called on clicking the Purchase Button
        protected override void HandleActionClick()
        {
            // Gather final Coins to pay based on the amount of items we want to buy
            int finalPrice = price * currentAmount;

            // Decline Purchase if the Player hasn´t got enough currency or if the Item is Locked.
            if (!tradeHub.progressionManager.coinService.CheckIfEnoughCoins(finalPrice) || lockedContainer.activeSelf)
            {
                tradeHub.Events.onDeclinedTrade?.Invoke();
                SoundManager.Instance.PlaySound(tradeHub.ForbiddenTradeSFX, 0, 0, false);
                return;
            }

            // Decline Purchase if the Item cannot be stored in the Inventory
            if (!AddItemToInventory(shopItemData.item, currentAmount).Item1)
            {
                if (ToastManager.Instance.ShowToastOnInsufficientSpace)
                {
                    ToastManager.Instance.ShowToast(ToastManager.Instance.InventoryIsFullMsg);
                }
                SoundManager.Instance.PlaySound(tradeHub.ForbiddenTradeSFX, 0, 0, false);
                return;
            }

            // Process Payment
            tradeHub.progressionManager.coinService.RemoveCoins(finalPrice, true);

            // Handle Notifications with Toast Manager
            if (ToastManager.Instance.ShowToastOnTrade)
            {
                string message = ToastManager.Instance.ShowAmountTraded ? $"x{currentAmount} " : "";
                message += $"{shopItemData.item._name} {ToastManager.Instance.PurchaseMsg}";
                ToastManager.Instance.ShowToast(message);
            }

            // Refresh all items. Based on the updated currency, update the visuals to display whether the player can purchase the items or not.
            Shop shop = (Shop)tradeHub;
            ShopUI shopUI = shop.ShopUIInstance;
            shopUI.RefreshShopItems(this);
            tradeHub.Events.onSuccessfulTrade?.Invoke();

            SoundManager.Instance.PlaySound(tradeHub.SuccessfulTradeSFX, 0, 0, false);

            // Ensure this interaction is saved.
#if SAVE_LOAD_ADD_ON
            shop.StoreData();
#endif
        }

        /// <summary>
        /// Updates current amount to be purchased at once.
        /// </summary>
        /// <param name="currentAmount"></param>
        protected override void UpdateCurrentAmount(int currentAmount)
        {
            if (currentAmount > shopItemData.amountAvailable) currentAmount = shopItemData.amountAvailable;
            this.currentAmount = currentAmount;
            amountInputField.text = this.currentAmount.ToString();

            nextAmountButton.gameObject.SetActive(true);
            previousAmountButton.gameObject.SetActive(true);

            if (currentAmount <= 1) previousAmountButton.gameObject.SetActive(false);
            if (currentAmount >= shopItemData.amountAvailable) nextAmountButton.gameObject.SetActive(false);

            UpdatePrice();
        }

        /// <summary>
        /// Handles the Action of Increasing Current Amount to be purchased.
        /// </summary>

        protected override void HandleNextAmountClick()
        {
            if (currentAmount >= shopItemData.amountAvailable) return;

            UpdateCurrentAmount(currentAmount + 1);
        }

        /// <summary>
        /// Increases Current Amount to be purchased.
        /// </summary>
        /// <param name="amount"></param>
        public override void AddAmount(int amount)
        {
            currentAmount += amount;
            if (currentAmount > shopItemData.item.maxStack) currentAmount = shopItemData.item.maxStack;
            amountInputField.text = this.currentAmount.ToString();
            UpdatePrice();

            tradeHub.Events.onIncreaseAmount?.Invoke();
        }
        /// <summary>
        /// Decreases Current Amount to be purchased.
        /// </summary>
        /// <param name="amount"></param>
        public override void ReduceAmount(int amount)
        {
            currentAmount -= amount;
            if (currentAmount < 1) currentAmount = 1;
            amountInputField.text = this.currentAmount.ToString();
            UpdatePrice();

            tradeHub.Events.onDecreaseAmount?.Invoke();
        }

        // Displays new Pricing for this specific object.
        private void UpdatePrice()
        {
            int finalPrice = shopItemData.price * currentAmount;
            // If the Player hasn´t got enough coins, display in red.
            callToActionText.text = tradeHub.progressionManager.coinService.CheckIfEnoughCoins(finalPrice) ?
                $"${finalPrice} {callToAction}" :
                $"<color=#{ColorUtility.ToHtmlStringRGB(Color.red)}> ${finalPrice} {callToAction}</color>";
            if (shopItemData.price <= 0) callToActionText.text = "Free";
        }

    }

}

