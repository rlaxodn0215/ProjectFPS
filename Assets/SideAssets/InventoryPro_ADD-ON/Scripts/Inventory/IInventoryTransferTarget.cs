namespace cowsins.Inventory
{
    /// <summary>
    /// Higher priority targets are evaluated first.
    /// </summary>
    public interface IInventoryTransferTarget
    {
        /// <summary>
        /// Unique identifier for this container
        /// </summary>
        string ContainerId { get; }

        /// <summary>
        /// Context menu action label for transferring to this target ("MOVE TO CHEST", "MOVE TO BACKPACK")
        /// </summary>
        string ActionLabel { get; }

        /// <summary>
        /// Priority of this container target. Higher values are evaluated first.
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// Whether this container is currently accessible
        /// </summary>
        bool IsAvailable { get; }

        /// <summary>
        /// Whether this container can accept the given source slot.
        /// </summary>
        bool CanAccept(InventorySlot sourceSlot);

        /// <summary>
        /// Attempts to transfer the item from sourceSlot into this container.
        /// Returns true if the transfer was successful.
        /// </summary>
        bool TryTransfer(InventorySlot sourceSlot);
    }
}
