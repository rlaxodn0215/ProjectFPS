namespace cowsins.Inventory
{
    /// <summary>
    /// Each Slot in the Inventory, Hotbar or Chest requires an initialized SlotData.
    /// SlotData contains essential information about the Item a slot carries, its amount, attachments, bullets, and much more.
    /// </summary>
    [System.Serializable]
    public class SlotData
    {
        public Item_SO item; // This can be Item_SO, Weapon_SO, BulletsItem_SO, AttachmentIdentifier_SO, etc...
        public int amount;
        public bool isOriented = true; // If the Inventory is Tetris style, you can rotate the items in the Inventory by pressing "R". If facing the default direction, isOriented = true;
        public int rotationAngle = 0; // Stored rotation angle in degrees (0, 90, 180, 270)

        // Handle Attachment References for Weapons
        public AttachmentIdentifier_SO barrel,
              scope,
              stock,
              grip,
              magazine,
              flashlight,
              laser;
        // Handle Bullets for Weapons
        public int bulletsLeftInMagazine, totalBullets;

        // Handle Durability
        public float currentDurability;
        public bool durabilityInitialized;

        // Handle Contained Items (For Backpacks or other containers)
        public SlotData[] containedItems;

        // Constructors
        public SlotData(Item_SO item, int amount)
        {
            this.item = item;
            this.amount = amount;
            if (item != null && item.UseDurability)
            {
                this.currentDurability = item.MaxDurability;
                this.durabilityInitialized = true;
            }
        }

        // Default SlotData Constructor
        public SlotData()
        {
            item = null;
            amount = 0;
            currentDurability = 0;
            durabilityInitialized = false;
        }

        // Copy Constructor
        public SlotData(SlotData other)
        {
            if (other == null) return;
            this.item = other.item;
            this.amount = other.amount;
            this.isOriented = other.isOriented;
            this.rotationAngle = other.rotationAngle;
            this.bulletsLeftInMagazine = other.bulletsLeftInMagazine;
            this.totalBullets = other.totalBullets;
            this.currentDurability = other.currentDurability;
            this.durabilityInitialized = other.durabilityInitialized;
            this.barrel = other.barrel;
            this.scope = other.scope;
            this.stock = other.stock;
            this.grip = other.grip;
            this.magazine = other.magazine;
            this.flashlight = other.flashlight;
            this.laser = other.laser;

            if (other.containedItems != null)
            {
                this.containedItems = new SlotData[other.containedItems.Length];
                for (int i = 0; i < other.containedItems.Length; i++)
                {
                    this.containedItems[i] = other.containedItems[i]?.Clone();
                }
            }
        }

        // Complete SlotData Constructor
        public SlotData(Item_SO item, int amount, int bulletsLeftInMagazine, int totalBullets,
                        AttachmentIdentifier_SO barrel, AttachmentIdentifier_SO scope,
                        AttachmentIdentifier_SO stock, AttachmentIdentifier_SO grip,
                        AttachmentIdentifier_SO magazine, AttachmentIdentifier_SO flashlight,
                        AttachmentIdentifier_SO laser)
        {
            this.item = item;
            this.amount = amount;
            this.bulletsLeftInMagazine = bulletsLeftInMagazine;
            this.totalBullets = totalBullets;
            this.barrel = barrel;
            this.scope = scope;
            this.stock = stock;
            this.grip = grip;
            this.magazine = magazine;
            this.flashlight = flashlight;
            this.laser = laser;
            if (item != null && item.UseDurability)
            {
                this.currentDurability = item.MaxDurability;
                this.durabilityInitialized = true;
            }
        }

        public void EnsureDurabilityInitialized()
        {
            if (item != null && item.UseDurability && !durabilityInitialized)
            {
                if (currentDurability <= 0) currentDurability = item.MaxDurability;
                durabilityInitialized = true;
            }
        }

        public SlotData Clone()
        {
            SlotData clone = new SlotData
            {
                item = this.item,
                amount = this.amount,   
                isOriented = this.isOriented,
                rotationAngle = this.rotationAngle,
                bulletsLeftInMagazine = this.bulletsLeftInMagazine,
                totalBullets = this.totalBullets,
                currentDurability = this.currentDurability,
                durabilityInitialized = this.durabilityInitialized,
                barrel = this.barrel,
                scope = this.scope,
                stock = this.stock,
                grip = this.grip,
                magazine = this.magazine,
                flashlight = this.flashlight,
                laser = this.laser
            };

            if (this.containedItems != null)
            {
                clone.containedItems = new SlotData[this.containedItems.Length];
                for (int i = 0; i < this.containedItems.Length; i++)
                {
                    clone.containedItems[i] = this.containedItems[i]?.Clone();
                }
            }

            return clone;
        }
    }
}