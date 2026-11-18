using System.Collections.Generic;

namespace cowsins.Inventory
{
    /// <summary>
    /// Implements priority-based quick transfer routing across registered containers.
    /// </summary>
    public sealed class InventoryTransferService : IInventoryTransferService
    {
        private readonly List<IInventoryTransferTarget> targets = new List<IInventoryTransferTarget>();

        public void RegisterTarget(IInventoryTransferTarget target)
        {
            if (target == null || targets.Contains(target)) return;
            targets.Add(target);
            // Sort by descending priority so highest priority targets evaluate first
            targets.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }

        public void UnregisterTarget(IInventoryTransferTarget target)
        {
            if (target != null) targets.Remove(target);
        }

        public bool QuickTransfer(InventorySlot sourceSlot)
        {
            if (sourceSlot == null || !sourceSlot.HasValidItem) return false;

            InventorySlot anchor = sourceSlot.GetAnchorSlot();
            if (anchor == null || !anchor.HasValidItem) return false;

            for (int i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                if (target.IsAvailable && target.CanAccept(anchor))
                {
                    if (target.TryTransfer(anchor))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public IInventoryTransferTarget GetTransferTarget(InventorySlot sourceSlot)
        {
            if (sourceSlot == null || !sourceSlot.HasValidItem) return null;

            InventorySlot anchor = sourceSlot.GetAnchorSlot();
            if (anchor == null || !anchor.HasValidItem) return null;

            for (int i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                if (target.IsAvailable && target.CanAccept(anchor))
                {
                    return target;
                }
            }

            return null;
        }
    }
}
