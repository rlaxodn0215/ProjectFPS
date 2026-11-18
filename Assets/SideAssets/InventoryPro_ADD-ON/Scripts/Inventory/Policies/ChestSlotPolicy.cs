namespace cowsins.Inventory
{
    /// <summary>
    /// Policy for Chest
    /// </summary>
    public sealed class ChestSlotPolicy : ISlotPolicy
    {
        public SlotType SlotType => SlotType.Chest;
        public bool IsGridBased => true;
        public bool AllowsItemRotation => false;
        public bool AllowsSplitting => true;
        public bool AllowsDurabilityLoss => false;
        public bool AffectsPlayerWeight => false;
        public bool IsExternalContainer => true;
        public bool AllowsFastTransfer => true;
        public bool AllowsItemUsage => false;
        public bool AllowsFavoritesPinning => false;
        public bool CanLootAll => true;

        public bool AcceptsItem(InventorySlot slot, Item_SO item)
        {
            if (item == null) return true;
            if (slot != null && slot.AcceptedType != null && slot.AcceptedType != typeof(Item_SO))
                return slot.AcceptedType.IsAssignableFrom(item.GetType());
            return true;
        }

        public void OnBeforeItemRemoved(InventorySlot slot, InventoryProManager manager) { }

        public void OnItemPlaced(InventorySlot slot, InventoryProManager manager) => SyncChestData(manager);

        public void OnItemRemoved(InventorySlot slot, InventoryProManager manager) => SyncChestData(manager);

        public void OnSlotModified(InventorySlot slot, InventoryProManager manager) => SyncChestData(manager);

        public void LootAll(InventoryProManager manager) => manager?._GridGenerator?.ChestBehaviour?.LootAllChest();

        public static void SyncChestData(InventoryProManager manager)
        {
            if (manager == null || manager.DataModel == null) return;
            var cachedChest = manager.DataModel.CachedChest;
            var chestSlots = manager.DataModel.ChestSlots;
            if (cachedChest == null || chestSlots == null) return;

            int rows = chestSlots.GetLength(0);
            int cols = chestSlots.GetLength(1);
            SlotData[] chestSlotsData = new SlotData[rows * cols];

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    InventorySlot slot = chestSlots[r, c];
                    chestSlotsData[r * cols + c] = slot != null ? slot.slotData : new SlotData();
                }
            }

            cachedChest.SetChestData(chestSlotsData);
        }
    }
}
