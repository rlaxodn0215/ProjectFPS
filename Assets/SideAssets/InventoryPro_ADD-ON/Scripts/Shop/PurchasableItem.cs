using UnityEngine;
using UnityEngine.UI; 
using UnityEngine.Events;

namespace cowsins.Inventory
{
    /// <summary>
    /// Purchasable Item works like a Shop but for a single Item. If you want to interact/collect this object, you�ll need to pay Coins first. 
    /// </summary>
    public class PurchasableItem : Interactable
    {
        [System.Serializable]
        public class Events
        {
            public UnityEvent onDeclinedPurchaseLowLevel, onDeclinesPurchaseLowCoins, onSuccessfulPurchase;
        }
        [SerializeField] private ShopItemData itemToPurchase;
        
        [Title("References"), SerializeField] private Image image;
        [Tooltip("Transform under which the graphics will be stored at when instantiated"), SerializeField]
        private Transform graphics;

        [SerializeField, Title("Events")] private Events purchasableEvents;

        private void Start()
        {
            // Update Interact Text based on the Pricing
            interactText += $" ${itemToPurchase.price} {itemToPurchase.item._name}";
            GetVisuals();
        }

        /// <summary>
        /// Called when looking at the Interactable
        /// Updates the Interact Text based on whether the player can afford the Item or not.
        /// </summary>
        public override void Highlight(Transform player)
        {
            if (itemToPurchase.price == 0) return;
            var progressionManager = player.GetComponent<ProgressionManager>();

            if(progressionManager.experienceService.playerLevel < itemToPurchase.minLevelRequired)
            {
                interactText = "<color=#" + ColorUtility.ToHtmlStringRGB(Color.magenta) + ">" + $" Required level {itemToPurchase.minLevelRequired + 1} to unlock." + "</color>";
            }
            else if (progressionManager.coinService.CheckIfEnoughCoins(itemToPurchase.price))
                interactText ="<color=#" + ColorUtility.ToHtmlStringRGB(Color.green) + ">" + $" [{itemToPurchase.price}]  {itemToPurchase.item._name}" + "</color>";
            else interactText = "<color=#" + ColorUtility.ToHtmlStringRGB(Color.red) + ">" + $" [{itemToPurchase.price}]  {itemToPurchase.item._name}" + "</color>";


        }
        public override void Interact(Transform player)
        {
            var progressionManager = player.GetComponent<ProgressionManager>();

            // Decline purchase if the player level requirement is not met
            if (progressionManager.experienceService.playerLevel < itemToPurchase.minLevelRequired)
            {
                purchasableEvents.onDeclinedPurchaseLowLevel?.Invoke();
                return;
            }

            // Process Purchase if Player can afford the Item price 
            if (itemToPurchase.price != 0 && progressionManager.UseCoins && progressionManager.coinService.CheckIfEnoughCoins(itemToPurchase.price) || itemToPurchase.price == 0)
            {
                if(itemToPurchase.item is Item_SO && InventoryProManager.instance.AddItem(itemToPurchase.item, itemToPurchase.amountAvailable).Item1
                    ||itemToPurchase.item is AttachmentIdentifier_SO && InventoryProManager.instance.AddItem((AttachmentIdentifier_SO)itemToPurchase.item, 1).Item1)
                {
                    progressionManager.coinService.RemoveCoins(itemToPurchase.price, true);
                    ToastManager.Instance?.ShowToast($"{itemToPurchase.item._name} {ToastManager.Instance.PurchaseMsg}");
                    purchasableEvents.onSuccessfulPurchase?.Invoke();
                    Destroy(this.gameObject);
                }
            }
            else
                purchasableEvents.onDeclinesPurchaseLowCoins?.Invoke();
        }

        public void GetVisuals()
        {
            // Get whatever we need to display
            interactText = itemToPurchase.item._name;
            image.sprite = itemToPurchase.item.icon;
            // Manage graphics
            Destroy(graphics.transform.GetChild(0).gameObject);
            if(itemToPurchase.item.pickUpGraphics) Instantiate(itemToPurchase.item.pickUpGraphics, graphics);
        }
    }
}
