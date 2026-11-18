namespace cowsins.Inventory
{
    /// <summary>
    /// Policy for Custom / Armor / Accessory slots
    /// </summary>
    public sealed class CustomSlotPolicy : ISlotPolicy
    {
        public SlotType SlotType => SlotType.Custom;
        public bool IsGridBased => false;
        public bool AllowsItemRotation => false;
        public bool AllowsSplitting => false;
        public bool AllowsDurabilityLoss => true;
        public bool AffectsPlayerWeight => false;
        public bool IsExternalContainer => false;
        public bool AllowsFastTransfer => false;
        public bool AllowsItemUsage => false;
        public bool AllowsFavoritesPinning => false;
        public bool CanLootAll => false;

        public void LootAll(InventoryProManager manager) { }

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
            slot?.UpdateCustomSlotEffects();
            manager?.onCustomSlotChanged?.Invoke(slot);
        }

        public void OnItemRemoved(InventorySlot slot, InventoryProManager manager)
        {
            slot?.UpdateCustomSlotEffects();
            manager?.onCustomSlotChanged?.Invoke(slot);
        }

        public void OnSlotModified(InventorySlot slot, InventoryProManager manager)
        {
            slot?.UpdateCustomSlotEffects();
            manager?.onCustomSlotChanged?.Invoke(slot);
        }
    }
}
