using System;
using UnityEngine;

namespace cowsins.Inventory
{
    /// <summary>
    /// Data model representing the state of the inventory grid, hotbar, and chest.
    /// </summary>
    public class InventoryDataModel
    {
        public InventorySlot[,] InventorySlots { get; set; }
        public InventorySlot[] HotbarSlots { get; set; }
        public InventorySlot[,] ChestSlots { get; set; }
        public Chest CachedChest { get; set; }
        public InventorySlot[,] BackpackSlots { get; set; }
        public InventorySlot ActiveBackpackSlot { get; set; }

        public int Rows { get; private set; }
        public int Columns { get; private set; }

        public event Action<InventorySlot> OnSlotChanged;
        public event Action<InventorySlot> OnSlotAreaChanged;
        public event Action OnGridChanged;

        public InventoryDataModel(int rows, int columns)
        {
            Rows = rows;
            Columns = columns;
        }

        public void SetDimensions(int rows, int columns)
        {
            if (rows > 0) Rows = rows;
            if (columns > 0) Columns = columns;
        }

        /// <summary>
        /// Returns the grid array (Inventory, Chest, or Backpack) corresponding to the given slot.
        /// </summary>
        public InventorySlot[,] GetGridForSlot(InventorySlot slot)
        {
            if (slot == null || slot.Policy == null) return null;
            return slot.Policy.SlotType switch
            {
                SlotType.Inventory => InventorySlots,
                SlotType.Chest => ChestSlots,
                SlotType.Backpack => BackpackSlots,
                _ => null
            };
        }

        /// Notifies observers that a single slot state has changed
        public void NotifySlotChanged(InventorySlot slot)
        {
            if (slot == null) return;
            OnSlotChanged?.Invoke(slot);
        }

        /// Notifies observers that an items anchor and all occupying slots in its area have changed
        public void NotifySlotAreaChanged(InventorySlot anchor)
        {
            if (anchor == null) return;
            OnSlotAreaChanged?.Invoke(anchor);
        }

        /// Notifies observers that the entire grid layout or state has changed
        public void NotifyGridChanged() => OnGridChanged?.Invoke();
    }
}
