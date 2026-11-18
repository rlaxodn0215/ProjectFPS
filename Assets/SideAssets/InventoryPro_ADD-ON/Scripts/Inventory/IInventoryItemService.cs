namespace cowsins.Inventory
{
    /// <summary>
    /// responsible for item additions, removals, modifications, drag-and-drop operations, and sorting.
    /// </summary>
    public interface IInventoryItemService
    {
        (bool success, int remainingAmount) AddItemToInventory(Item_SO item_SO, int amount, SlotData[] containedItems = null);
        bool AddWeaponToInventory(Weapon_SO weapon_SO, int bulletsLeftInmagazine, int totalBullets, WeaponPickeable weaponPickeable = null);
        void SortInventory();
        int GetItemTypePriority(Item_SO item);
        void StackItems(InventorySlot slot, bool playSFX);
        void SwapItems(InventorySlot targetSlot, bool playSFX);
        void MoveItemTo(InventorySlot currentSlot, InventorySlot targetSlot, bool playSFX = true);
        bool CanStackItems(SlotData slotData);
        void RotateItem(int rot);
        void DeleteItem(InventorySlot slot, bool notify = true);
        void SplitOutsideInventory(InventorySlot slot);
        void DropOutsideInventory(InventorySlot slot);
        bool HandleDropOutsideAndItemDeletion(InventorySlot anchorSlot);
        void OnEndDrag(bool clearCurrentReferences);
        bool DepositSingleItem(InventorySlot sourceSlot, InventorySlot targetSlot);
        bool CanSwapGridItems(InventorySlot anchorA, InventorySlot anchorB);
        void SwapGridItems(InventorySlot anchorA, InventorySlot anchorB, bool playSFX = true);
        bool CanSwapOrMoveSingleAndGrid(InventorySlot singleSlot, InventorySlot gridSlot);
        bool SwapOrMoveSingleAndGrid(InventorySlot singleSlot, InventorySlot gridSlot);
        bool SwapOrMoveSingleSlots(InventorySlot slotA, InventorySlot slotB);
        bool CanPlaceOrSwap(InventorySlot sourceSlot, InventorySlot targetSlot);
        bool ExecutePlacementOrSwap(InventorySlot source, InventorySlot target, bool playSFX = true);
        bool TransferWeaponToHotbarSlot(InventorySlot sourceSlot, InventorySlot hotbarSlot);
        void HandleHotbarNumberKey(int hotbarIndex);
    }
}
