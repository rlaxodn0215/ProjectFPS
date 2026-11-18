using UnityEngine;

namespace cowsins.Inventory
{
    /// <summary>
    /// Coordinating system for inventory operations
    /// </summary>
    public sealed class InventoryOperationsBehaviour
    {
        private readonly IInventoryGridService gridService;
        private readonly IInventoryCraftingService craftingService;
        private readonly IInventoryItemService itemService;
        private readonly IInventoryFeedbackService feedbackService;
        private readonly IInventoryTransferService transferService;

        public IInventoryGridService GridService => gridService;
        public IInventoryCraftingService CraftingService => craftingService;
        public IInventoryItemService ItemService => itemService;
        public IInventoryFeedbackService FeedbackService => feedbackService;
        public IInventoryTransferService TransferService => transferService;

        public InventoryOperationsBehaviour(InventoryProManager inventoryManager, InventoryGridData_SO inventoryGridData, InvWeightCalculator weightCalculator,
            WeaponController weaponController, InventoryDataModel dataModel, IInventoryGridService gridService = null, IInventoryCraftingService craftingService = null,
            IInventoryItemService itemService = null, IInventoryFeedbackService feedbackService = null, IInventoryTransferService transferService = null)
        {
            this.feedbackService = feedbackService ?? new InventoryFeedbackService(inventoryManager);
            this.gridService = gridService ?? new InventoryGridService(inventoryManager, inventoryGridData, dataModel);
            this.craftingService = craftingService ?? new InventoryCraftingService(dataModel, weightCalculator, inventoryManager);
            this.itemService = itemService ?? new InventoryItemService(inventoryManager, inventoryGridData, weightCalculator, weaponController, dataModel, this.gridService, this.feedbackService);
            this.transferService = transferService ?? new InventoryTransferService();

            this.transferService.RegisterTarget(new ChestTransferTarget(inventoryManager, dataModel, this.gridService, this.feedbackService, weightCalculator, weaponController));
            this.transferService.RegisterTarget(new BackpackTransferTarget(inventoryManager, dataModel, this.gridService, this.feedbackService, weightCalculator, weaponController));
            this.transferService.RegisterTarget(new PlayerInventoryTransferTarget(inventoryManager, dataModel, this.gridService, this.feedbackService, weightCalculator, weaponController));
            this.transferService.RegisterTarget(new HotbarTransferTarget(inventoryManager, dataModel, this.gridService, this.feedbackService, weightCalculator, weaponController));
            this.transferService.RegisterTarget(new CustomSlotTransferTarget(inventoryManager, dataModel, this.feedbackService, weightCalculator));
        }

        // GRID & SPATIAL QUERIES

        public bool ItemFits(InventorySlot slot, Vector2Int itemSize, int row, int col, bool isInventorySlot) => gridService.ItemFits(slot, itemSize, row, col, isInventorySlot);

        public void ClearSlotArea(InventorySlot slot) =>  gridService.ClearSlotArea(slot);

        public void UpdateSlotsSize(InventorySlot[,] slotsArray) => gridService.UpdateSlotsSize(slotsArray);

        public bool IsSlotWithinBounds(int row, int col, Vector2Int itemSize, InventorySlot[,] inventorySlots) => gridService.IsSlotWithinBounds(row, col, itemSize, inventorySlots);

        public bool IsSlotWithinBounds(int targetRow, int targetCol, InventorySlot[,] inventorySlots) => gridService.IsSlotWithinBounds(targetRow, targetCol, inventorySlots);

        public bool IsInventoryFull() =>gridService.IsInventoryFull();

        public (int row, int col) GetRowAndColumnOutOfIndex(int index) => gridService.GetRowAndColumnOutOfIndex(index);

        public void ReassignSlots(InventorySlot slot) => gridService.ReassignSlots(slot);

        public bool IsValidPosition(InventorySlot targetSlot, Vector2Int itemSize) => gridService.IsValidPosition(targetSlot, itemSize);

        public InventorySlot FindAvailableSlotHotbar() => gridService.FindAvailableSlotHotbar();

        public InventorySlot FindAvailableSlotInventory(Item_SO item) => gridService.FindAvailableSlotInventory(item);

        public InventorySlot FindAvailableSlotChest(Item_SO item) => gridService.FindAvailableSlotChest(item);
        public InventorySlot FindAvailableSlotBackpack(Item_SO item) => gridService.FindAvailableSlotBackpack(item);

        // CRAFTING QUERIES & ACTIONS

        public bool HasEnoughIngredients(Recipe_SO recipe, int craftAmount) => craftingService.HasEnoughIngredients(recipe, craftAmount);

        public bool HasEnoughOfItem(Item_SO item, int requiredAmount) => craftingService.HasEnoughOfItem(item, requiredAmount);

        public int GetAmountOfItem(Item_SO item) => craftingService.GetAmountOfItem(item);

        public bool PayIngredients(Recipe_SO recipe, int craftAmount) => craftingService.PayIngredients(recipe, craftAmount);

        // ITEM MUTATIONS & TRANSFERS

        public (bool, int) AddItemToInventory(Item_SO item_SO, int amount, SlotData[] containedItems = null) => itemService.AddItemToInventory(item_SO, amount, containedItems);

        public bool AddWeaponToInventory(Weapon_SO weapon_SO, int bulletsLeftInmagazine, int totalBullets, WeaponPickeable weaponPickeable = null) => itemService.AddWeaponToInventory(weapon_SO, bulletsLeftInmagazine, totalBullets, weaponPickeable);

        public void SortInventory() => itemService.SortInventory();

        public int GetItemTypePriority(Item_SO item) => itemService.GetItemTypePriority(item);

        public void StackItems(InventorySlot slot, bool playSFX) => itemService.StackItems(slot, playSFX);

        public void SwapItems(InventorySlot targetSlot, bool playSFX) => itemService.SwapItems(targetSlot, playSFX);

        public void MoveItemTo(InventorySlot currentSlot, InventorySlot targetSlot, bool playSFX = true) => itemService.MoveItemTo(currentSlot, targetSlot, playSFX);

        public bool CanStackItems(SlotData slotData) => itemService.CanStackItems(slotData);

        public void RotateItem(int rot) => itemService.RotateItem(rot);

        public void DeleteItem(InventorySlot slot, bool notify = true) => itemService.DeleteItem(slot, notify);

        public void SplitOutsideInventory(InventorySlot slot) => itemService.SplitOutsideInventory(slot);

        public void DropOutsideInventory(InventorySlot slot) => itemService.DropOutsideInventory(slot);

        public bool HandleDropOutsideAndItemDeletion(InventorySlot anchorSlot) => itemService.HandleDropOutsideAndItemDeletion(anchorSlot);

        public void OnEndDrag(bool clearCurrentReferences) => itemService.OnEndDrag(clearCurrentReferences);
        
        public bool DepositSingleItem(InventorySlot sourceSlot, InventorySlot targetSlot) => itemService.DepositSingleItem(sourceSlot, targetSlot);

        public bool QuickTransfer(InventorySlot sourceSlot) => transferService.QuickTransfer(sourceSlot);
        public IInventoryTransferTarget GetTransferTarget(InventorySlot sourceSlot) => transferService.GetTransferTarget(sourceSlot);

        public bool CanSwapGridItems(InventorySlot anchorA, InventorySlot anchorB) => itemService.CanSwapGridItems(anchorA, anchorB);

        public void SwapGridItems(InventorySlot anchorA, InventorySlot anchorB, bool playSFX = true) => itemService.SwapGridItems(anchorA, anchorB, playSFX);

        public bool CanSwapOrMoveSingleAndGrid(InventorySlot singleSlot, InventorySlot gridSlot) => itemService.CanSwapOrMoveSingleAndGrid(singleSlot, gridSlot);

        public bool SwapOrMoveSingleAndGrid(InventorySlot singleSlot, InventorySlot gridSlot) => itemService.SwapOrMoveSingleAndGrid(singleSlot, gridSlot);

        public bool SwapOrMoveSingleSlots(InventorySlot slotA, InventorySlot slotB) => itemService.SwapOrMoveSingleSlots(slotA, slotB);

        public bool CanPlaceOrSwap(InventorySlot sourceSlot, InventorySlot targetSlot) => itemService.CanPlaceOrSwap(sourceSlot, targetSlot);

        public bool ExecutePlacementOrSwap(InventorySlot source, InventorySlot target, bool playSFX = true) => itemService.ExecutePlacementOrSwap(source, target, playSFX);

        public bool TransferWeaponToHotbarSlot(InventorySlot sourceSlot, InventorySlot hotbarSlot) => itemService.TransferWeaponToHotbarSlot(sourceSlot, hotbarSlot);

        public void HandleHotbarNumberKey(int hotbarIndex) => itemService.HandleHotbarNumberKey(hotbarIndex);
    }
}
