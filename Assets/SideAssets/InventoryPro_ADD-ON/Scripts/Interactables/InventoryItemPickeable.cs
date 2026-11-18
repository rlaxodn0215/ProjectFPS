using UnityEngine;

namespace cowsins.Inventory
{
    public partial class InventoryItemPickeable : Pickeable
    {
        [SerializeField, Title("Inventory Item Pickeable"), SaveField, Tooltip("Item to Pick Up")] private Item_SO item;

        [SerializeField, Min(0), SaveField, Tooltip("Amount to add to the Inventory on Pick Up")] private int amount = 1;

        [SerializeField, Tooltip("If true, the Pick Up Toast Notification will display the amount to pick up (Ex: x4 Apple)")] private bool displayAmountInteractText;
        [SerializeField, HideInInspector] private SlotData[] containedItems;

        // GETTERS
        public Item_SO Item { get { return item; } }
        public int Amount { get { return amount; } }
        public SlotData[] ContainedItems => containedItems;
        public void SetContainedItems(SlotData[] items) => this.containedItems = items;

        private void Start() => GetVisuals();

        public override void Interact(Transform player)
        {
            if (InventoryProManager.instance)
            {
                (bool success, int remainingAmount) = InventoryProManager.instance.AddItem(item, amount, containedItems);
                if (success)
                {
                    alreadyInteracted = true;
                    interactableEvents.OnInteract?.Invoke();
                    amount = remainingAmount;
#if SAVE_LOAD_ADD_ON
                    StoreData();
#endif
                    ToastManager.Instance?.ShowToast($"{item._name} {ToastManager.Instance.CollectedMsg}");
                    if (amount <= 0) Destroy(this.gameObject);
                    // Save this Interaction in GameDataManager
                }
                else
                    ToastManager.Instance?.ShowToast(ToastManager.Instance.InventoryIsFullMsg);
#if SAVE_LOAD_ADD_ON
                StoreData();
#endif
                base.Interact(player);
                return;
            }
        }

        /// <summary>
        /// Updates the Item and its Visuals
        /// </summary>
        public void SetItem(Item_SO item, int amount)
        {
            this.item = item;
            this.amount = amount;

            GetVisuals();
        }

        /// <summary>
        /// Updates Visuals 
        /// </summary>
        public void GetVisuals()
        {
            // Weapon, Bullets or Attachments SOs cannot be used with InventoryItemPickeable, as they have their own Pickeable Components.
            if (this.item is Weapon_SO || this.item is BulletTypeIdentifier_SO || this.item is AttachmentIdentifier_SO)
            {
                Debug.LogError("<color=red>[COWSINS]</color> <b><color=yellow>Missing or Invalid Item_SO!</color></b> " +
                "Ensure that your Item_SO is correctly assigned and not null. " +
                "Avoid using <b><color=cyan>Weapon_SO, BulletsItem_SO, or AttachmentIdentifier_SO</color></b> directly. " +
                "Instead, use their corresponding prefabs: <b><color=green>Weapon Pickeable, Bullets Pickeable, and Attachment Pickeable</color></b> to resolve this issue." +
                "This InventoryItemPickeable will be destroyed now to avoid further issues.", this);
                Destroy(this.gameObject);
                return;
            }
            if (amount <= 0) amount = 1;

            interactText = item._name;
            image.sprite = item.icon;
            if (displayAmountInteractText) interactText = $"[{amount}] {interactText}";
            if (item.pickUpGraphics == null) return;
            Destroy(graphics.GetChild(0).gameObject);
            Instantiate(item.pickUpGraphics, transform.position, Quaternion.identity, graphics);
        }
        public override bool IsForbiddenInteraction(IWeaponReferenceProvider weaponController)
        {
            return false;
        }

#if SAVE_LOAD_ADD_ON
        // If interacted, simply destroy the object, as it has already been picked up.
        // Interacted State is called after loading
        public override void LoadedState()
        {
            if (alreadyInteracted) Destroy(this.gameObject);
        }
#endif
    }
}