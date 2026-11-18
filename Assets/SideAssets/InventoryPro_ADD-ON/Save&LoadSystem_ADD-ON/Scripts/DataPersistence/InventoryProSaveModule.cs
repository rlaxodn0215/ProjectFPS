using System.Collections.Generic;
using UnityEngine;

#if INVENTORY_PRO_ADD_ON
using cowsins.Inventory;
#endif

namespace cowsins.SaveLoad
{
    public class InventoryProSaveModule : ISaveModule
    {
        public string ModuleId => "InventoryPro";

        public void SaveData(GameData gameData, DataPersistence_SO settings)
        {
#if INVENTORY_PRO_ADD_ON
            if (settings == null || !settings.SaveInventory || gameData == null) return;
            if (InventoryProManager.instance == null) return;

            var invData = gameData.inventory ?? (gameData.inventory = new GameData.InventorySaveData());

            // If a backpack is currently open, ensure its contained items are synced back to the slotData
            if (InventoryProManager.instance._GridGenerator?.BackpackBehaviour != null && InventoryProManager.instance._GridGenerator.BackpackBehaviour.IsBackpackOpen) InventoryProManager.instance._GridGenerator.BackpackBehaviour.SyncActiveBackpackData();

            // Store size parameters
            invData.rows = InventoryProManager.instance._GridGenerator.Rows;
            invData.cols = InventoryProManager.instance._GridGenerator.Columns;
            invData.serializableInventoryData = new GameData.SerializableInventoryData[invData.rows * invData.cols];

            // Store the SlotData for each slot in the Inventory Grid
            for (int i = 0; i < invData.rows * invData.cols; i++)
            {
                int row = i / invData.cols;
                int col = i % invData.cols;

                SlotData slotData = InventoryProManager.instance.InventorySlots[row, col].slotData;
                invData.serializableInventoryData[i] = SerializeSlotData(slotData);
            }

            // Save Custom Slots
            InventoryProManager.instance.EnsureCustomSlotsRegistered();
            var customSlots = InventoryProManager.instance.CustomSlots;
            if (customSlots != null && customSlots.Count > 0)
            {
                invData.serializableCustomSlotData = new GameData.SerializableCustomSlotData[customSlots.Count];
                for (int i = 0; i < customSlots.Count; i++)
                {
                    var customSlot = customSlots[i];
                    invData.serializableCustomSlotData[i] = new GameData.SerializableCustomSlotData(
                        customSlot != null ? customSlot.CustomSlotId : string.Empty,
                        i,
                        customSlot != null && customSlot.slotData != null
                            ? SerializeSlotData(customSlot.slotData)
                            : new GameData.SerializableInventoryData()
                    );
                }
            }
            else
            {
                invData.serializableCustomSlotData = new GameData.SerializableCustomSlotData[0];
            }

            // Save Favorites Radial Menu
            if (!InventoryProManager.instance.UseFavouritesRadialMenu || InventoryProManager.instance._FavItemsMenu == null) return;

            int favSize = InventoryProManager.instance._FavItemsMenu.RadialMenuSize;
            if (favSize <= 0) return;

            invData.favItems = new GameData.SerializableFavItemReference[favSize];
            for (int i = 0; i < favSize; i++)
            {
                FavItemReference favItemReference = InventoryProManager.instance._FavItemsMenu.favoritesList[i];
                invData.favItems[i] = new GameData.SerializableFavItemReference
                {
                    row = favItemReference.row,
                    col = favItemReference.col,
                    itemName = favItemReference.item != null ? favItemReference.item.name : string.Empty,
                    isInventorySlot = favItemReference.isInventorySlot
                };
            }
#endif
        }

        public void LoadData(GameData gameData, DataPersistence_SO settings)
        {
#if INVENTORY_PRO_ADD_ON
            if (settings == null || !settings.SaveInventory || gameData == null || gameData.inventory == null) return;
            if (InventoryProManager.instance == null) return;

            var invData = gameData.inventory;

            if (invData.serializableInventoryData != null && invData.serializableInventoryData.Length > 0)
            {
                int savedRows = invData.rows > 0 ? invData.rows : InventoryProManager.instance._GridGenerator.Rows;
                int savedCols = invData.cols > 0 ? invData.cols : InventoryProManager.instance._GridGenerator.Columns;

                InventoryProManager.instance._GridGenerator.GenerateInventory(savedRows, savedCols);
                InventoryProManager.instance._GridGenerator.GenerateHotbar();
                InventoryProManager.instance._GridGenerator.PopulateInventoryWithSerializableInventoryData(invData.serializableInventoryData);
            }
            else
            {
                InventoryProManager.instance._GridGenerator.GenerateInventory();
                InventoryProManager.instance._GridGenerator.PopulateInventory();
                InventoryProManager.instance._GridGenerator.GenerateHotbar();
            }

            InventoryProManager.instance.CalculateTotalBullets(overrideCurrentBullets: true);

            // Custom Slots
            LoadCustomSlotsData(invData);

            // Favorites Radial Menu
            if (!InventoryProManager.instance.UseFavouritesRadialMenu || invData.favItems == null || invData.favItems.Length <= 0) return;

            int favSize = invData.favItems.Length;
            FavItemsMenu favItemsMenu = InventoryProManager.instance._FavItemsMenu;
            if (favItemsMenu == null) return;

            favItemsMenu.favoritesList = new List<FavItemReference>(favSize);

#pragma warning disable CS0618
            var playerMovement = UnityEngine.Object.FindObjectOfType<PlayerMovement>();
#pragma warning restore CS0618
            var weaponController = playerMovement?.GetComponent<WeaponController>();

            for (int i = 0; i < favSize; i++)
            {
                var favItem = invData.favItems[i];
                Item_SO itemSO = null;

                if (favItem != null && !string.IsNullOrEmpty(favItem.itemName))
                {
                    itemSO = ItemRegistry.GetItemByName(favItem.itemName);
                }

                var favItemReference = new FavItemReference(
                    favItem != null ? favItem.row : 0,
                    favItem != null ? favItem.col : 0,
                    itemSO,
                    favItem != null && favItem.isInventorySlot
                );

                if (i < favItemsMenu.favoritesList.Count)
                    favItemsMenu.favoritesList[i] = favItemReference;
                else
                    favItemsMenu.favoritesList.Add(favItemReference);

                if (favItemsMenu.InventorySlots != null && i < favItemsMenu.InventorySlots.Length && favItemsMenu.InventorySlots[i] != null)
                {
                    favItemsMenu.InventorySlots[i].Initialize(i, favItemReference, favItemsMenu, InventoryProManager.instance, weaponController);
                }
            }
#endif
        }

#if INVENTORY_PRO_ADD_ON
        private void LoadCustomSlotsData(GameData.InventorySaveData invData)
        {
            if (InventoryProManager.instance == null || invData == null) return;

            InventoryProManager.instance.EnsureCustomSlotsRegistered();
            var customSlots = InventoryProManager.instance.CustomSlots;
            if (customSlots == null || customSlots.Count == 0) return;
            if (invData.serializableCustomSlotData == null || invData.serializableCustomSlotData.Length == 0) return;

            var matchedSlots = new HashSet<InventorySlot>();

            for (int e = 0; e < invData.serializableCustomSlotData.Length; e++)
            {
                var entry = invData.serializableCustomSlotData[e];
                if (entry == null) continue;

                InventorySlot targetSlot = null;

                // Match by customSlotId
                if (!string.IsNullOrEmpty(entry.customSlotId))
                {
                    for (int i = 0; i < customSlots.Count; i++)
                    {
                        var slot = customSlots[i];
                        if (slot != null && !matchedSlots.Contains(slot) && (slot.CustomSlotId == entry.customSlotId || slot.gameObject.name == entry.customSlotId))
                        {
                            targetSlot = slot;
                            break;
                        }
                    }
                }

                // Match by slotIndex
                if (targetSlot == null && entry.slotIndex >= 0 && entry.slotIndex < customSlots.Count)
                {
                    var slot = customSlots[entry.slotIndex];
                    if (slot != null && !matchedSlots.Contains(slot))
                    {
                        targetSlot = slot;
                    }
                }

                // Fallback to first unmatched slot
                if (targetSlot == null)
                {
                    for (int i = 0; i < customSlots.Count; i++)
                    {
                        var slot = customSlots[i];
                        if (slot != null && !matchedSlots.Contains(slot))
                        {
                            targetSlot = slot;
                            break;
                        }
                    }
                }

                if (targetSlot == null) continue;

                matchedSlots.Add(targetSlot);

                if (entry.slotData != null && !string.IsNullOrEmpty(entry.slotData.SOPath))
                {
                    targetSlot.slotData = InventoryProGeneratorBehaviour.DeserializeSlotData(entry.slotData);
                }
                else
                {
                    targetSlot.slotData = new SlotData();
                }

                targetSlot.UpdateSlotGraphics();

                if (InventoryProManager.instance.DataModel != null)
                {
                    InventoryProManager.instance.DataModel.NotifySlotChanged(targetSlot);
                }
            }
        }

        public static GameData.SerializableInventoryData SerializeSlotData(SlotData slotData)
        {
            var data = new GameData.SerializableInventoryData();
            if (slotData != null && slotData.item != null && slotData.amount > 0)
            {
                data.SOPath = slotData.item.name;
                data.amount = slotData.amount;
                data.isOriented = slotData.isOriented;
                data.rotationAngle = slotData.rotationAngle;
                data.bulletsLeftInMagazine = slotData.bulletsLeftInMagazine;
                data.totalBullets = slotData.totalBullets;
                data.currentDurability = slotData.currentDurability;

                data.barrel = slotData.barrel != null ? slotData.barrel.name : string.Empty;
                data.scope = slotData.scope != null ? slotData.scope.name : string.Empty;
                data.stock = slotData.stock != null ? slotData.stock.name : string.Empty;
                data.grip = slotData.grip != null ? slotData.grip.name : string.Empty;
                data.magazine = slotData.magazine != null ? slotData.magazine.name : string.Empty;
                data.flashlight = slotData.flashlight != null ? slotData.flashlight.name : string.Empty;
                data.laser = slotData.laser != null ? slotData.laser.name : string.Empty;

                if (slotData.containedItems != null && slotData.containedItems.Length > 0)
                {
                    data.containedItems = SerializeContainedItems(slotData.containedItems);
                }
            }
            else
            {
                data.SOPath = string.Empty;
                data.amount = 0;
            }
            return data;
        }

        public static GameData.SerializableInventoryData[] SerializeContainedItems(SlotData[] slots)
        {
            if (slots == null) return null;
            var result = new GameData.SerializableInventoryData[slots.Length];
            for (int i = 0; i < slots.Length; i++)
            {
                result[i] = SerializeSlotData(slots[i]);
            }
            return result;
        }
#endif
    }
}
