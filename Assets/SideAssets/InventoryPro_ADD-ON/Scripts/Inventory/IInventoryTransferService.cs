namespace cowsins.Inventory
{
    /// <summary>
    /// Handling priority-based container transfer routing
    /// </summary>
    public interface IInventoryTransferService
    {
        /// <summary>
        /// Registers a new container transfer target.
        /// </summary>
        void RegisterTarget(IInventoryTransferTarget target);

        /// <summary>
        /// Unregisters an existing container transfer target.
        /// </summary>
        void UnregisterTarget(IInventoryTransferTarget target);

        /// <summary>
        /// Executes a quick transfer of the item in sourceSlot to the highest priority available container.
        /// </summary>
        bool QuickTransfer(InventorySlot sourceSlot);

        /// <summary>
        /// Returns the highest-priority transfer target available to accept the source slot, or null if none
        /// </summary>
        IInventoryTransferTarget GetTransferTarget(InventorySlot sourceSlot);
    }
}
