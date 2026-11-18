using UnityEngine;

namespace cowsins.Inventory
{
    public partial class Chest : Interactable
    {
        [SerializeField, Tooltip("Initial Content of this Chest"), Title("Chest")] private InventoryGridData_SO initialInventoryGridData;

        // Tracks the current content of the chest
        private SlotData[] currentChestData;
        // Size = rows * columns
        public Vector2Int ChestSize { get { return new Vector2Int(initialInventoryGridData.rows, initialInventoryGridData.columns); } }
        public InventoryGridData_SO InitialInventoryGridData => initialInventoryGridData;

        // Initially populate the content of the chest with initial inventory content.
        // This will be overrided by the Save & Load system if required.
        private void Start()
        {
            if (initialInventoryGridData == null) return;

            currentChestData = new SlotData[initialInventoryGridData.rows * initialInventoryGridData.columns];
            if (initialInventoryGridData.inventoryInitialData == null) return;

            for (int i = 0; i < initialInventoryGridData.rows; i++)
            {
                for (int j = 0; j < initialInventoryGridData.columns; j++)
                {
                    int adjustedLinearIndex = i * initialInventoryGridData.columns + j;
                    if (adjustedLinearIndex < 0 || adjustedLinearIndex >= initialInventoryGridData.inventoryInitialData.Length) continue;

                    currentChestData[adjustedLinearIndex] = initialInventoryGridData.inventoryInitialData[adjustedLinearIndex]?.Clone();
                    if (currentChestData[adjustedLinearIndex]?.item != null && currentChestData[adjustedLinearIndex].item is Weapon_SO)
                    {
                        Weapon_SO weapon = (Weapon_SO)currentChestData[adjustedLinearIndex].item;
                        if (weapon == null) continue;

                        currentChestData[adjustedLinearIndex].bulletsLeftInMagazine = weapon.magazineSize;
                        currentChestData[adjustedLinearIndex].totalBullets = weapon.limitedMagazines ? weapon.magazineSize * weapon.totalMagazines : weapon.magazineSize;

                        if (weapon.weaponObject != null && weapon.weaponObject.AttachmentState != null)
                        {
                            currentChestData[adjustedLinearIndex].barrel = weapon.weaponObject.AttachmentState.GetCurrent(AttachmentType.Barrel)?.attachmentIdentifier;
                            currentChestData[adjustedLinearIndex].scope = weapon.weaponObject.AttachmentState.GetCurrent(AttachmentType.Scope)?.attachmentIdentifier;
                            currentChestData[adjustedLinearIndex].stock = weapon.weaponObject.AttachmentState.GetCurrent(AttachmentType.Stock)?.attachmentIdentifier;
                            currentChestData[adjustedLinearIndex].grip = weapon.weaponObject.AttachmentState.GetCurrent(AttachmentType.Grip)?.attachmentIdentifier;
                            currentChestData[adjustedLinearIndex].magazine = weapon.weaponObject.AttachmentState.GetCurrent(AttachmentType.Magazine)?.attachmentIdentifier;
                            currentChestData[adjustedLinearIndex].flashlight = weapon.weaponObject.AttachmentState.GetCurrent(AttachmentType.Flashlight)?.attachmentIdentifier;
                            currentChestData[adjustedLinearIndex].laser = weapon.weaponObject.AttachmentState.GetCurrent(AttachmentType.Laser)?.attachmentIdentifier;
                        }
                    }
                }
            }
        }

        public override void Interact(Transform player)
        {
            if (InventoryProManager.instance == null) return;

            // Opening the chest means generating a grid exclusively for chests apart from the Inventory�s Grid, given the parameters of this specific chest.
            InventoryProManager.instance._GridGenerator.ChestBehaviour.OpenChest(this, currentChestData, initialInventoryGridData.rows, initialInventoryGridData.columns);
            // To visualize the Chest UI we need to Open the Inventory, as they work together
            InventoryProManager.instance.UIController.OpenInventory();

            interactableEvents.OnInteract?.Invoke();
            // Make sure we cannot move, shoot, or perform any action while the chest is opened.
            player.GetComponent<PlayerControl>().LoseControl();
        }

        public void SetChestData(SlotData[] slotData) => this.currentChestData = slotData;
    }
}