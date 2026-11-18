using UnityEngine;

namespace cowsins.Inventory
{
    [CreateAssetMenu(fileName = "newEnergyDrinkInventoryItem", menuName = "COWSINS/Inventory/Showcase Items/New Energy Drink", order = 1)]
    public class EnergyDrinkItem_SO : Item_SO
    {
        [Min(.1f), Tooltip("Drinking an Energy Drink Increases Damage. This is just an example of how to make custom Item_SOs." +
            "Check EnergyDrinkItem_SO.cs for more Information.")] public float damageIncreaseOnUse = .05f;

#if INVENTORY_PRO_ADD_ON
        public override void Use(InventoryProManager inventoryProManager, InventorySlot slot)
        {
            // Increases Damage Multiplier
            PlayerMultipliers multiplier = inventoryProManager._WeaponController.GetComponent<PlayerMultipliers>();
            multiplier.DamageMultiplier.BaseValue += damageIncreaseOnUse;
            // base.Use ensures the item is consumed.
            base.Use(inventoryProManager, slot);
        }
#endif
    }
}