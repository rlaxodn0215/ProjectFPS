using cowsins.SaveLoad;
using System.Collections.Generic;
using UnityEngine;

namespace cowsins.Inventory
{
    /// <summary>
    /// Gathers all Chest related operations for Inventory Pro Add-On.
    /// </summary>
    public sealed class InvChestBehaviour
    {
        public delegate void UpdateSlotsSizeDelegate(InventorySlot[,] slotsArray);

        private readonly InventoryProManager inventoryManager;
        private readonly WeaponController weaponController;
        private readonly Transform chestParent;
        private readonly GridGenerator gridGenerator;
        private readonly UpdateSlotsSizeDelegate updateSlotsSize;
        private readonly InventoryDataModel dataModel;

        public Transform ChestParent => chestParent;
        public InventorySlot[,] ChestSlots => dataModel.ChestSlots;
        public Chest CachedChest => dataModel.CachedChest;
        public bool IsChestOpen => dataModel.CachedChest != null;

        public InvChestBehaviour(
            InventoryProManager inventoryManager,
            WeaponController weaponController,
            Transform chestParent,
            GridGenerator gridGenerator,
            UpdateSlotsSizeDelegate updateSlotsSize,
            InventoryDataModel dataModel)
        {
            this.inventoryManager = inventoryManager;
            this.weaponController = weaponController;
            this.chestParent = chestParent;
            this.gridGenerator = gridGenerator;
            this.updateSlotsSize = updateSlotsSize;
            this.dataModel = dataModel;
        }

        public void OpenChest(Chest chest, SlotData[] chestSlotData, int chestRows, int chestColumns)
        {
            // Enables Chest UI
            if (chestParent != null) chestParent.gameObject.SetActive(true);
            dataModel.CachedChest = chest;
            dataModel.ChestSlots = new InventorySlot[chestRows, chestColumns];

            // Generate Chest Grid
            gridGenerator.Generator?.GenerateGrid(
                SlotType.Chest,
                chestRows,
                chestColumns,
                chest.InitialInventoryGridData.inventoryPadding,
                chestParent,
                dataModel.ChestSlots,
                chest.InitialInventoryGridData.buttonPrefab);

            // Populate Chest Grid
            for (int row = 0; row < chestRows; row++)
            {
                for (int col = 0; col < chestColumns; col++)
                {
                    bool hasSlotData =
                        chestSlotData != null &&
                        chestSlotData.Length > 0 &&
                        chestSlotData.Length > row * chestColumns + col &&
                        chestSlotData[row * chestColumns + col] != null;

                    SlotData data = hasSlotData ? chestSlotData[row * chestColumns + col] : new SlotData();
                    SlotData slotData = dataModel.ChestSlots[row, col].anchorSlot == null ? data : new SlotData();
                    dataModel.ChestSlots[row, col].Initialize(row, col, slotData, weaponController, inventoryManager);
                }
            }

            // Now, we need to update the size of each item populated in the Chest Slots. This is only called for Tetris Inventories.
            updateSlotsSize?.Invoke(dataModel.ChestSlots);

            // Notify Backpack container to dock underneath the newly opened chest
            gridGenerator.BackpackBehaviour?.UpdateContainerLayout();

            inventoryManager?.Events?.onOpenChest?.Invoke();
        }

        public void CloseChest()
        {
            if (chestParent != null) chestParent.gameObject.SetActive(false);

            if (dataModel.ChestSlots == null)
            {
                dataModel.CachedChest = null;
                gridGenerator.BackpackBehaviour?.UpdateContainerLayout();
                return;
            }

            List<SlotData> chestSlotsData = new List<SlotData>();

            foreach (InventorySlot slot in dataModel.ChestSlots)
            {
                chestSlotsData.Add(slot.slotData);
                if (slot != null)
                {
                    slot.UnbindDataModel();
                    Object.Destroy(slot.gameObject);
                }
            }

            // Ensure Chest Data is saved into the appropriate Chest.
            // This information is passed to the GameDataManager in case we pretend to save the game.
            dataModel.ChestSlots = new InventorySlot[0, 0];
            dataModel.CachedChest?.SetChestData(chestSlotsData.ToArray());
#if SAVE_LOAD_ADD_ON
            dataModel.CachedChest?.StoreData();
#endif
            dataModel.CachedChest = null;

            // Restore Backpack container to top position if open
            gridGenerator.BackpackBehaviour?.UpdateContainerLayout();

            inventoryManager?.Events?.onCloseChest?.Invoke();
        }

        /// <summary>
        /// Moves all items from the opened chest into the player inventory
        /// </summary>
        public void LootAllChest()
        {
            if (dataModel.ChestSlots == null) return;

            bool anyLooted = false;

            for (int i = 0; i < dataModel.ChestSlots.GetLength(0); i++)
            {
                for (int j = 0; j < dataModel.ChestSlots.GetLength(1); j++)
                {
                    // Use anchor slot as the dragged slot equivalent
                    InventorySlot dragged = dataModel.ChestSlots[i, j]?.GetAnchorSlot();
                    Item_SO item = dragged?.slotData?.item;

                    if (item == null) continue;

                    // Find an available inventory slot for this item
                    InventorySlot targetSlot = gridGenerator.Operations.FindAvailableSlotInventory(item);
                    if (targetSlot == null) continue;

                    if (targetSlot?.slotData?.item == null)
                    {
                        gridGenerator.Operations.MoveItemTo(dragged, targetSlot, playSFX: false);
                        anyLooted = true;
                    }
                    else
                    {
                        inventoryManager.NavigationBehaviour.HighlightSlot(targetSlot);
                        inventoryManager.draggedSlot = dragged;
                        gridGenerator.Operations.StackItems(inventoryManager.draggedSlot, playSFX: false);
                        anyLooted = true;
                    }
                }
            }

            inventoryManager.NavigationBehaviour.ClearDraggedHighlightedReferences();

            if (anyLooted) inventoryManager.FeedbackService?.OnItemPlaced();
        }

        public bool IsChestFull()
        {
            if (dataModel.ChestSlots == null) return true;
            foreach (var item in dataModel.ChestSlots)
            {
                if (item.slotData == null || item.slotData.item == null) return false;
            }

            return true;
        }
    }
}

