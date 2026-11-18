using cowsins.SaveLoad;
using UnityEngine.SceneManagement;

namespace cowsins.Inventory
{
    public partial class Shop : TradeHub
    {
#if SAVE_LOAD_ADD_ON
        // ShopItems(ShopItemData[]) from Shop cannot be directly serialized, so we´ll store the amount of each ShopItemData instead
        // Because of it, we need to create a new class that inherits from CustomSaveData. This class will store our customized data instead.
        [System.Serializable]
        public class ShopSaveData : CustomSaveData
        {
            public int[] availableAmounts;
        }

        // Base SaveFields method gathers all variables within the class that implement the [SaveField]
        // In this case, no variable implements it in Shop, so we´ll be processing and saving the availableAmounts manually.
        public override CustomSaveData SaveFields()
        {
            return new ShopSaveData
            {
                // For each item in shopItems, store the amountAvailable into the availableAmounts array.
                availableAmounts = shopItems != null ?
                                   System.Array.ConvertAll(shopItems, item => item.amountAvailable)
                                   : new int[0],
                // Scene Name is also very important and must be saved.
                SceneName = SceneManager.GetActiveScene().name
            };
        }

        public override void LoadFields(object data)
        {
            if (data is ShopSaveData shopData)
            {
                // Ensure shopItems and availableAmounts have the same length
                if (shopData.availableAmounts != null)
                {
                    // We´ll populate the amounts from the initial shopItems 
                    for (int i = 0; i < shopData.availableAmounts.Length; i++)
                    {
                        shopItems[i].amountAvailable = shopData.availableAmounts[i];
                    }
                }
            }
        }
#endif
    }
}