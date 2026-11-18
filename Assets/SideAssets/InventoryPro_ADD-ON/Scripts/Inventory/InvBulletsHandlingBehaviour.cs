using System;
using UnityEngine;

namespace cowsins.Inventory
{
    public sealed class InvBulletsHandlingBehaviour
    {
        private readonly InvWeightCalculator weightCalculator;
        private readonly InventoryDataModel dataModel;
        private readonly IInventoryGridService gridService;
        private readonly InventoryProManager manager;

        private int Rows => dataModel.Rows;
        private int Columns => dataModel.Columns;

        public InvBulletsHandlingBehaviour(InvWeightCalculator weightCalculator, InventoryDataModel dataModel, IInventoryGridService gridService = null, InventoryProManager manager = null)
        {
            this.weightCalculator = weightCalculator;
            this.dataModel = dataModel;
            this.gridService = gridService;
            this.manager = manager;
        }

        public int GatherBulletsInInventory(Weapon_SO weapon)
        {
            int amount = 0;
            InventorySlot[,] inventorySlots = dataModel.InventorySlots;
            if (inventorySlots == null) return 0;

            for (int i = 0; i < Columns * Rows; i++)
            {
                (int iRow, int iCol) = GetRowAndColumnOutOfIndex(i);
                InventorySlot inventorySlot = inventorySlots[iRow, iCol];
                if (inventorySlot != null && inventorySlot.anchorSlot == inventorySlot && inventorySlot.slotData?.item is BulletTypeIdentifier_SO bulletsSO && BulletsMatchWeapon(bulletsSO, weapon))
                {
                    amount += inventorySlot.slotData.amount;
                }
            }

            return amount;
        }

        public void CalculateTotalBullets(bool overrideCurrentBullets, WeaponController weaponController, InventorySlot[] hotbarSlots)
        {
            if (weaponController == null || weaponController.Weapon == null || !weaponController.Weapon.limitedMagazines) return;

            if (overrideCurrentBullets && hotbarSlots != null && hotbarSlots.Length > 0)
            {
                weaponController.Id.bulletsLeftInMagazine = hotbarSlots[weaponController.CurrentWeaponIndex].slotData.bulletsLeftInMagazine;
            }

            weaponController.Id.totalBullets = GatherBulletsInInventory(weaponController.Id.weapon);

            weaponController.Events.OnAmmoChanged?.Invoke(true);
        }

        /// <summary>
        /// Updates the Hotbar slot bullets left in magazine when a shot happens
        /// </summary>
        public void UpdateBulletCountOnShooting(WeaponController weaponController, InventorySlot[] hotbarSlots)
        {
            if (hotbarSlots == null || hotbarSlots.Length == 0 || weaponController == null || weaponController.Weapon == null) return;
            int idx = weaponController.CurrentWeaponIndex;
            if (idx < 0 || idx >= hotbarSlots.Length) return;
            hotbarSlots[idx].slotData.bulletsLeftInMagazine = weaponController.Id?.bulletsLeftInMagazine ?? 0;
        }

        /// <summary>
        /// Updates bullets info on the hotbar slots after dragging and dropping
        /// </summary>
        public void UpdateBulletsHotbar(InventorySlot draggedSlot, InventorySlot highlightedSlot, WeaponController weaponController)
        {
            bool isDraggedItemWeapon = draggedSlot != null ? draggedSlot.IsItemWeapon : false;
            bool isHighlightedItemWeapon = highlightedSlot != null ? highlightedSlot.IsItemWeapon : false;
            InventoryProManager mgr = manager != null ? manager : InventoryProManager.instance;

            if (isDraggedItemWeapon && draggedSlot != null)
            {
                draggedSlot.Policy.OnBeforeItemRemoved(draggedSlot, mgr);
            }

            if (isHighlightedItemWeapon && highlightedSlot != null)
            {
                highlightedSlot.Policy.OnBeforeItemRemoved(highlightedSlot, mgr);
            }
        }

        /// <summary>
        /// Called when a weapon finishes reloading to update hotbar slot data.
        /// </summary>
        public void OnWeaponReload(WeaponController weaponController, InventorySlot[] hotbarSlots)
        {
            if (weaponController == null || weaponController.Weapon == null || !weaponController.Weapon.limitedMagazines) return;

            int previousAmount = GatherBulletsInInventory(weaponController.Weapon);
            int currentAmount = weaponController.Id.totalBullets;

            int difference = previousAmount - currentAmount;
            ReduceBulletsInInventory(difference, weaponController.Weapon);

            int idx = weaponController.CurrentWeaponIndex;
            if (hotbarSlots != null && idx >= 0 && idx < hotbarSlots.Length)
            {
                var id = weaponController.Inventory[idx];
                hotbarSlots[idx].slotData.bulletsLeftInMagazine = id?.bulletsLeftInMagazine ?? 0;
                hotbarSlots[idx].slotData.totalBullets = id?.totalBullets ?? 0;
            }
        }

        public void ReduceBulletsInInventory(int amount, Weapon_SO weapon)
        {
            int remainingToReduce = amount;
            if (remainingToReduce <= 0) return;

            InventorySlot[,] inventorySlots = dataModel.InventorySlots;
            if (inventorySlots == null) return;

            InventoryProManager mgr = manager != null ? manager : InventoryProManager.instance;

            for (int i = 0; i < Columns * Rows; i++)
            {
                (int iRow, int iCol) = GetRowAndColumnOutOfIndex(i);
                InventorySlot inventorySlot = inventorySlots[iRow, iCol];
                if (inventorySlot == null || inventorySlot.anchorSlot == null || inventorySlot.anchorSlot != inventorySlot) continue;

                if (inventorySlot.slotData?.item is BulletTypeIdentifier_SO bulletsSO && BulletsMatchWeapon(bulletsSO, weapon))
                {
                    int bulletsInSlot = inventorySlot.slotData.amount;
                    int amountToReduceFromThisSlot = Mathf.Min(remainingToReduce, bulletsInSlot);

                    inventorySlot.slotData.amount -= amountToReduceFromThisSlot;
                    remainingToReduce -= amountToReduceFromThisSlot;

                    weightCalculator?.UpdateCurrentWeight(bulletsSO, -amountToReduceFromThisSlot);

                    if (inventorySlot.slotData.amount <= 0)
                    {
                        if (gridService != null) gridService.ClearSlotArea(inventorySlot);
                        else ClearSlotAreaFallback(inventorySlot, inventorySlots);
                        inventorySlot.Policy.OnItemRemoved(inventorySlot, mgr);
                    }
                    else
                    {
                        dataModel.NotifySlotAreaChanged(inventorySlot);
                        inventorySlot.Policy.OnSlotModified(inventorySlot, mgr);
                    }

                    if (remainingToReduce <= 0)
                    {
                        break;
                    }
                }
            }
        }

        private void ClearSlotAreaFallback(InventorySlot slot, InventorySlot[,] slots)
        {
            if (slot == null || slots == null) return;
            InventorySlot anchor = slot.GetAnchorSlot();
            if (anchor == null) return;

            Vector2Int size = anchor.GetItemSize();
            int rows = slots.GetLength(0);
            int cols = slots.GetLength(1);

            int startRow = anchor.row;
            int startCol = anchor.col;

            int maxExtent = Mathf.Max(size.x, size.y);
            for (int r = 0; r < maxExtent; r++)
            {
                int targetRow = startRow + r;
                if (targetRow < 0 || targetRow >= rows) continue;

                for (int c = 0; c < maxExtent; c++)
                {
                    int targetCol = startCol + c;
                    if (targetCol < 0 || targetCol >= cols) continue;

                    if (slots[targetRow, targetCol] != null && slots[targetRow, targetCol].anchorSlot == anchor)
                    {
                        slots[targetRow, targetCol].ClearSlot();
                    }
                }
            }
        }

        private bool BulletsMatchWeapon(BulletTypeIdentifier_SO bulletsSO, Weapon_SO weapon)
        {
            if (bulletsSO == null) return false;

            if (weapon == null) return bulletsSO.bulletType == BulletTypeIdentifier_SO.BulletType.Universal;

            var weaponBullet = weapon.bulletTypeIdentifier;

            if (weaponBullet == null) return bulletsSO.bulletType == BulletTypeIdentifier_SO.BulletType.Universal;

            if (weaponBullet.bulletType == BulletTypeIdentifier_SO.BulletType.Universal) return true;

            return bulletsSO == weaponBullet;
        }

        private (int row, int col) GetRowAndColumnOutOfIndex(int index)
        {
            int row = index / Columns;
            int col = index % Columns;
            return (row, col);
        }
    }
}
