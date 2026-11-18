using System.Collections.Generic;
using UnityEngine;

namespace cowsins.Inventory
{
    /// <summary>
    /// Gathers all Backpack & container-related operations for Inventory Pro Add-On
    /// </summary>
    public sealed class InvBackpackBehaviour
    {
        private readonly InventoryProManager inventoryManager;
        private readonly WeaponController weaponController;
        private readonly Transform backpackParent;
        private readonly GridGenerator gridGenerator;
        private readonly InvChestBehaviour.UpdateSlotsSizeDelegate updateSlotsSize;
        private readonly InventoryDataModel dataModel;

        private InventorySlot equippedBackpackSlot;
        private Vector2 defaultPosition;

        public Transform BackpackParent => backpackParent;
        public InventorySlot[,] BackpackSlots => dataModel.BackpackSlots;
        public InventorySlot ActiveBackpackSlot => dataModel.ActiveBackpackSlot;
        public bool IsBackpackOpen => dataModel.ActiveBackpackSlot != null && dataModel.BackpackSlots != null && dataModel.BackpackSlots.Length > 0;
        public IContainerItem ActiveContainerItem => dataModel.ActiveBackpackSlot?.slotData?.item as IContainerItem;
        public Backpack_SO ActiveBackpackItem => dataModel.ActiveBackpackSlot?.slotData?.item as Backpack_SO;
        public InventorySlot EquippedBackpackSlot => equippedBackpackSlot;

        public InvBackpackBehaviour(
            InventoryProManager inventoryManager,
            WeaponController weaponController,
            Transform backpackParent,
            GridGenerator gridGenerator,
            InvChestBehaviour.UpdateSlotsSizeDelegate updateSlotsSize,
            InventoryDataModel dataModel)
        {
            this.inventoryManager = inventoryManager;
            this.weaponController = weaponController;
            this.backpackParent = backpackParent;
            this.gridGenerator = gridGenerator;
            this.updateSlotsSize = updateSlotsSize;
            this.dataModel = dataModel;

            Transform target = backpackParent != null ? backpackParent : gridGenerator?.ChestParent;
            if (target != null)
            {
                RectTransform rect = target.GetComponent<RectTransform>();
                if (rect != null) defaultPosition = rect.anchoredPosition;
            }
        }

        public void ToggleBackpack(InventorySlot backpackSlot)
        {
            if (backpackSlot == null) return;
            InventorySlot anchor = backpackSlot.GetAnchorSlot() ?? backpackSlot;
            if (dataModel.ActiveBackpackSlot == anchor && IsBackpackOpen)
                CloseBackpack();
            else
                OpenBackpack(backpackSlot);
        }

        public void OpenBackpack(InventorySlot backpackSlot)
        {
            if (backpackSlot == null || !backpackSlot.HasValidItem) return;

            InventorySlot anchor = backpackSlot.GetAnchorSlot() ?? backpackSlot;
            if (anchor == null || !(anchor.slotData.item is IContainerItem containerItem)) return;

            // If this backpack is already open, do nothing
            if (dataModel.ActiveBackpackSlot == anchor && IsBackpackOpen) return;

            // If another backpack was open, close it first
            if (IsBackpackOpen) CloseBackpack();

            dataModel.ActiveBackpackSlot = anchor;

            int rows = containerItem.Rows;
            int cols = containerItem.Columns;
            int totalSlots = rows * cols;

            // Ensure containedItems array is initialized and matches the backpack capacity
            if (anchor.slotData.containedItems == null || anchor.slotData.containedItems.Length != totalSlots)
            {
                SlotData[] newContained = new SlotData[totalSlots];
                if (anchor.slotData.containedItems != null)
                {
                    int copyCount = Mathf.Min(anchor.slotData.containedItems.Length, totalSlots);
                    for (int i = 0; i < copyCount; i++)
                        newContained[i] = anchor.slotData.containedItems[i];
                }
                else
                {
                    SlotData[] defaults = (anchor.slotData.item as Item_SO)?.CreateDefaultContainedItems();
                    if (defaults != null && defaults.Length > 0)
                    {
                        int seedCount = Mathf.Min(defaults.Length, totalSlots);
                        for (int i = 0; i < seedCount; i++)
                            newContained[i] = defaults[i];
                    }
                }

                for (int i = 0; i < totalSlots; i++)
                {
                    if (newContained[i] == null) newContained[i] = new SlotData();
                }

                anchor.slotData.containedItems = newContained;
            }

            // use dedicated backpackParent if assigned, otherwise fall back to chestParent
            Transform targetParent = backpackParent != null ? backpackParent : gridGenerator.ChestParent;
            if (targetParent != null) targetParent.gameObject.SetActive(true);

            dataModel.BackpackSlots = new InventorySlot[rows, cols];

            // Generate Backpack Grid reusing InventoryProGeneratorBehaviour
            var gridData = gridGenerator._InventoryGridData_SO;
            gridGenerator.Generator?.GenerateGrid(
                SlotType.Backpack,
                rows,
                cols,
                gridData != null ? gridData.inventoryPadding : default,
                targetParent,
                dataModel.BackpackSlots,
                gridData != null ? gridData.buttonPrefab : null);

            // Populate slots
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    int linear = r * cols + c;
                    SlotData data = (anchor.slotData.containedItems != null && linear < anchor.slotData.containedItems.Length && anchor.slotData.containedItems[linear] != null)
                        ? anchor.slotData.containedItems[linear]
                        : new SlotData();

                    SlotData slotData = dataModel.BackpackSlots[r, c].anchorSlot == null ? data : new SlotData();
                    dataModel.BackpackSlots[r, c].Initialize(r, c, slotData, weaponController, inventoryManager);
                }
            }

            updateSlotsSize?.Invoke(dataModel.BackpackSlots);

            // Dynamically adjust vertical stacking layout so backpack never overlaps with chests
            UpdateContainerLayout();
        }

        public void CloseBackpack()
        {
            Transform targetParent = backpackParent != null ? backpackParent : gridGenerator.ChestParent;
            if (targetParent != null)
            {
                // Only hide if not falling back to an open chest parent
                if (gridGenerator.ChestBehaviour == null || !gridGenerator.ChestBehaviour.IsChestOpen || targetParent != gridGenerator.ChestParent)
                    targetParent.gameObject.SetActive(false);

                RectTransform bpRect = targetParent.GetComponent<RectTransform>();
                if (bpRect != null) bpRect.anchoredPosition = defaultPosition;
            }

            if (dataModel.BackpackSlots == null || dataModel.ActiveBackpackSlot == null)
            {
                dataModel.ActiveBackpackSlot = null;
                dataModel.BackpackSlots = new InventorySlot[0, 0];
                return;
            }

            SyncActiveBackpackData();

            foreach (InventorySlot slot in dataModel.BackpackSlots)
            {
                if (slot != null)
                {
                    slot.UnbindDataModel();
                    Object.Destroy(slot.gameObject);
                }
            }

            dataModel.BackpackSlots = new InventorySlot[0, 0];
            dataModel.ActiveBackpackSlot = null;
        }

        public void UpdateContainerLayout()
        {
            Transform targetParent = backpackParent != null ? backpackParent : gridGenerator?.ChestParent;
            if (targetParent == null) return;

            // If backpack is using chestParent directly, avoid offsetting chest onto itself
            if (gridGenerator != null && targetParent == gridGenerator.ChestParent) return;

            RectTransform bpRect = targetParent.GetComponent<RectTransform>();
            if (bpRect == null) return;

            if (gridGenerator != null && gridGenerator.isChestOpen && gridGenerator.ChestParent != null && gridGenerator.ChestParent.gameObject.activeSelf)
            {
                RectTransform chestRect = gridGenerator.ChestParent.GetComponent<RectTransform>();
                if (chestRect != null)
                {
                    float extraSpacing = 20f;

                    // Check if LootAllChestButton is active and extends below the chest container
                    var lootBtn = inventoryManager != null ? inventoryManager.LootAllChestButton : null;
                    if (lootBtn != null && lootBtn.gameObject.activeInHierarchy && lootBtn.transform.IsChildOf(chestRect))
                    {
                        RectTransform lootRect = lootBtn.GetComponent<RectTransform>();
                        if (lootRect != null)
                        {
                            extraSpacing = Mathf.Max(extraSpacing, Mathf.Abs(lootRect.anchoredPosition.y) + lootRect.rect.height + 15f);
                        }
                    }

                    float targetY = chestRect.anchoredPosition.y - chestRect.sizeDelta.y - extraSpacing;
                    bpRect.anchoredPosition = new Vector2(chestRect.anchoredPosition.x, targetY);
                    return;
                }
            }

            bpRect.anchoredPosition = defaultPosition;
        }

        public void SyncActiveBackpackData()
        {
            if (dataModel.ActiveBackpackSlot == null || dataModel.BackpackSlots == null) return;

            int rows = dataModel.BackpackSlots.GetLength(0);
            int cols = dataModel.BackpackSlots.GetLength(1);
            SlotData[] syncData = new SlotData[rows * cols];

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    InventorySlot slot = dataModel.BackpackSlots[r, c];
                    syncData[r * cols + c] = slot != null && slot.slotData != null ? slot.slotData : new SlotData();
                }
            }

            dataModel.ActiveBackpackSlot.slotData.containedItems = syncData;
            dataModel.NotifySlotAreaChanged(dataModel.ActiveBackpackSlot);
        }

        public void OnEquipped(InventorySlot slot)
        {
            equippedBackpackSlot = slot;
            if (inventoryManager != null && inventoryManager.IsInventoryOpen && !IsBackpackOpen) OpenBackpack(slot);
        }

        public void OnUnequipped(InventorySlot slot)
        {
            if (dataModel.ActiveBackpackSlot == slot) CloseBackpack();
            if (equippedBackpackSlot == slot) equippedBackpackSlot = null;
        }

        public bool IsBackpackFull()
        {
            if (dataModel.BackpackSlots == null || dataModel.BackpackSlots.Length == 0) return true;

            int rows = dataModel.BackpackSlots.GetLength(0);
            int cols = dataModel.BackpackSlots.GetLength(1);

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    InventorySlot slot = dataModel.BackpackSlots[r, c];
                    if (slot == null || !slot.HasValidItem) return false;
                }
            }
            return true;
        }

        public void LootAllBackpack()
        {
            if (!IsBackpackOpen || dataModel.BackpackSlots == null) return;

            int rows = dataModel.BackpackSlots.GetLength(0);
            int cols = dataModel.BackpackSlots.GetLength(1);

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    InventorySlot slot = dataModel.BackpackSlots[r, c];
                    if (slot != null && slot.HasValidItem && slot.anchorSlot == slot) inventoryManager.QuickTransfer(slot);
                }
            }
        }
    }
}
