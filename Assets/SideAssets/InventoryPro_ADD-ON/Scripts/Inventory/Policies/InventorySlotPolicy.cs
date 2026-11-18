namespace cowsins.Inventory
{
    public sealed class InventorySlotPolicy : ISlotPolicy
    {
        public SlotType SlotType => SlotType.Inventory;
        public bool IsGridBased => true;
        public bool AllowsItemRotation => true;
        public bool AllowsSplitting => true;
        public bool AllowsDurabilityLoss => false;
        public bool AffectsPlayerWeight => true;
        public bool IsExternalContainer => false;
        public bool AllowsFastTransfer => true;
        public bool AllowsItemUsage => true;
        public bool AllowsFavoritesPinning => true;
        public bool CanLootAll => false;

        public bool AcceptsItem(InventorySlot slot, Item_SO item)
        {
            if (item == null) return true;
            if (slot != null && slot.AcceptedType != null && slot.AcceptedType != typeof(Item_SO))
                return slot.AcceptedType.IsAssignableFrom(item.GetType());
            return true;
        }

        public void OnBeforeItemRemoved(InventorySlot slot, InventoryProManager manager) { }

        public void OnItemPlaced(InventorySlot slot, InventoryProManager manager)
        {
            if (slot?.slotData?.item is BulletTypeIdentifier_SO)
                manager?.CalculateTotalBullets(false);
        }

        public void OnItemRemoved(InventorySlot slot, InventoryProManager manager)
        {
            if (slot?.slotData?.item is BulletTypeIdentifier_SO)
                manager?.CalculateTotalBullets(false);
        }

        public void OnSlotModified(InventorySlot slot, InventoryProManager manager) { }

        public void LootAll(InventoryProManager manager) { }
    }
}
