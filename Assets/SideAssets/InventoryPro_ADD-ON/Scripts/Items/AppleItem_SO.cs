using cowsins;
using UnityEngine;

namespace cowsins.Inventory
{
    [CreateAssetMenu(fileName = "newAppleInventoryItem", menuName = "COWSINS/Inventory/Showcase Items/New Apple", order = 1)]
    public class AppleItem_SO : Item_SO
    {
        [Min(.1f), Tooltip("Eating an apple heals ´amountToHealOnUse´ Health Points.")] public float amountToHealOnUse = 10;

#if INVENTORY_PRO_ADD_ON
        public override void Use(InventoryProManager inventoryProManager, InventorySlot slot)
        {
            // Avoid consuming the Apple if the Player is already at max health
            PlayerStats stats = inventoryProManager._WeaponController.GetComponent<PlayerStats>();
            if (stats.Shield >= stats.MaxShield && stats.Health >= stats.MaxHealth)
            {
                ToastManager.Instance?.ShowToast(ToastManager.Instance?.FullHealthMsg);
                return;
            }
            // Heal the player
            stats.Heal(amountToHealOnUse);
            // base.Use ensures the item is consumed.
            base.Use(inventoryProManager, slot);
        }
#endif
    }
}