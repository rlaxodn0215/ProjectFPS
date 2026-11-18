using cowsins.SaveLoad;
using UnityEngine;
using UnityEngine.UI;

namespace cowsins.Inventory
{
    /// <summary>
    /// Encapsulates all inventory grid generation logic (inventory, hotbar, generic grids and slot instantiation)
    /// </summary>
    public sealed class InventoryProGeneratorBehaviour
    {
        private readonly InventoryProManager inventoryManager;
        private readonly InventoryGridData_SO inventoryGridData;
        private readonly Transform hotbarParent;
        private readonly Transform inventoryParent;
        private readonly Transform chestParent;
        private readonly float gapX;
        private readonly float gapY;
        private readonly Padding hotbarPadding;

        private WeaponController weaponController;
        private int Rows => inventoryManager?._GridGenerator != null ? inventoryManager._GridGenerator.Rows : inventoryGridData.rows;
        private int Columns => inventoryManager?._GridGenerator != null ? inventoryManager._GridGenerator.Columns : inventoryGridData.columns;

        public InventoryProGeneratorBehaviour(
            InventoryProManager inventoryManager,
            InventoryGridData_SO inventoryGridData,
            Transform hotbarParent,
            Transform inventoryParent,
            Transform chestParent,
            float gapX,
            float gapY,
            Padding hotbarPadding)
        {
            this.inventoryManager = inventoryManager;
            this.inventoryGridData = inventoryGridData;
            this.hotbarParent = hotbarParent;
            this.inventoryParent = inventoryParent;
            this.chestParent = chestParent;
            this.gapX = gapX;
            this.gapY = gapY;
            this.hotbarPadding = hotbarPadding;
        }

        public void Initialize(WeaponController controller)
        {
            weaponController = controller;
        }

        public InventorySlot[,] GenerateInventory(int rows, int columns)
        {
            InventorySlot[,] inventorySlots = new InventorySlot[rows, columns];
            GenerateGrid(
                SlotType.Inventory,
                rows,
                columns,
                inventoryGridData.inventoryPadding,
                inventoryParent,
                inventorySlots,
                inventoryGridData.buttonPrefab);

            inventoryManager?.Events?.onInventoryGenerated?.Invoke();
            return inventorySlots;
        }

        public InventorySlot[,] ExpandInventory(InventorySlot[,] currentSlots,int currentRows,int currentCols,int newRows,int newCols)
        {
            if (currentSlots == null) return null;
            if (newRows < currentRows || newCols < currentCols) return currentSlots;
            if (newRows == currentRows && newCols == currentCols) return currentSlots;

            InventorySlot[,] newSlots = new InventorySlot[newRows, newCols];

            Padding padding = inventoryGridData.inventoryPadding;
            Vector3 offset = new Vector3(padding.horizontal, padding.vertical, 0);

            // Copy existing slots to keep current inventory items unchanged
            // And ensure their anchors are normalized to (0, 1) top-left so they never drift when containers resize
            for (int r = 0; r < currentRows; r++)
            {
                for (int c = 0; c < currentCols; c++)
                {
                    InventorySlot existingSlot = currentSlots[r, c];
                    if (existingSlot != null)
                    {
                        RectTransform slotRect = existingSlot.GetComponent<RectTransform>();
                        if (slotRect != null)
                        {
                            slotRect.anchorMin = new Vector2(0, 1);
                            slotRect.anchorMax = new Vector2(0, 1);
                            slotRect.pivot = new Vector2(0, 1);
                            slotRect.anchoredPosition = new Vector2(c * gapX + offset.x, -r * gapY - offset.y);
                        }
                    }
                    newSlots[r, c] = existingSlot;
                }
            }

            // Resize all containers
            ResizeInventoryContainers(newRows, newCols);

            // nstantiate new slots in the expanded region
            for (int r = 0; r < newRows; r++)
            {
                for (int c = 0; c < newCols; c++)
                {
                    if (r < currentRows && c < currentCols) continue;

                    InventorySlot slot = GenerateSlot(
                        SlotType.Inventory,
                        r,
                        c,
                        offset,
                        inventoryParent,
                        newSlots,
                        null,
                        inventoryGridData.buttonPrefab
                    );
                    slot.Initialize(r, c, new SlotData(), weaponController, inventoryManager);
                }
            }

            inventoryManager?.Events?.onInventoryGenerated?.Invoke();
            return newSlots;
        }

        public InventorySlot[] GenerateHotbar()
        {
            // Hotbar Slots are the same size as the Inventory Size from Weapon Controller
            InventorySlot[] hotbarSlots = new InventorySlot[weaponController.settings.inventorySize];

            Vector3 offset = new Vector3(hotbarPadding.horizontal, hotbarPadding.vertical, 0);

            for (int i = 0; i < weaponController.settings.inventorySize; i++)
            {
                InventorySlot slot = GenerateSlot(SlotType.Hotbar, 0, i, offset, hotbarParent, null, hotbarSlots, inventoryGridData.buttonPrefab);

                Weapon_SO weapon = i >= 0 && i < weaponController.settings.initialWeapons.Length
                    ? weaponController.settings.initialWeapons[i]
                    : null;

                SlotData slotData = weapon != null ? new SlotData(weapon, 1) : new SlotData();

                if (slotData?.item is Weapon_SO)
                {
                    InitializeInitialWeapon(slotData, weapon);
                }

                slot.Initialize(-1, i, slotData, weaponController, inventoryManager);
            }

            inventoryManager?.Events?.onHotbarGenerated?.Invoke();
            return hotbarSlots;
        }

        public void PopulateInventory(InventorySlot[,] inventorySlots, InvWeightCalculator weightCalculator)
        {
            if (inventorySlots == null) return;
            weightCalculator?.ResetWeight();

            int rows = inventorySlots.GetLength(0);
            int cols = inventorySlots.GetLength(1);

            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    InventorySlot slot = inventorySlots[row, col];
                    SlotData data = new SlotData();
                    int initialIndex = row * cols + col;
                    if (inventoryGridData.inventoryInitialData != null && inventoryGridData.inventoryInitialData.Length > initialIndex && inventoryGridData.inventoryInitialData[initialIndex] != null)
                    {
                        SlotData newData = inventoryGridData.inventoryInitialData[initialIndex];
                        data.item = newData.item;
                        data.amount = newData.amount;
                        data.bulletsLeftInMagazine = newData.bulletsLeftInMagazine;
                        data.totalBullets = newData.totalBullets;
                        data.barrel = newData.barrel;
                        data.scope = newData.scope;
                        data.stock = newData.stock;
                        data.grip = newData.grip;
                        data.magazine = newData.magazine;
                        data.flashlight = newData.flashlight;
                        data.laser = newData.laser;
                    }

                    InitializeSlot(inventorySlots, data, row, col, slot, setDefaultAttachments: true);
                    weightCalculator?.UpdateCurrentWeight(data.item, data.amount);
                }
            }
        }

        public void PopulateInventoryWithSerializableInventoryData(
            InventorySlot[,] inventorySlots,
            GameData.SerializableInventoryData[] loadedData,
            InvWeightCalculator weightCalculator)
        {
            if (inventorySlots == null) return;
            weightCalculator?.ResetWeight();

            int rows = inventorySlots.GetLength(0);
            int cols = inventorySlots.GetLength(1);

            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    InventorySlot slot = inventorySlots[row, col];
                    int linearIndex = row * cols + col;
                    if (loadedData == null || linearIndex >= loadedData.Length || loadedData[linearIndex] == null)
                    {
                        InitializeSlot(inventorySlots, new SlotData(), row, col, slot, setDefaultAttachments: false);
                        continue;
                    }

                    var loadedDataEntry = loadedData[linearIndex];

                    SlotData data = DeserializeSlotData(loadedDataEntry);
                    InitializeSlot(inventorySlots, data, row, col, slot, setDefaultAttachments: false);
                    weightCalculator?.UpdateCurrentWeight(data.item, data.amount);
                }
            }
        }

        public static SlotData DeserializeSlotData(GameData.SerializableInventoryData loadedDataEntry)
        {
            if (loadedDataEntry == null || string.IsNullOrEmpty(loadedDataEntry.SOPath))
                return new SlotData();

            Item_SO item = ItemRegistry.GetItemByName(loadedDataEntry.SOPath);
            if (item == null)
            {
                Debug.LogWarning($"<color=yellow>[COWSINS]</color> Item '<b>{loadedDataEntry.SOPath}</b>' could not be loaded because it was not found in ItemRegistry. Please ensure the item exists in the project and the ItemRegistry is updated.");
            }
            SlotData data = new SlotData
            {
                amount = loadedDataEntry.amount,
                bulletsLeftInMagazine = loadedDataEntry.bulletsLeftInMagazine,
                totalBullets = loadedDataEntry.totalBullets,
                item = item,
                isOriented = loadedDataEntry.isOriented,
                rotationAngle = loadedDataEntry.rotationAngle != 0 || loadedDataEntry.isOriented ? loadedDataEntry.rotationAngle : 90,
                currentDurability = loadedDataEntry.currentDurability,
                durabilityInitialized = loadedDataEntry.currentDurability > 0,
                barrel = ItemRegistry.GetItemByName(loadedDataEntry.barrel) as AttachmentIdentifier_SO,
                scope = ItemRegistry.GetItemByName(loadedDataEntry.scope) as AttachmentIdentifier_SO,
                stock = ItemRegistry.GetItemByName(loadedDataEntry.stock) as AttachmentIdentifier_SO,
                grip = ItemRegistry.GetItemByName(loadedDataEntry.grip) as AttachmentIdentifier_SO,
                magazine = ItemRegistry.GetItemByName(loadedDataEntry.magazine) as AttachmentIdentifier_SO,
                flashlight = ItemRegistry.GetItemByName(loadedDataEntry.flashlight) as AttachmentIdentifier_SO,
                laser = ItemRegistry.GetItemByName(loadedDataEntry.laser) as AttachmentIdentifier_SO,
            };

            data.EnsureDurabilityInitialized();

            if (loadedDataEntry.containedItems != null && loadedDataEntry.containedItems.Length > 0)
                data.containedItems = DeserializeContainedItems(loadedDataEntry.containedItems);
            else data.containedItems = data.item?.CreateDefaultContainedItems();

            return data;
        }

        public static SlotData[] DeserializeContainedItems(GameData.SerializableInventoryData[] loadedEntries)
        {
            if (loadedEntries == null) return null;
            SlotData[] result = new SlotData[loadedEntries.Length];
            for (int i = 0; i < loadedEntries.Length; i++)
            {
                result[i] = DeserializeSlotData(loadedEntries[i]);
            }
            return result;
        }

        public void UpdateHotbarSlotsVisuals(InventorySlot[] hotbarSlots)
        {
            for (int i = 0; i < weaponController.settings.inventorySize; i++)
            {
                var currentWeapon = weaponController.Inventory[i];
                if (currentWeapon == null) continue;

                float existingDurability = (hotbarSlots[i].slotData != null && hotbarSlots[i].slotData.item == currentWeapon.weapon)
                    ? hotbarSlots[i].slotData.currentDurability : 0f;
                bool existingInit = (hotbarSlots[i].slotData != null && hotbarSlots[i].slotData.item == currentWeapon.weapon) && hotbarSlots[i].slotData.durabilityInitialized;

                hotbarSlots[i].slotData = new SlotData
                {
                    item = currentWeapon.weapon,
                    amount = 1,
                    bulletsLeftInMagazine = currentWeapon.bulletsLeftInMagazine,
                    totalBullets = currentWeapon.totalBullets,
                    currentDurability = existingDurability,
                    durabilityInitialized = existingInit
                };
                hotbarSlots[i].slotData.EnsureDurabilityInitialized();

                if (inventoryManager?.DataModel != null)
                    inventoryManager.DataModel.NotifySlotChanged(hotbarSlots[i]);
                else
                    hotbarSlots[i].UpdateSlotGraphics();
            }
        }

        public Transform ChestParent => chestParent;

        public void ResizeInventoryContainers(int rows, int cols)
        {
            Padding padding = inventoryGridData.inventoryPadding;
            float totalWidth = cols * gapX + (padding.horizontal * 2);
            float totalHeight = rows * gapY + (padding.vertical * 2);

            // Resize inventoryParent
            RectTransform inventoryRect = inventoryParent != null ? inventoryParent.GetComponent<RectTransform>() : null;
            if (inventoryRect != null)
            {
                inventoryRect.anchorMin = new Vector2(0, 1);
                inventoryRect.anchorMax = new Vector2(0, 1);
                inventoryRect.pivot = new Vector2(0, 1);
                inventoryRect.anchoredPosition = Vector2.zero;
                inventoryRect.sizeDelta = new Vector2(totalWidth, totalHeight);
            }

            // Resize hotbarParent width so it matches the grid width
            RectTransform hotbarRect = hotbarParent != null ? hotbarParent.GetComponent<RectTransform>() : null;
            float hotbarH = 125f;
            if (hotbarRect != null)
            {
                hotbarRect.anchorMin = new Vector2(0, 0);
                hotbarRect.anchorMax = new Vector2(0, 0);
                hotbarRect.pivot = new Vector2(0, 0);
                hotbarRect.anchoredPosition = Vector2.zero;
                if (hotbarRect.sizeDelta.y > 0) hotbarH = hotbarRect.sizeDelta.y;
                hotbarRect.sizeDelta = new Vector2(totalWidth, hotbarH);
            }

            // Resize main inventory window panel
            RectTransform panelRect = inventoryParent != null && inventoryParent.parent != null
                ? inventoryParent.parent.GetComponent<RectTransform>()
                : null;

            if (panelRect != null)
            {
                const float betweenContainersGap = 13.229f;
                float panelHeight = totalHeight + hotbarH + betweenContainersGap;

                panelRect.sizeDelta = new Vector2(totalWidth, panelHeight);
                LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);
            }
        }

        /// <summary>
        /// Generates a grid of InventorySlots (inventory or chest).
        /// </summary>
        public void GenerateGrid(SlotType slotType, int rows, int cols, Padding padding, Transform instantiationParent, InventorySlot[,] slotsArray, GameObject buttonPrefab)
        {
            Vector3 offset = new Vector3(padding.horizontal, padding.vertical, 0);

            if (slotType == SlotType.Inventory) ResizeInventoryContainers(rows, cols);
            else
            {
                float totalWidth = cols * gapX + (padding.horizontal * 2);
                float totalHeight = rows * gapY + (padding.vertical * 2);

                RectTransform parentRect = instantiationParent != null ? instantiationParent.GetComponent<RectTransform>() : null;
                if (parentRect != null)
                    parentRect.sizeDelta = new Vector2(totalWidth, totalHeight);
            }

            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    GenerateSlot(slotType, row, col, offset, instantiationParent, slotsArray, null, buttonPrefab);
                }
            }
        }

        public void ApplyDefaultWeaponSlotData(SlotData data, Weapon_SO weapon)
        {
            if (data == null || weapon == null) return;
            InitializeInitialWeapon(data, weapon);
        }

        private void InitializeSlot(InventorySlot[,] inventorySlots, SlotData data, int row, int col, InventorySlot slot, bool setDefaultAttachments)
        {
            if (setDefaultAttachments && data.item is Weapon_SO weapon)
            {
                ApplyDefaultWeaponSlotData(data, weapon);
            }

            SlotData slotData = inventorySlots[row, col].anchorSlot == null ? data : new SlotData();
            inventorySlots[row, col].Initialize(row, col, slotData, weaponController, inventoryManager);

            if (inventoryManager._InventoryStyle != InventoryProManager.InventoryStyle.Tetris && slotData?.item != null)
            {
                slot.anchorSlot = slot;
            }
        }

        private InventorySlot GenerateSlot(
            SlotType slotType,
            int row,
            int col,
            Vector3 offset,
            Transform instantiationParent,
            InventorySlot[,] slotsArray,
            InventorySlot[] hotbarSlots,
            GameObject buttonPrefab)
        {
            GameObject button = Object.Instantiate(buttonPrefab, instantiationParent);
            RectTransform rectTransform = button.GetComponent<RectTransform>();
            bool isHotbar = slotType == SlotType.Hotbar;

            if (isHotbar)
            {
                rectTransform.anchorMin = new Vector2(0, 0);
                rectTransform.anchorMax = new Vector2(0, 0);
                rectTransform.pivot = new Vector2(0, 0);
                rectTransform.anchoredPosition = new Vector2(col * gapX + offset.x, 10f);
            }
            else
            {
                rectTransform.anchorMin = new Vector2(0, 1);
                rectTransform.anchorMax = new Vector2(0, 1);
                rectTransform.pivot = new Vector2(0, 1);
                rectTransform.anchoredPosition = new Vector2(col * gapX + offset.x, -row * gapY - offset.y);
            }

            button.name = $"Button[{row},{col}]";

            InventorySlot slot = button.GetComponent<InventorySlot>();
            if (isHotbar)
            {
                hotbarSlots[col] = slot;
                slot.slotType = SlotType.Hotbar;
                slot.SetAcceptedType<Weapon_SO>();
            }
            else
            {
                slotsArray[row, col] = slot;
                slot.slotType = slotType;
                slot.SetAcceptedType<Item_SO>();
            }

            return slot;
        }

        // Used for Hotbar Slots, handles attachments & bullets for Initial Weapons in the Hotbar Inventory.
        private void InitializeInitialWeapon(SlotData data, Weapon_SO weapon)
        {
            if (weapon == null) return;

            WeaponIdentification wId = weapon.weaponObject;
            // If weapon object or its attachment state is missing, avoid accessing it
            if (wId == null || wId.AttachmentState == null)
            {
                data.bulletsLeftInMagazine = weapon.magazineSize;
                data.totalBullets = weapon.limitedMagazines ? weapon.magazineSize * weapon.totalMagazines : weapon.magazineSize;
                return;
            }

            data.barrel = wId.AttachmentState.GetDefault(AttachmentType.Barrel)?.attachmentIdentifier;
            data.scope = wId.AttachmentState.GetDefault(AttachmentType.Scope)?.attachmentIdentifier;
            data.stock = wId.AttachmentState.GetDefault(AttachmentType.Stock)?.attachmentIdentifier;
            data.grip = wId.AttachmentState.GetDefault(AttachmentType.Grip)?.attachmentIdentifier;
            data.magazine = wId.AttachmentState.GetDefault(AttachmentType.Magazine)?.attachmentIdentifier;
            data.flashlight = wId.AttachmentState.GetDefault(AttachmentType.Flashlight)?.attachmentIdentifier;
            data.laser = wId.AttachmentState.GetDefault(AttachmentType.Laser)?.attachmentIdentifier;

            data.bulletsLeftInMagazine = weapon.magazineSize;
            data.totalBullets = weapon.limitedMagazines ? weapon.magazineSize * weapon.totalMagazines : weapon.magazineSize;
        }
    }
}

