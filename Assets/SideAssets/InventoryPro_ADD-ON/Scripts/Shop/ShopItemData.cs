using System.Collections;
using System.Collections.Generic;
using cowsins;
using UnityEngine;

namespace cowsins.Inventory
{
    /// <summary>
    /// Shop Item Data stores information regarding each item sold in this shop.
    /// </summary>
    [System.Serializable]
    public class ShopItemData
    {
        [Tooltip("Item Available For Purchase")] public Item_SO item;
        [Tooltip("x Items will be available for sale. Once the Shop runs out of stock, no more buying will be allowed.")] public int amountAvailable = 1;
        [Tooltip("Coins to pay for this Item")] public int price = 1;
        [Tooltip("Minimum level required by the player to access this item purchase. If this equals 0, it will always be available.")] public int minLevelRequired = 0;
    }
}