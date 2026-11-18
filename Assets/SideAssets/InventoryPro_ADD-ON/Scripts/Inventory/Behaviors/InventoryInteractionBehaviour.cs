using UnityEngine;

namespace cowsins.Inventory
{
    /// <summary>
    /// Handles interaction logic for picking up items and updating hotbar slots
    /// </summary>
    public sealed class InventoryInteractionBehaviour
    {
        private readonly InventoryProManager manager;
        private readonly WeaponController weaponController;
        private readonly InteractManager interactManager;

        public InventoryInteractionBehaviour(InventoryProManager manager, WeaponController weaponController, InteractManager interactManager)
        {
            this.manager = manager;
            this.weaponController = weaponController;
            this.interactManager = interactManager;
        }

        /// <summary>
        /// This method is called when the Player interacts with an Interactable.
        /// </summary>
        public void OnFinishedInteraction()
        {
            if (interactManager.HighlightedInteractable is WeaponPickeable)  // Handle Interaction with WeaponPickeable
            {
                for (int i = 0; i < weaponController.settings.inventorySize; i++)
                {
                    if (weaponController.Inventory[i])
                    {
                        WeaponPickeable weaponPickeable = (WeaponPickeable)interactManager.HighlightedInteractable;
                        manager._GridGenerator.UpdateHotbarSlot(i, weaponPickeable.GetAttachmentByType(AttachmentType.Barrel),
                            weaponPickeable.GetAttachmentByType(AttachmentType.Scope),
                            weaponPickeable.GetAttachmentByType(AttachmentType.Stock),
                            weaponPickeable.GetAttachmentByType(AttachmentType.Grip),
                            weaponPickeable.GetAttachmentByType(AttachmentType.Magazine),
                            weaponPickeable.GetAttachmentByType(AttachmentType.Flashlight),
                            weaponPickeable.GetAttachmentByType(AttachmentType.Laser));
                    }
                }
                manager.CalculateTotalBullets(overrideCurrentBullets: true);
            }
            else if (interactManager.HighlightedInteractable is AttachmentPickeable)// Handle Interaction with AttachmentPickeable
            {
                InventorySlot currentInventorySlot = manager.HotbarSlots[weaponController.CurrentWeaponIndex];
                AttachmentPickeable attachmentPickeable = interactManager.HighlightedInteractable as AttachmentPickeable;
                if (attachmentPickeable.CheckCompatibleAttachment(weaponController))
                {
                    switch (attachmentPickeable.Atc)
                    {
                        case Scope scope: currentInventorySlot.slotData.scope = attachmentPickeable.attachmentIdentifier; break;
                        case Barrel barrel: currentInventorySlot.slotData.barrel = attachmentPickeable.attachmentIdentifier; break;
                        case Stock stock: currentInventorySlot.slotData.stock = attachmentPickeable.attachmentIdentifier; break;
                        case Grip grip: currentInventorySlot.slotData.grip = attachmentPickeable.attachmentIdentifier; break;
                        case Magazine magazine: currentInventorySlot.slotData.magazine = attachmentPickeable.attachmentIdentifier; break;
                        case Flashlight flashlight: currentInventorySlot.slotData.flashlight = attachmentPickeable.attachmentIdentifier; break;
                        case Laser laser: currentInventorySlot.slotData.laser = attachmentPickeable.attachmentIdentifier; break;
                    }
                }
            }
            else if (interactManager.HighlightedInteractable is BulletsPickeable) // Handle Interaction with BulletsPickeable
            {
                manager.CalculateTotalBullets(overrideCurrentBullets: true);
            }
        }
    }
}
