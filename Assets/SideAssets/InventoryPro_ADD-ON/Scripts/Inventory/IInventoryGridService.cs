using UnityEngine;

namespace cowsins.Inventory
{
    /// <summary>
    /// Responsible for inventory grid calculations, boundaries, spatial queries, and slot finding.
    /// </summary>
    public interface IInventoryGridService
    {
        bool ItemFits(InventorySlot slot, Vector2Int itemSize, int row, int col, bool isInventorySlot);
        void ClearSlotArea(InventorySlot slot);
        void UpdateSlotsSize(InventorySlot[,] slotsArray);
        bool IsSlotWithinBounds(int row, int col, Vector2Int itemSize, InventorySlot[,] inventorySlots);
        bool IsSlotWithinBounds(int targetRow, int targetCol, InventorySlot[,] inventorySlots);
        bool IsInventoryFull();
        (int row, int col) GetRowAndColumnOutOfIndex(int index);
        void ReassignSlots(InventorySlot slot);
        bool IsValidPosition(InventorySlot targetSlot, Vector2Int itemSize);
        InventorySlot FindAvailableSlotHotbar();
        InventorySlot FindAvailableSlotInventory(Item_SO item);
        InventorySlot FindAvailableSlotChest(Item_SO item);
        InventorySlot FindAvailableSlotBackpack(Item_SO item);
        InventorySlot FindAvailableSlot(Item_SO item, InventorySlot[,] array);
    }
}
