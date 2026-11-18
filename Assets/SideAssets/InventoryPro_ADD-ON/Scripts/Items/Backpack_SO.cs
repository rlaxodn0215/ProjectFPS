using System;
using UnityEngine;
using cowsins.Inventory;

namespace cowsins
{
    [System.Serializable]
    [CreateAssetMenu(fileName = "newBackpackItem", menuName = "COWSINS/Inventory/New Backpack Item", order = 3)]
    public class Backpack_SO : Item_SO, IContainerItem
    {
        [Header("Container Grid Settings")]
        [Tooltip("(Rows = X, Columns = Y)")]
        [SerializeField] private Vector2Int containerSize = new Vector2Int(3, 4);

        [Tooltip("Weight reduction percentage applied to items stored inside this backpack (.20 means items feel 20% lighter).")]
        [SerializeField, Range(0f, 1f)] private float weightReductionPercentage = 0.2f;

        [Tooltip("If populated, only items matching these types or assets can be stored inside.")]
        [SerializeField] private Item_SO[] allowedItemTypes;

        [Tooltip("Starter contents populated inside this backpack when instantiated.")]
        [SerializeField] private SlotData[] defaultContents;

        public Vector2Int ContainerSize => containerSize;
        public int Rows => Mathf.Max(1, containerSize.x);
        public int Columns => Mathf.Max(1, containerSize.y);
        public float WeightReductionPercentage => weightReductionPercentage;
        public Item_SO[] AllowedItemTypes => allowedItemTypes;
        public SlotData[] DefaultContents => defaultContents;

        /// <summary>
        /// Validates whether the given item is allowed into this backpack based on filter lists
        /// </summary>
        public virtual bool CanStoreItem(Item_SO candidateItem)
        {
            if (candidateItem == null) return true;

            // Never allow storing containers inside containers
            if (candidateItem is IContainerItem) return false;

            // If an item filter is defined, verify match
            if (allowedItemTypes != null && allowedItemTypes.Length > 0)
            {
                bool matches = false;
                for (int i = 0; i < allowedItemTypes.Length; i++)
                {
                    Item_SO allowed = allowedItemTypes[i];
                    if (allowed == null) continue;

                    if (allowed == candidateItem || allowed.GetType().IsAssignableFrom(candidateItem.GetType()))
                    {
                        matches = true;
                        break;
                    }
                }
                if (!matches) return false;
            }

            return true;
        }

        public override bool CanUse(InventorySlot slot, InventoryProManager manager) => true;

        public override string GetContextActionName(InventorySlot slot, InventoryProManager manager)
        {
            if (manager != null && manager.isBackpackOpen && manager.BackpackBehaviour?.ActiveBackpackSlot == slot?.GetAnchorSlot())
                return "CLOSE";
            return "OPEN";
        }

        public override bool ShowUseToast => false;

        public override SlotData[] CreateDefaultContainedItems()
        {
            if (defaultContents == null || defaultContents.Length == 0) return null;
            SlotData[] copy = new SlotData[defaultContents.Length];
            for (int i = 0; i < defaultContents.Length; i++)
                copy[i] = defaultContents[i]?.Clone() ?? new SlotData();
            return copy;
        }

        /// <summary>
        /// Using a backpack directly toggles its container view.
        /// </summary>
        public override void Use(InventoryProManager inventoryProManager, InventorySlot inventorySlot)
        {
            if (inventoryProManager == null || inventorySlot == null) return;
            inventoryProManager.ToggleBackpack(inventorySlot);
        }

        public override void OnEquipCustomSlot(InventorySlot slot, PlayerDependencies player)
        {
            base.OnEquipCustomSlot(slot, player);
            if (InventoryProManager.instance != null)
                InventoryProManager.instance.OnBackpackEquipped(slot);
        }

        public override void OnUnequipCustomSlot(InventorySlot slot, PlayerDependencies player)
        {
            base.OnUnequipCustomSlot(slot, player);
            if (InventoryProManager.instance != null)
                InventoryProManager.instance.OnBackpackUnequipped(slot);
        }
    }
}
