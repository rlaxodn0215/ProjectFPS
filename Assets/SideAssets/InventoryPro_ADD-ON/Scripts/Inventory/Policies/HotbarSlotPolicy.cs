using UnityEngine;

namespace cowsins.Inventory
{
    /// <summary>
    /// Policy for Hotbar slots
    /// </summary>
    public sealed class HotbarSlotPolicy : ISlotPolicy
    {
        public SlotType SlotType => SlotType.Hotbar;
        public bool IsGridBased => false;
        public bool AllowsItemRotation => false;
        public bool AllowsSplitting => false;
        public bool AllowsDurabilityLoss => false;
        public bool AffectsPlayerWeight => false;
        public bool IsExternalContainer => false;
        public bool AllowsFastTransfer => false;
        public bool AllowsItemUsage => false;
        public bool AllowsFavoritesPinning => true;
        public bool CanLootAll => false;

        public void LootAll(InventoryProManager manager) { }

        public bool AcceptsItem(InventorySlot slot, Item_SO item)
        {
            if (item == null) return true;
            if (!(item is Weapon_SO)) return false;
            if (slot != null && slot.AcceptedType != null && slot.AcceptedType != typeof(Item_SO))
                return slot.AcceptedType.IsAssignableFrom(item.GetType());
            return true;
        }

        public void OnBeforeItemRemoved(InventorySlot slot, InventoryProManager manager)
        {
            if (slot == null) return;
            var wc = manager?._WeaponController;
            if (wc == null || wc.Inventory == null) return;

            int col = slot.col;
            if (col < 0 || col >= wc.Inventory.Length) return;

            if (wc.IsReloading && wc.Id == wc.Inventory[col])
                wc.reloadBehaviour?.StopReload();

            if (slot.HasValidItem && slot.IsItemWeapon && wc.Inventory[col] != null)
            {
                slot.slotData.bulletsLeftInMagazine = wc.Inventory[col].bulletsLeftInMagazine;
                slot.slotData.totalBullets = wc.Inventory[col].totalBullets;
            }
        }

        public void OnItemPlaced(InventorySlot slot, InventoryProManager manager)
        {
            SyncHotbarWeapon(slot, manager);
        }

        public void OnItemRemoved(InventorySlot slot, InventoryProManager manager)
        {
            ClearHotbarWeapon(slot, manager);
        }

        public void OnSlotModified(InventorySlot slot, InventoryProManager manager)
        {
            SyncHotbarWeapon(slot, manager);
        }

        public static void SyncHotbarWeapon(InventorySlot slot, InventoryProManager manager)
        {
            if (slot == null || manager == null) return;
            var wc = manager._WeaponController;
            if (wc == null || wc.Inventory == null) return;

            int col = slot.col;
            if (col < 0 || col >= wc.Inventory.Length) return;

            if (slot.HasValidItem && slot.slotData.item is Weapon_SO weaponSO)
            {
                if (wc.CurrentWeaponIndex == col && wc.Inventory[col] != null)
                    wc.ReleaseCurrentWeapon();
                else if (wc.Inventory[col] != null)
                {
                    Object.Destroy(wc.Inventory[col].gameObject);
                }

                manager.PlayerDependencies?.UIManager?.GetComponentInChildren<InventoryHUDModule>()?.SetInventoryUISlotWeapon(col, weaponSO);
                wc.InstantiateWeapon(weaponSO, col, slot.slotData.bulletsLeftInMagazine, slot.slotData.totalBullets);

                SlotData slotData = slot.slotData;
                var attachments = new[] {
                    slotData.barrel, slotData.scope, slotData.stock, slotData.grip,
                    slotData.magazine, slotData.flashlight, slotData.laser
                };
                foreach (var attachment in attachments)
                {
                    if (attachment != null)
                        wc.attachmentsSystem?.AssignNewAttachmentToWeapon(attachment, col);
                }

                if (wc.CurrentWeaponIndex == col)
                {
                    if (wc.Id == null && wc.Inventory[col] != null)
                        wc.weaponInventoryBehaviour?.UnHolster(wc.Inventory[col].gameObject, false);
                    wc.Weapon = weaponSO;
                }
            }
            else
            {
                ClearHotbarWeapon(slot, manager);
            }
        }

        public static void ClearHotbarWeapon(InventorySlot slot, InventoryProManager manager)
        {
            if (slot == null || manager == null) return;
            var wc = manager._WeaponController;
            if (wc == null || wc.Inventory == null) return;

            int col = slot.col;
            if (col < 0 || col >= wc.Inventory.Length) return;

            if (col == wc.CurrentWeaponIndex && wc.Inventory[col] != null)
                wc.ReleaseCurrentWeapon();
            else if (wc.Inventory[col] != null)
            {
                wc.aimBehaviour?.ForceAimReset();
                if (wc.Inventory[col]?.gameObject != null)
                    Object.Destroy(wc.Inventory[col].gameObject);
                wc.Inventory[col] = null;
            }

            manager.PlayerDependencies?.UIManager?.GetComponentInChildren<InventoryHUDModule>()?.SetInventoryUISlotWeapon(col, null);
        }
    }
}
