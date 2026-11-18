using UnityEngine;

namespace cowsins.Inventory
{
    /// <summary>
    /// Policy for Backpack container slots
    /// </summary>
    public sealed class BackpackSlotPolicy : ISlotPolicy
    {
        public SlotType SlotType => SlotType.Backpack;
        public bool IsGridBased => true;
        public bool AllowsItemRotation => true;
        public bool AllowsSplitting => true;
        public bool AllowsDurabilityLoss => false;
        public bool AffectsPlayerWeight => true;
        public bool IsExternalContainer => false;
        public bool AllowsFastTransfer => true;
        public bool AllowsItemUsage => true;
        public bool AllowsFavoritesPinning => true;
        public bool CanLootAll => true;

        public bool AcceptsItem(InventorySlot slot, Item_SO item)
        {
            if (item == null) return true;

            // Never allow storing containers inside containers
            if (item is IContainerItem) return false;

            // Check slot-level type restrictions if assigned
            if (slot != null && slot.AcceptedType != null && slot.AcceptedType != typeof(Item_SO))
            {
                if (!slot.AcceptedType.IsAssignableFrom(item.GetType()))
                    return false;
            }

            var activeContainer = InventoryProManager.instance?.ActiveContainerItem;
            if (activeContainer != null)
            {
                if (!activeContainer.CanStoreItem(item))
                    return false;
            }

            return true;
        }

        public void OnBeforeItemRemoved(InventorySlot slot, InventoryProManager manager) { }

        public void LootAll(InventoryProManager manager) => manager?._GridGenerator?.BackpackBehaviour?.LootAllBackpack();

        public void OnItemPlaced(InventorySlot slot, InventoryProManager manager)
        {
            SyncBackpackData(manager);
            if (slot?.slotData?.item is BulletTypeIdentifier_SO)
                manager?.CalculateTotalBullets(false);
        }

        public void OnItemRemoved(InventorySlot slot, InventoryProManager manager)
        {
            SyncBackpackData(manager);
            if (slot?.slotData?.item is BulletTypeIdentifier_SO)
                manager?.CalculateTotalBullets(false);
        }

        public void OnSlotModified(InventorySlot slot, InventoryProManager manager) => SyncBackpackData(manager);

        public static void SyncBackpackData(InventoryProManager manager)
        {
            if (manager == null || manager._GridGenerator == null || manager._GridGenerator.BackpackBehaviour == null)
                return;

            manager._GridGenerator.BackpackBehaviour.SyncActiveBackpackData();
        }
    }
}
