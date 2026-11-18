using System.Collections.Generic;

namespace cowsins.Inventory
{
    public sealed class InvProAttachmentBehaviour
    {
        private readonly InventoryProManager manager;

        public InvProAttachmentBehaviour(InventoryProManager manager)
        {
            this.manager = manager;
        }

        public bool DragAttachmentOnWeaponSlot(bool clearCurrentReferences)
        {
            InventorySlot draggedAnchor = manager.draggedSlot.GetAnchorSlot();
            InventorySlot highlightedAnchor = manager.highlightedSlot.GetAnchorSlot();
            bool canAttachOnWeapon = IsValidAttachmentWeaponPair(draggedAnchor, highlightedAnchor);

            if (canAttachOnWeapon)
            {
                (bool success, Attachment atc, int id) =
                    CowsinsUtilities.CompatibleAttachment(((Weapon_SO)highlightedAnchor.slotData.item).weaponObject, (AttachmentIdentifier_SO)draggedAnchor.slotData.item);

                if (success)
                {
                    manager.FeedbackService?.ShowAttachmentAddedToast(draggedAnchor.slotData.item._name, highlightedAnchor.slotData.item._name);
                    manager.FeedbackService?.OnItemPlaced();

                    manager._GridGenerator.Operations.ClearSlotArea(draggedAnchor);
                    manager.NavigationBehaviour.ResetHighlightedSlotsArea(manager.draggedSlot.DefaultBackgroundColor, clearCurrentReferences);

                    switch (atc)
                    {
                        case Barrel barrel:
                            SwapAttachmentOnWeapon(highlightedAnchor, ref highlightedAnchor.slotData.barrel, barrel);
                            break;
                        case Scope scope:
                            SwapAttachmentOnWeapon(highlightedAnchor, ref highlightedAnchor.slotData.scope, scope);
                            break;
                        case Stock stock:
                            SwapAttachmentOnWeapon(highlightedAnchor, ref highlightedAnchor.slotData.stock, stock);
                            break;
                        case Grip grip:
                            SwapAttachmentOnWeapon(highlightedAnchor, ref highlightedAnchor.slotData.grip, grip);
                            break;
                        case Magazine magazine:
                            SwapAttachmentOnWeapon(highlightedAnchor, ref highlightedAnchor.slotData.magazine, magazine);
                            break;
                        case Flashlight flashlight:
                            SwapAttachmentOnWeapon(highlightedAnchor, ref highlightedAnchor.slotData.flashlight, flashlight);
                            break;
                        case Laser laser:
                            SwapAttachmentOnWeapon(highlightedAnchor, ref highlightedAnchor.slotData.laser, laser);
                            break;
                    }

                    // Handle Attachments for Hotbars so they are immediately assigned after dropping the attachment on player
                    if (highlightedAnchor && highlightedAnchor.IsHotbarSlot)
                    {
                        SlotData highlightedSlotData = highlightedAnchor.slotData;
                        int colId = highlightedAnchor.col;
                        var attachments = new[] { highlightedSlotData.barrel, highlightedSlotData.scope, highlightedSlotData.stock, highlightedSlotData.grip,
                        highlightedSlotData.magazine, highlightedSlotData.flashlight, highlightedSlotData.laser };
                        foreach (var attachment in attachments)
                        {
                            manager._WeaponController.attachmentsSystem.AssignNewAttachmentToWeapon(attachment, colId);
                        }

                        // Unholster the weapon so we can actually use it
                        if (manager._WeaponController.CurrentWeaponIndex == colId) manager._WeaponController.weaponInventoryBehaviour.SelectWeapon();
                    }

                    return true;
                }
            }
            return false;
        }

        private void SwapAttachmentOnWeapon<T>(InventorySlot highlightedAnchor, ref AttachmentIdentifier_SO atcIdentifier, T attachment) where T : Attachment
        {
#if INVENTORY_PRO_ADD_ON
            if (!highlightedAnchor.IsHotbarSlot && atcIdentifier != null)
                manager._InteractManager.TryAddAttachmentToInventory(atcIdentifier);
#endif
            atcIdentifier = attachment.attachmentIdentifier;
        }

        public bool CanAttachDraggedAttachmentToWeaponSlot()
        {
            InventorySlot draggedAnchor = manager.draggedSlot?.GetAnchorSlot();
            InventorySlot highlightedAnchor = manager.highlightedSlot?.GetAnchorSlot();
            bool canAttachOnWeapon = IsValidAttachmentWeaponPair(draggedAnchor, highlightedAnchor);

            if (canAttachOnWeapon)
            {
                (bool success, Attachment atc, int id) =
                    CowsinsUtilities.CompatibleAttachment(((Weapon_SO)highlightedAnchor.slotData.item).weaponObject, (AttachmentIdentifier_SO)draggedAnchor.slotData.item);
                return success;
            }

            return false;
        }

        private bool IsValidAttachmentWeaponPair(InventorySlot draggedAnchor, InventorySlot highlightedAnchor)
        {
            bool canAttachOnWeapon = draggedAnchor && highlightedAnchor && draggedAnchor.HasValidItem && draggedAnchor.slotData.item is AttachmentIdentifier_SO
                && highlightedAnchor.HasValidItem && highlightedAnchor.slotData.item is Weapon_SO;
            return canAttachOnWeapon;
        }
    }
}
