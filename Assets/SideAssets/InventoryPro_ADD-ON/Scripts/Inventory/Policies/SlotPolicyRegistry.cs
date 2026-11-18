using System.Collections.Generic;

namespace cowsins.Inventory
{
    /// <summary>
    /// Central registry for slot policies
    /// </summary>
    public static class SlotPolicyRegistry
    {
        public static readonly ISlotPolicy InventoryPolicy = new InventorySlotPolicy();
        public static readonly ISlotPolicy HotbarPolicy = new HotbarSlotPolicy();
        public static readonly ISlotPolicy ChestPolicy = new ChestSlotPolicy();
        public static readonly ISlotPolicy CustomPolicy = new CustomSlotPolicy();
        public static readonly ISlotPolicy BackpackPolicy = new BackpackSlotPolicy();

        private static readonly Dictionary<SlotType, ISlotPolicy> customRegistry = new Dictionary<SlotType, ISlotPolicy>();

        public static ISlotPolicy GetPolicy(SlotType type)
        {
            if (customRegistry.TryGetValue(type, out var customPolicy))
                return customPolicy;

            switch (type)
            {
                case SlotType.Hotbar:
                    return HotbarPolicy;
                case SlotType.Chest:
                    return ChestPolicy;
                case SlotType.Custom:
                    return CustomPolicy;
                case SlotType.Backpack:
                    return BackpackPolicy;
                case SlotType.Inventory:
                default:
                    return InventoryPolicy;
            }
        }

        public static void RegisterPolicy(SlotType type, ISlotPolicy policy)
        {
            if (policy != null)
                customRegistry[type] = policy;
            else
                customRegistry.Remove(type);
        }
    }
}
