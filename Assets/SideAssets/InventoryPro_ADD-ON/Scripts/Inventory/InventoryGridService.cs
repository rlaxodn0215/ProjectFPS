using UnityEngine;

namespace cowsins.Inventory
{
    /// <summary>
    /// Handles spatial calculations, boundary validations, slot placement checks, and slot search logic
    /// </summary>
    public class InventoryGridService : IInventoryGridService
    {
        private readonly InventoryProManager inventoryManager;
        private readonly InventoryGridData_SO inventoryGridData;
        private readonly InventoryDataModel dataModel;

        private int Rows => dataModel.Rows;
        private int Columns => dataModel.Columns;

        public InventoryGridService(InventoryProManager inventoryManager, InventoryGridData_SO inventoryGridData, InventoryDataModel dataModel)
        {
            this.inventoryManager = inventoryManager;
            this.inventoryGridData = inventoryGridData;
            this.dataModel = dataModel;
        }

        public bool ItemFits(InventorySlot slot, Vector2Int itemSize, int row, int col, bool isInventorySlot)
        {
            InventorySlot[,] inventorySlotsToCheck = (slot != null ? dataModel.GetGridForSlot(slot) : null) ?? (isInventorySlot ? dataModel.InventorySlots : dataModel.ChestSlots);
            if (inventorySlotsToCheck == null) return false;

            if (!IsSlotWithinBounds(row, col, itemSize, inventorySlotsToCheck))
                return false;

            for (int i = 0; i < itemSize.y; i++)
            {
                for (int j = 0; j < itemSize.x; j++)
                {
                    InventorySlot slotToCheck = inventorySlotsToCheck[row + i, col + j];
                    if (slotToCheck != null && slotToCheck.anchorSlot != null && (slot == null || slotToCheck.GetAnchorSlot() != slot.GetAnchorSlot()) && slotToCheck.GetAnchorSlot().HasValidItem)
                        return false;
                }
            }

            return true;
        }

        public void ClearSlotArea(InventorySlot slot)
        {
            if (slot == null) return;

            InventorySlot anchor = slot.GetAnchorSlot();

            if (!slot.Policy.IsGridBased || (anchor != null && !anchor.Policy.IsGridBased))
            {
                if (anchor != null) anchor.ClearSlot();
                slot.ClearSlot();
                return;
            }

            InventorySlot[,] slots = dataModel.GetGridForSlot(slot) ?? (slot.Policy.SlotType == SlotType.Chest ? dataModel.ChestSlots : dataModel.InventorySlots);
            if (slots == null)
            {
                if (anchor != null) anchor.ClearSlot();
                slot.ClearSlot();
                return;
            }

            if (anchor == null) return;

            Vector2Int itemSize = anchor.GetItemSize();

            int startRow = anchor.row;
            int startCol = anchor.col;
            if (startRow < 0 || startCol < 0)
            {
                anchor.ClearSlot();
                return;
            }

            int biggerValue = Mathf.Max(itemSize.x, itemSize.y);

            for (int i = 0; i < biggerValue; i++)
            {
                int rowIndex = startRow + i;
                if (rowIndex < 0 || rowIndex >= slots.GetLength(0)) continue;
                for (int j = 0; j < biggerValue; j++)
                {
                    int colIndex = startCol + j;
                    if (colIndex < 0 || colIndex >= slots.GetLength(1)) continue;
                    if (slots[rowIndex, colIndex] != null && slots[rowIndex, colIndex].anchorSlot == anchor)
                        slots[rowIndex, colIndex].ClearSlot();
                }
            }
        }

        public void UpdateSlotsSize(InventorySlot[,] slotsArray)
        {
            if (inventoryManager._InventoryStyle != InventoryProManager.InventoryStyle.Tetris) return;
            if (slotsArray == null || slotsArray.Length == 0) return;

            bool isInventorySlot = slotsArray[0, 0].IsInventorySlot;
            int rows = slotsArray.GetLength(0);
            int cols = slotsArray.GetLength(1);

            foreach (InventorySlot slot in slotsArray)
            {
                if (slot.HasValidItem)
                {
                    Vector2Int size = slot.GetItemSize();
                    int itemWidth = size.x;
                    int itemHeight = size.y;
                    int startRow = slot.row;
                    int startCol = slot.col;

                    if (!ItemFits(slot, size, startRow, startCol, isInventorySlot))
                    {
                        Debug.LogError($"<color=red>[COWSINS]</color> Item at Row: <b><color=yellow>{startRow}</color></b> Col: <b><color=yellow>{startCol} ({slot.slotData?.item?._name})</color></b> " +
                            $"in <b><color=yellow>{inventoryGridData.name}</color></b> does not fit in the Inventory & overlaps with other items or is out of bounds. " +
                            "<b><color=green>Please, open the Inventory Designer & relocate or delete the item</color></b> to fix this error.", inventoryGridData);
                        ClearSlotArea(slot);
                        continue;
                    }

                    for (int i = 0; i < itemHeight; i++)
                    {
                        for (int j = 0; j < itemWidth; j++)
                        {
                            int targetRow = startRow + i;
                            int targetCol = startCol + j;

                            if (targetRow < 0 || targetRow >= rows || targetCol < 0 || targetCol >= cols)
                            {
                                Debug.LogError(
                                    $"<color=red>[COWSINS]</color> Item at Row: <b><color=yellow>{startRow}</color></b> Col: <b><color=yellow>{startCol} ({slot.slotData?.item?._name})</color></b> " +
                                    $"in <b><color=yellow>{inventoryGridData.name}</color></b> does not fit in the Inventory & overlaps with other items or is out of bounds. " +
                                    "<b><color=green>Please, open the Inventory Designer & relocate or delete the item</color></b> to fix this error.", inventoryGridData);
                                ClearSlotArea(slot);
                                return;
                            }
                            else if (slotsArray[targetRow, targetCol].anchorSlot == null)
                            {
                                slotsArray[targetRow, targetCol].anchorSlot = slot;
                            }
                        }
                    }
                }
            }

            dataModel.NotifyGridChanged();
        }

        public bool IsSlotWithinBounds(int row, int col, Vector2Int itemSize, InventorySlot[,] inventorySlots)
        {
            return !(row < 0 || row + itemSize.y > inventorySlots.GetLength(0) ||
                     col < 0 || col + itemSize.x > inventorySlots.GetLength(1));
        }

        public bool IsSlotWithinBounds(int targetRow, int targetCol, InventorySlot[,] inventorySlots)
        {
            return targetRow >= 0 && targetRow < inventorySlots.GetLength(0) &&
                   targetCol >= 0 && targetCol < inventorySlots.GetLength(1);
        }

        public bool IsInventoryFull()
        {
            InventorySlot[,] inventorySlots = dataModel.InventorySlots;
            if (inventorySlots == null) return true;
            foreach (var slot in inventorySlots)
            {
                if (slot.slotData == null || slot.slotData.item == null) return false;
            }

            return false;
        }

        public (int row, int col) GetRowAndColumnOutOfIndex(int index)
        {
            int row = index / Columns;
            int col = index % Columns;
            return (row, col);
        }

        public void ReassignSlots(InventorySlot slot)
        {
            if (slot == null) return;
            InventorySlot anchor = slot.GetAnchorSlot();
            if (anchor != null && anchor.HasValidItem)
            {
                Vector2Int itemSize = anchor.GetItemSize();

                int startRow = anchor.row;
                int startcol = anchor.col;
                if (startRow < 0 || startcol < 0) return;

                InventorySlot[,] inventorySlots = dataModel.GetGridForSlot(slot) ?? (slot.IsInventorySlot ? dataModel.InventorySlots : dataModel.ChestSlots);
                if (inventorySlots == null) return;

                for (int i = 0; i < itemSize.y; i++)
                {
                    int r = startRow + i;
                    if (r < 0 || r >= inventorySlots.GetLength(0)) continue;
                    for (int j = 0; j < itemSize.x; j++)
                    {
                        int c = startcol + j;
                        if (c < 0 || c >= inventorySlots.GetLength(1)) continue;

                        if (inventorySlots[r, c] != null)
                        {
                            inventorySlots[r, c].anchorSlot = anchor;
                            if (inventorySlots[r, c].BackgroundImage != null)
                                inventorySlots[r, c].BackgroundImage.color = inventorySlots[r, c].DefaultBackgroundColor;
                        }
                    }
                }

                dataModel.NotifySlotAreaChanged(anchor);
            }
        }

        public bool IsValidPosition(InventorySlot targetSlot, Vector2Int itemSize)
        {
            if (targetSlot == null) return false;
            InventorySlot[,] inventorySlots = dataModel.GetGridForSlot(targetSlot) ?? (targetSlot.IsInventorySlot ? dataModel.InventorySlots : dataModel.ChestSlots);
            if (inventorySlots == null) return false;

            if (!IsSlotWithinBounds(targetSlot.row, targetSlot.col, itemSize, inventorySlots)) return false;

            int startRow = targetSlot.row;
            int startCol = targetSlot.col;
            for (int i = 0; i < itemSize.y; i++)
            {
                for (int j = 0; j < itemSize.x; j++)
                {
                    InventorySlot slotAtCoord = inventorySlots[startRow + i, startCol + j];
                    InventorySlot anchor = slotAtCoord != null ? slotAtCoord.anchorSlot : null;
                    if (inventoryManager.IsSlotOccupied(anchor)) return false;
                }
            }
            return true;
        }

        public InventorySlot FindAvailableSlotHotbar()
        {
            InventorySlot[] hotbarSlots = dataModel.HotbarSlots ?? inventoryManager.HotbarSlots;
            if (hotbarSlots == null) return null;
            foreach (var slot in hotbarSlots)
            {
                if (slot.slotData == null || slot.slotData.item == null)
                    return slot;
            }
            return null;
        }

        public InventorySlot FindAvailableSlotInventory(Item_SO item)
        {
            return FindAvailableSlot(item, dataModel.InventorySlots);
        }

        public InventorySlot FindAvailableSlotChest(Item_SO item)
        {
            return FindAvailableSlot(item, dataModel.ChestSlots);
        }

        public InventorySlot FindAvailableSlotBackpack(Item_SO item)
        {
            return FindAvailableSlot(item, dataModel.BackpackSlots);
        }

        public InventorySlot FindAvailableSlot(Item_SO item, InventorySlot[,] array)
        {
            if (array == null || item == null) return null;
            Vector2Int size = inventoryManager?._GridGenerator != null && inventoryManager._GridGenerator.IsGridInventory()
                ? new Vector2Int(1, 1)
                : item.itemSize;

            foreach (var slot in array)
            {
                if (slot == null) continue;
                if ((slot.slotData == null || slot.slotData.item == null) && IsValidPosition(slot, size))
                    return slot;
                if (slot.slotData != null && slot.slotData.item == item && slot.slotData.amount < item.maxStack)
                    return slot;
            }
            return null;
        }
    }
}
