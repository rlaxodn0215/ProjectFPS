using UnityEngine;

namespace cowsins.Inventory
{
    public partial class Shop : TradeHub
    {
        [SerializeField, Title("SHOP", upMargin = 10)] private ShopUI shopUIPrefab;
        [Tooltip("If enabled, Available Amount will decrease as the Player purchases the available stock.")] public bool decreaseAvailableAmountOnPurchase = true;
        [SerializeField, Tooltip("Stores all Items sold in this Shop")] private ShopItemData[] shopItems;

        // INTERNAL USE + GETTERS
        private ShopUI shopUIInstance;
        public ShopUI ShopUIInstance => shopUIInstance;

        // Gets called on opening the Shop
        // Instantiates a new Shop UI
        public override void OpenHub()
        {
            base.OpenHub();
            shopUIInstance = Instantiate(shopUIPrefab);
            shopUIInstance.InitializeUI(shopItems, this, CloseHub);
        }

        // Gets called on closing the Shop
        // Destroys Shop UI
        public override void CloseHub()
        {
            base.CloseHub();
            if (shopUIInstance != null)
                Destroy(shopUIInstance.gameObject);
        }
    }
}