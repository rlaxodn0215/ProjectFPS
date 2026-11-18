using UnityEngine;

namespace cowsins.Inventory
{
    /// <summary>
    /// Required by Grid Generator. It allows to store information & Customization about Inventories & Chests.
    /// Improves modularity and allows to easily create different types of Inventories.
    /// See InventoryDesigner in CowsinsManager for more information
    /// </summary>
    public class InventoryGridData_SO : ScriptableObject
    {
        // Size of the Inventory = rows * columns ( Amount of slots available )
        public int rows = 5;
        public int columns = 5;

        // InventorySlot Prefab
        public GameObject buttonPrefab;

        // Padding & Spacing Customization
        public Padding inventoryPadding;

        // SlotData contains essential information about the Item a slot carries, its amount, attachments, bullets, and much more.
        // inventoryInitialData contains information of each Slot in the Inventory.
        // Size of inventoryInitialData equals rows * columns
        public SlotData[] inventoryInitialData;
    }
}