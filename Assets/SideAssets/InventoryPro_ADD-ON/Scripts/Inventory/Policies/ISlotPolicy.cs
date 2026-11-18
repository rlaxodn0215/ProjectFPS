using UnityEngine;

namespace cowsins.Inventory
{
    /// <summary>
    /// Defines capabilities, validation, and lifecycle hooks for inventory slots
    /// </summary>
    public interface ISlotPolicy
    {
        SlotType SlotType { get; }
        bool IsGridBased { get; }
        bool AllowsItemRotation { get; }
        bool AllowsSplitting { get; }
        bool AllowsDurabilityLoss { get; }
        bool AffectsPlayerWeight { get; }
        bool IsExternalContainer { get; }
        bool AllowsFastTransfer { get; }
        bool AllowsItemUsage { get; }
        bool AllowsFavoritesPinning { get; }
        bool CanLootAll { get; }
        bool AcceptsItem(InventorySlot slot, Item_SO item);
        void OnBeforeItemRemoved(InventorySlot slot, InventoryProManager manager);
        void OnItemPlaced(InventorySlot slot, InventoryProManager manager);
        void OnItemRemoved(InventorySlot slot, InventoryProManager manager);
        void OnSlotModified(InventorySlot slot, InventoryProManager manager);
        void LootAll(InventoryProManager manager);
    }
}
