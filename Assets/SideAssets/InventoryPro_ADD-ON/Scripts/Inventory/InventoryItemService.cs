using System.Collections.Generic;
using UnityEngine;

namespace cowsins.Inventory
{
    /// <summary>
    /// Handles item mutations, addition, removal, dragging, stacking, swapping, sorting, and world dropping.
    /// </summary>
    public class InventoryItemService : IInventoryItemService
    {
        private readonly InventoryProManager inventoryManager;
        private readonly InventoryGridData_SO inventoryGridData;
        private readonly InvWeightCalculator weightCalculator;
        private readonly WeaponController weaponController;
        private readonly InventoryDataModel dataModel;
        private readonly IInventoryGridService gridService;
        private readonly IInventoryFeedbackService feedbackService;

        private int Rows => dataModel.Rows;
        private int Columns => dataModel.Columns;

        public InventoryItemService(InventoryProManager inventoryManager,InventoryGridData_SO inventoryGridData,InvWeightCalculator weightCalculator,WeaponController weaponController,InventoryDataModel dataModel,
            IInventoryGridService gridService,IInventoryFeedbackService feedbackService = null)
        {
            this.inventoryManager = inventoryManager;
            this.inventoryGridData = inventoryGridData;
            this.weightCalculator = weightCalculator;
            this.weaponController = weaponController;
            this.dataModel = dataModel;
            this.gridService = gridService;
            this.feedbackService = feedbackService ?? new InventoryFeedbackService(inventoryManager);
        }

        public (bool success, int remainingAmount) AddItemToInventory(Item_SO item_SO, int amount, SlotData[] containedItems = null)
        {
            InventorySlot[,] inventorySlots = dataModel.InventorySlots;
            if (item_SO == null) return (false, amount);

            if (amount <= 0) amount = 1;
            int availableAmount = amount;

            if (item_SO.maxStack > 1)
            {
                for (int i = 0; i < Columns * Rows; i++)
                {
                    (int iRow, int iCol) = gridService.GetRowAndColumnOutOfIndex(i);
                    InventorySlot inventorySlot = inventorySlots[iRow, iCol];

                    if (inventorySlot.HasValidItem && inventorySlot.slotData.item == item_SO)
                    {
                        int amountInSlot = inventorySlot.slotData.amount;
                        int maxAmountPerSlot = item_SO.maxStack;

                        if (amountInSlot < maxAmountPerSlot)
                        {
                            int availableSpace = maxAmountPerSlot - amountInSlot;
                            int amountToAdd = Mathf.Min(availableAmount, availableSpace);

                            inventorySlot.slotData.amount += amountToAdd;
                            availableAmount -= amountToAdd;

                            weightCalculator?.UpdateCurrentWeight(item_SO, amountToAdd);
                            dataModel.NotifySlotAreaChanged(inventorySlot.GetAnchorSlot());

                            if (availableAmount <= 0)
                                return (true, 0);
                        }
                    }
                }
            }

            for (int i = 0; i < Rows; i++)
            {
                for (int j = 0; j < Columns; j++)
                {
                    InventorySlot inventorySlot = inventorySlots[i, j];
                    Vector2Int size = inventoryManager._InventoryStyle == InventoryProManager.InventoryStyle.Grid
                        ? new Vector2Int(1, 1)
                        : item_SO.itemSize;

                    if (inventorySlot.slotData.item == null &&
                        inventorySlot.GetAnchorSlot()?.slotData?.item == null &&
                        gridService.ItemFits(inventorySlot, size, inventorySlot.row, inventorySlot.col, inventorySlot.IsInventorySlot))
                    {
                        int amountToAdd = Mathf.Min(availableAmount, item_SO.maxStack);

                        inventorySlot.slotData = new SlotData
                        {
                            item = item_SO,
                            amount = amountToAdd
                        };

                        if (containedItems != null && containedItems.Length > 0)
                            inventorySlot.slotData.containedItems = containedItems;
                        else
                            inventorySlot.slotData.containedItems = item_SO.CreateDefaultContainedItems();

                        if (item_SO is Weapon_SO weapon_SO)
                        {
                            inventorySlot.slotData.bulletsLeftInMagazine = weapon_SO.magazineSize;
                            inventorySlot.slotData.totalBullets = weapon_SO.magazineSize;
                        }

                        for (int u = 0; u < size.y; u++)
                        {
                            for (int v = 0; v < size.x; v++)
                            {
                                inventorySlots[inventorySlot.row + u, inventorySlot.col + v].anchorSlot = inventorySlot;
                            }
                        }

                        weightCalculator?.UpdateCurrentWeight(item_SO, amountToAdd);
                        dataModel.NotifySlotAreaChanged(inventorySlot);
                        availableAmount -= amountToAdd;

                        if (availableAmount <= 0)
                            return (true, 0);
                    }
                }
            }

            return (availableAmount < amount, availableAmount);
        }

        public bool AddWeaponToInventory(Weapon_SO weapon_SO, int bulletsLeftInmagazine, int totalBullets, WeaponPickeable weaponPickeable = null)
        {
            InventorySlot[,] inventorySlots = dataModel.InventorySlots;
            if (weapon_SO == null) return false;

            for (int i = 0; i < Rows; i++)
            {
                for (int j = 0; j < Columns; j++)
                {
                    InventorySlot inventorySlot = inventorySlots[i, j];
                    Vector2Int size = inventoryManager._InventoryStyle == InventoryProManager.InventoryStyle.Grid
                        ? new Vector2Int(1, 1)
                        : weapon_SO.itemSize;

                    if (inventorySlot.slotData.item == null &&
                        inventorySlot.GetAnchorSlot()?.slotData?.item == null &&
                        gridService.ItemFits(inventorySlot, size, inventorySlot.row, inventorySlot.col, inventorySlot.IsInventorySlot))
                    {
                        int amountToAdd = 1;

                        inventorySlot.slotData = new SlotData
                        {
                            item = weapon_SO,
                            amount = amountToAdd,
                            bulletsLeftInMagazine = bulletsLeftInmagazine,
                            totalBullets = totalBullets
                        };

                        if (weaponPickeable)
                        {
                            inventorySlot.slotData.barrel = weaponPickeable.currentAttachments[AttachmentType.Barrel];
                            inventorySlot.slotData.scope = weaponPickeable.currentAttachments[AttachmentType.Scope];
                            inventorySlot.slotData.stock = weaponPickeable.currentAttachments[AttachmentType.Stock];
                            inventorySlot.slotData.grip = weaponPickeable.currentAttachments[AttachmentType.Grip];
                            inventorySlot.slotData.magazine = weaponPickeable.currentAttachments[AttachmentType.Magazine];
                            inventorySlot.slotData.flashlight = weaponPickeable.currentAttachments[AttachmentType.Flashlight];
                            inventorySlot.slotData.laser = weaponPickeable.currentAttachments[AttachmentType.Laser];
                        }

                        for (int u = 0; u < size.y; u++)
                        {
                            for (int v = 0; v < size.x; v++)
                            {
                                inventorySlots[inventorySlot.row + u, inventorySlot.col + v].anchorSlot = inventorySlot;
                            }
                        }

                        weightCalculator?.UpdateCurrentWeight(weapon_SO, amountToAdd);
                        dataModel.NotifySlotAreaChanged(inventorySlot);
                        return true;
                    }
                }
            }

            return false;
        }

        public void SortInventory()
        {
            InventorySlot[,] inventorySlots = dataModel.InventorySlots;
            if (inventorySlots == null) return;

            List<SlotData> allItems = new List<SlotData>();

            for (int row = 0; row < Rows; row++)
            {
                for (int col = 0; col < Columns; col++)
                {
                    InventorySlot slot = inventorySlots[row, col];
                    if (slot != null && slot.HasValidItem && slot.GetAnchorSlot() == slot)
                    {
                        SlotData itemData = new SlotData
                        {
                            item = slot.slotData.item,
                            amount = slot.slotData.amount,
                            barrel = slot.slotData.barrel,
                            scope = slot.slotData.scope,
                            stock = slot.slotData.stock,
                            grip = slot.slotData.grip,
                            magazine = slot.slotData.magazine,
                            flashlight = slot.slotData.flashlight,
                            laser = slot.slotData.laser,
                            bulletsLeftInMagazine = slot.slotData.bulletsLeftInMagazine,
                            totalBullets = slot.slotData.totalBullets
                        };
                        allItems.Add(itemData);
                    }
                }
            }

            if (allItems.Count == 0) return;

            allItems.Sort((a, b) =>
            {
                int typeComparison = GetItemTypePriority(a.item).CompareTo(GetItemTypePriority(b.item));
                if (typeComparison != 0) return typeComparison;

                return string.Compare(a.item._name, b.item._name, System.StringComparison.Ordinal);
            });

            List<SlotData> consolidatedItems = new List<SlotData>();
            foreach (var item in allItems)
            {
                if (item.item.maxStack > 1)
                {
                    SlotData existingStack = consolidatedItems.Find(x => x.item == item.item && x.amount < item.item.maxStack);
                    if (existingStack != null)
                    {
                        int spaceAvailable = item.item.maxStack - existingStack.amount;
                        int amountToAdd = Mathf.Min(spaceAvailable, item.amount);
                        existingStack.amount += amountToAdd;

                        if (item.amount > amountToAdd)
                        {
                            item.amount -= amountToAdd;
                            consolidatedItems.Add(item);
                        }
                    }
                    else
                    {
                        consolidatedItems.Add(item);
                    }
                }
                else
                {
                    consolidatedItems.Add(item);
                }
            }

            for (int row = 0; row < Rows; row++)
            {
                for (int col = 0; col < Columns; col++)
                {
                    InventorySlot slot = inventorySlots[row, col];
                    if (slot != null)
                    {
                        gridService.ClearSlotArea(slot);
                        slot.slotData = new SlotData();
                        slot.anchorSlot = null;
                    }
                }
            }

            foreach (var itemData in consolidatedItems)
            {
                bool placed = false;
                Vector2Int itemSize = itemData.item.itemSize;

                for (int row = 0; row < Rows && !placed; row++)
                {
                    for (int col = 0; col < Columns && !placed; col++)
                    {
                        InventorySlot slot = inventorySlots[row, col];

                        if (slot.anchorSlot != null) continue;

                        if (gridService.ItemFits(slot, itemSize, row, col, true))
                        {
                            slot.slotData = itemData;
                            slot.anchorSlot = slot;

                            slot.Initialize(row, col, itemData, weaponController, inventoryManager);

                            for (int i = 0; i < itemSize.y; i++)
                            {
                                for (int j = 0; j < itemSize.x; j++)
                                {
                                    int targetRow = row + i;
                                    int targetCol = col + j;

                                    if (targetRow < Rows && targetCol < Columns)
                                    {
                                        InventorySlot targetSlot = inventorySlots[targetRow, targetCol];
                                        targetSlot.anchorSlot = slot;

                                        if (targetSlot != slot)
                                        {
                                            targetSlot.slotData = new SlotData();
                                        }
                                    }
                                }
                            }

                            placed = true;
                        }
                    }
                }

                if (!placed)
                {
                    Debug.LogWarning(
                        $"<color=red>[COWSINS]</color> Could not place item <b><color=yellow>{itemData.item._name}</color></b> during sorting. Inventory may be too full.");
                }
            }

            dataModel.NotifyGridChanged();
        }

        public int GetItemTypePriority(Item_SO item)
        {
            if (item is Weapon_SO) return 1;
            if (item is BulletTypeIdentifier_SO) return 2;
            if (item is AttachmentIdentifier_SO) return 3;
            return 4;
        }

        public void StackItems(InventorySlot slot, bool playSFX)
        {
            InventorySlot highlightedSlot = inventoryManager.highlightedSlot;
            InventorySlot draggedSlot = inventoryManager.draggedSlot;

            Item_SO highlightedItem = highlightedSlot.slotData.item;
            int stackableAmount = highlightedItem.maxStack - highlightedSlot.slotData.amount;
            int amountToMove = Mathf.Min(slot.slotData.amount, stackableAmount);

            highlightedSlot.slotData.amount += amountToMove;
            slot.slotData.amount -= amountToMove;

            if (slot.Policy.AffectsPlayerWeight != highlightedSlot.Policy.AffectsPlayerWeight)
            {
                int weightDelta = highlightedSlot.Policy.AffectsPlayerWeight ? amountToMove : -amountToMove;
                weightCalculator?.UpdateCurrentWeight(highlightedItem, weightDelta);
            }

            if (slot.slotData.amount <= 0)
                gridService.ClearSlotArea(slot);
            else 
                gridService.ReassignSlots(slot);

            gridService.ReassignSlots(highlightedSlot);

            highlightedSlot.Policy.OnSlotModified(highlightedSlot, inventoryManager);
            slot.Policy.OnSlotModified(slot, inventoryManager);

            if (inventoryManager.UseFavouritesRadialMenu)
                inventoryManager._FavItemsMenu?.UpdatePinnedFavorites(draggedSlot.GetAnchorSlot(), highlightedSlot.GetAnchorSlot());

            inventoryManager.Events.onStackItems?.Invoke();
            if (playSFX) feedbackService.OnItemPlaced();
        }

        public void SwapItems(InventorySlot targetSlot, bool playSFX)
        {
            InventorySlot highlightedSlot = inventoryManager.highlightedSlot;
            InventorySlot draggedSlot = inventoryManager.draggedSlot;

            if (draggedSlot == null || targetSlot == null) return;

            InventorySlot draggedAnchor = draggedSlot.GetAnchorSlot();
            if (draggedAnchor == null || !draggedAnchor.HasValidItem) return;

            InventorySlot targetAnchor = targetSlot.GetAnchorSlot();

            // 1. If target slot belongs to an existing different item, perform a swap!
            if (targetAnchor != null && targetAnchor.HasValidItem && targetAnchor != draggedAnchor)
            {
                if (CanSwapGridItems(draggedAnchor, targetAnchor))
                {
                    SwapGridItems(draggedAnchor, targetAnchor, playSFX);
                }
                else
                {
                    inventoryManager.NavigationBehaviour?.RevertDraggedRotation();
                }
                return;
            }

            // 2. Target is an empty slot or self: move dragged item into targetSlot
            Vector2Int draggedItemSize = draggedAnchor.GetItemSize();
            if (!gridService.IsValidPosition(targetSlot, draggedItemSize))
            {
                inventoryManager.NavigationBehaviour?.RevertDraggedRotation();
                return;
            }

            // Deep clone dragged data BEFORE clearing the slot area!
            SlotData draggedData = new SlotData(draggedAnchor.slotData);

            if (inventoryManager.UseFavouritesRadialMenu)
                inventoryManager._FavItemsMenu?.UpdatePinnedFavorites(draggedAnchor, targetSlot);

            // Clear the old footprint of the item
            gridService.ClearSlotArea(draggedAnchor);

            // Place item into targetSlot and reassign footprint
            targetSlot.slotData = draggedData;
            targetSlot.anchorSlot = targetSlot;
            gridService.ReassignSlots(targetSlot);

            if (draggedAnchor.Policy.AffectsPlayerWeight != targetSlot.Policy.AffectsPlayerWeight)
            {
                int weightDelta = targetSlot.Policy.AffectsPlayerWeight ? draggedData.amount : -draggedData.amount;
                weightCalculator?.UpdateCurrentWeight(draggedData.item, weightDelta);
            }

            if (draggedAnchor != targetSlot)
            {
                draggedAnchor.Policy.OnItemRemoved(draggedAnchor, inventoryManager);
                targetSlot.Policy.OnItemPlaced(targetSlot, inventoryManager);
                dataModel.NotifySlotChanged(draggedAnchor);
                dataModel.NotifySlotAreaChanged(draggedAnchor);
            }
            else
            {
                targetSlot.Policy.OnSlotModified(targetSlot, inventoryManager);
            }
            dataModel.NotifySlotChanged(targetSlot);
            dataModel.NotifySlotAreaChanged(targetSlot);

            inventoryManager.CalculateTotalBullets(overrideCurrentBullets: false);
            if (playSFX) feedbackService.OnItemPlaced();
        }

        public void MoveItemTo(InventorySlot currentSlot, InventorySlot targetSlot, bool playSFX = true)
        {
            inventoryManager.draggedSlot = currentSlot?.GetAnchorSlot();
            if (inventoryManager.draggedSlot != null)
                inventoryManager.draggedSlot.anchorSlot = inventoryManager.draggedSlot;

            if (targetSlot?.slotData?.item == null)
            {
                SwapItems(targetSlot, playSFX: playSFX);
            }
            else
            {
                inventoryManager.NavigationBehaviour.HighlightSlot(targetSlot);
                StackItems(inventoryManager.draggedSlot, playSFX: playSFX);
            }
            inventoryManager.NavigationBehaviour.ClearDraggedHighlightedReferences();
        }

        public bool CanStackItems(SlotData slotData)
        {
            InventorySlot highlightedSlot = inventoryManager.highlightedSlot;
            InventorySlot draggedSlot = inventoryManager.draggedSlot;

            Item_SO draggedItem = slotData.item;
            Item_SO highlightedItem = highlightedSlot?.slotData?.item;

            if (draggedItem?.maxStack == 1 || highlightedItem?.maxStack == 1) return false;

            return draggedItem && highlightedItem && draggedItem.GetType() == highlightedItem.GetType() && draggedItem?._name == highlightedItem._name && draggedSlot != highlightedSlot;
        }

        public void RotateItem(int rot)
        {
            if (inventoryManager.draggedSlot == null || inventoryManager._InventoryStyle != InventoryProManager.InventoryStyle.Tetris)
                return;

            int normalizedAngle = ((rot % 360) + 360) % 360;

            if (inventoryManager._RotationDirections == InventoryProManager.RotationDirections.TwoDirections)
            {
                if (normalizedAngle == 180) normalizedAngle = 0;
                else if (normalizedAngle == 270) normalizedAngle = 90;
                else if (normalizedAngle != 0 && normalizedAngle != 90) normalizedAngle = 0;
            }

            // Resolve target slot to highlight
            InventorySlot slotToHighlight = inventoryManager.NavigationBehaviour?.GetCurrentHoveredSlot();

            if (slotToHighlight != null) inventoryManager.NavigationBehaviour.ResetHighlightedSlotsArea(slotToHighlight.DefaultBackgroundColor, clearCurrentReferences: false);

            inventoryManager.ItemRotation = normalizedAngle;
            inventoryManager.draggedSlot.slotData.rotationAngle = normalizedAngle;
            inventoryManager.draggedSlot.slotData.isOriented = inventoryManager.IsOriented();
            inventoryManager._TooltipManager.Rotate(inventoryManager.ItemRotation);

            if (slotToHighlight != null)
            {
                inventoryManager.NavigationBehaviour.SetAreaColorOnPointerEnter(slotToHighlight);
                feedbackService.OnItemRotated();
            }

            inventoryManager.Events.onRotateItem?.Invoke();
        }

        public void DeleteItem(InventorySlot slot, bool notify = true)
        {
            SlotData cachedSlotData = slot.slotData;

            slot.Policy.OnBeforeItemRemoved(slot, inventoryManager);

            if (inventoryManager.UseFavouritesRadialMenu) inventoryManager._FavItemsMenu?.UnpinFromFavMenu(slot);

            if (!slot.Policy.IsGridBased) slot.ClearSlot();
            else gridService.ClearSlotArea(slot);

            slot.Policy.OnItemRemoved(slot, inventoryManager);

            if (cachedSlotData.item is BulletTypeIdentifier_SO) inventoryManager.CalculateTotalBullets(overrideCurrentBullets: false);
            else if (cachedSlotData.item is Weapon_SO && weaponController.IsReloading) weaponController.reloadBehaviour.StopReload();

            if (notify) feedbackService.OnItemDeleted(cachedSlotData.item._name);
        }

        public void SplitOutsideInventory(InventorySlot slot)
        {
            if (slot.slotData?.item == null) return;

            var playerMovement = inventoryManager.PlayerDependencies.GetComponent<PlayerMovement>();
            PlayerOrientation orientation = playerMovement.Orientation;
            int amount = (int)System.Math.Floor((double)slot.slotData.amount / 2);

            if (slot.slotData.item is BulletTypeIdentifier_SO)
            {
                var pick = Object.Instantiate(inventoryManager.BulletsGenericPickeable, orientation.Position + orientation.Forward * inventoryManager._InteractManager.DroppingDistance, orientation.Rotation) as BulletsPickeable;
                pick.SetBullets((BulletTypeIdentifier_SO)slot.slotData.item, amount);
            }
            else
            {
                var pick = Object.Instantiate(inventoryManager.ItemGenericPickeable, orientation.Position + orientation.Forward * inventoryManager._InteractManager.DroppingDistance, orientation.Rotation) as InventoryItemPickeable;
                pick.SetItem(slot.slotData.item, amount);
            }

            slot.slotData.amount -= amount;
            dataModel.NotifySlotAreaChanged(slot.GetAnchorSlot());

            inventoryManager.CalculateTotalBullets(overrideCurrentBullets: false);
        }

        public void DropOutsideInventory(InventorySlot slot)
        {
            if (!inventoryManager._InteractManager.CanDrop) return;
            if (!slot.HasValidItem) return;

            // If the slot being dropped is the currently opened backpack, close the container grid first
            if (inventoryManager.BackpackBehaviour != null && inventoryManager.BackpackBehaviour.ActiveBackpackSlot == slot.GetAnchorSlot()) inventoryManager.CloseBackpack();

            var playerMovement = inventoryManager.PlayerDependencies.GetComponent<PlayerMovement>();
            PlayerOrientation orientation = playerMovement.Orientation;
            Vector3 dropPosition = orientation.Position + orientation.Forward * inventoryManager._InteractManager.DroppingDistance;
            Quaternion dropRotation = orientation.Rotation;

            Pickeable pickeable = InstantiatePickeable(slot, dropPosition, dropRotation);

#if SAVE_LOAD_ADD_ON
            if (pickeable != null)
                pickeable.SaveInstance();
#endif

            if (inventoryManager.UseFavouritesRadialMenu) inventoryManager._FavItemsMenu?.UnpinFromFavMenu(slot);
            DeleteItem(slot);
            inventoryManager.CalculateTotalBullets(overrideCurrentBullets: false);

            inventoryManager._TooltipManager.Disable();
            inventoryManager.Events.onDropItem?.Invoke();
        }

        public Pickeable InstantiatePickeable(InventorySlot slot, Vector3 position, Quaternion rotation)
        {
            if (slot.IsItemWeapon)
            {
                var pick = Object.Instantiate(inventoryManager.WeaponGenericPickeable, position, rotation) as WeaponPickeable;
                SlotData tempSlotData = slot.slotData;
                Dictionary<AttachmentType, AttachmentIdentifier_SO> tempAttachments = new Dictionary<AttachmentType, AttachmentIdentifier_SO>
                {
                    { AttachmentType.Barrel, slot.slotData.barrel },
                    { AttachmentType.Scope, slot.slotData.scope },
                    { AttachmentType.Stock, slot.slotData.stock },
                    { AttachmentType.Grip, slot.slotData.grip },
                    { AttachmentType.Magazine, slot.slotData.magazine },
                    { AttachmentType.Flashlight, slot.slotData.flashlight },
                    { AttachmentType.Laser, slot.slotData.laser }
                };
                pick.DropOverrideParameters((Weapon_SO)tempSlotData.item, tempSlotData.bulletsLeftInMagazine, tempSlotData.totalBullets, tempAttachments);
                return pick;
            }
            else if (slot.IsItemBullets)
            {
                var pick = Object.Instantiate(inventoryManager.BulletsGenericPickeable, position, rotation) as BulletsPickeable;
                pick.SetBullets((BulletTypeIdentifier_SO)slot.slotData.item, slot.slotData.amount);
                return pick;
            }
            else if (slot.IsItemAttachment)
            {
                var pick = Object.Instantiate(inventoryManager.AttachmentGenericPickeable, position, rotation) as AttachmentPickeable;
                pick.dropped = true;
                pick.attachmentIdentifier = (AttachmentIdentifier_SO)slot.slotData.item;
                pick.GetVisuals();
                return pick;
            }
            else
            {
                var pick = Object.Instantiate(inventoryManager.ItemGenericPickeable, position, rotation) as InventoryItemPickeable;
                pick.SetItem(slot.slotData.item, slot.slotData.amount);
                if (slot.slotData.containedItems != null && slot.slotData.containedItems.Length > 0) pick.SetContainedItems(slot.slotData.containedItems);
                return pick;
            }
        }

        public bool HandleDropOutsideAndItemDeletion(InventorySlot anchorSlot)
        {
            InventorySlot highlightedSlot = inventoryManager.highlightedSlot;

            if (highlightedSlot != null) return false;

            UnityEngine.RectTransform inventoryRectTransform = inventoryManager._GridGenerator.GetComponent<UnityEngine.RectTransform>();
            UnityEngine.RectTransform trashRectTransform = inventoryManager.TrashButton;
            UnityEngine.RectTransform chestRectTransform = inventoryManager._GridGenerator.ChestParent != null ? inventoryManager._GridGenerator.ChestParent.GetComponent<UnityEngine.RectTransform>() : null;
            UnityEngine.RectTransform backpackRectTransform = inventoryManager._GridGenerator.BackpackParent != null ? inventoryManager._GridGenerator.BackpackParent.GetComponent<UnityEngine.RectTransform>() : null;

            Vector2 mousePosition = UnityEngine.InputSystem.Mouse.current.position.ReadValue();

            if (!UnityEngine.RectTransformUtility.RectangleContainsScreenPoint(inventoryRectTransform, mousePosition))
            {
                if (trashRectTransform != null && UnityEngine.RectTransformUtility.RectangleContainsScreenPoint(trashRectTransform, mousePosition))
                {
                    DeleteItem(anchorSlot);
                    inventoryManager.draggedSlot = null;
                    return true;
                }
                bool insideChest = chestRectTransform != null && UnityEngine.RectTransformUtility.RectangleContainsScreenPoint(chestRectTransform, mousePosition);
                bool insideBackpack = backpackRectTransform != null && UnityEngine.RectTransformUtility.RectangleContainsScreenPoint(backpackRectTransform, mousePosition);
                if (insideChest || insideBackpack)
                {
                    return false;
                }

                DropOutsideInventory(anchorSlot);
                inventoryManager.draggedSlot = null;
                return true;
            }

            return false;
        }

        public void OnEndDrag(bool clearCurrentReferences)
        {
            InventorySlot highlightedSlot = inventoryManager.highlightedSlot;
            InventorySlot draggedSlot = inventoryManager.draggedSlot;

            inventoryManager._TooltipManager.SetDragImageVisibility(false, null, false);
            inventoryManager.Events.onSlotEndDrag?.Invoke();

            if (inventoryManager.NavigationBehaviour != null && inventoryManager.NavigationBehaviour.IsRightClickDragging)
            {
                int depositedCount = inventoryManager.NavigationBehaviour.DepositedCountThisDrag;
                inventoryManager.NavigationBehaviour.EndRightClickDrag();

                Color resetColor = draggedSlot != null ? draggedSlot.DefaultBackgroundColor : (highlightedSlot != null ? highlightedSlot.DefaultBackgroundColor : Color.white);
                inventoryManager.NavigationBehaviour.ResetHighlightedSlotsArea(resetColor, clearCurrentReferences);

                if (depositedCount == 0 && highlightedSlot != null && draggedSlot != null && highlightedSlot.GetAnchorSlot() == draggedSlot.GetAnchorSlot())
                {
                    inventoryManager.onSlotRightClick?.Invoke(draggedSlot.GetAnchorSlot());
                }
                return;
            }

            if (draggedSlot == null)
            {
                inventoryManager.NavigationBehaviour?.ClearDraggedHighlightedReferences();
                return;
            }

            if (inventoryManager.PreventEndDrag())
            {
                inventoryManager.NavigationBehaviour?.RevertDraggedRotation();
                inventoryManager.NavigationBehaviour.ClearDraggedHighlightedReferences();
                return;
            }

            bool isItemDropped = HandleDropOutsideAndItemDeletion(draggedSlot.GetAnchorSlot());
            inventoryManager._GridGenerator?.UpdateBulletsHotbar(draggedSlot, highlightedSlot, weaponController);

            if (isItemDropped || highlightedSlot == null)
            {
                if (!isItemDropped)
                    inventoryManager.NavigationBehaviour?.RevertDraggedRotation();
                inventoryManager.NavigationBehaviour.ClearDraggedHighlightedReferences();
                return;
            }

            bool canStack = CanStackItems(draggedSlot.slotData);
            if (canStack) StackItems(draggedSlot, playSFX: true);
            else if (CanPlaceOrSwap(draggedSlot, highlightedSlot)) ExecutePlacementOrSwap(draggedSlot, highlightedSlot, playSFX: true);
            else
            {
                inventoryManager.NavigationBehaviour?.RevertDraggedRotation();
            }

            inventoryManager.NavigationBehaviour.ResetHighlightedSlotsArea(draggedSlot.DefaultBackgroundColor, clearCurrentReferences);
        }

        public bool DepositSingleItem(InventorySlot sourceSlot, InventorySlot targetSlot)
        {
            if (sourceSlot == null || targetSlot == null || sourceSlot == targetSlot) return false;
            if (!sourceSlot.HasValidItem || sourceSlot.slotData.amount < 1) return false;

            Item_SO item = sourceSlot.slotData.item;
            if (item == null || item.maxStack <= 1) return false;

            Vector2Int itemSize = sourceSlot.GetItemSize();
            if (!gridService.IsValidPosition(targetSlot, itemSize)) return false;

            InventorySlot targetAnchor = targetSlot.GetAnchorSlot();

            // Target slot is empty
            if (targetAnchor == null || !targetAnchor.HasValidItem)
            {
                if (!gridService.ItemFits(targetSlot, itemSize, targetSlot.row, targetSlot.col, targetSlot.IsInventorySlot))
                    return false;

                targetSlot.slotData = new SlotData
                {
                    item = item,
                    amount = 1,
                    bulletsLeftInMagazine = sourceSlot.slotData.bulletsLeftInMagazine,
                    totalBullets = sourceSlot.slotData.totalBullets,
                    isOriented = sourceSlot.slotData.isOriented,
                    rotationAngle = sourceSlot.slotData.rotationAngle
                };
                targetSlot.anchorSlot = targetSlot;

                InventorySlot[,] targetSlots = dataModel.GetGridForSlot(targetSlot) ?? (targetSlot.IsInventorySlot ? dataModel.InventorySlots : dataModel.ChestSlots);
                if (targetSlots != null)
                {
                    for (int i = 0; i < itemSize.y; i++)
                    {
                        for (int j = 0; j < itemSize.x; j++)
                        {
                            targetSlots[targetSlot.row + i, targetSlot.col + j].anchorSlot = targetSlot;
                        }
                    }
                }

                sourceSlot.slotData.amount -= 1;

                if (sourceSlot.Policy.AffectsPlayerWeight != targetSlot.Policy.AffectsPlayerWeight)
                {
                    int weightDelta = targetSlot.Policy.AffectsPlayerWeight ? 1 : -1;
                    weightCalculator?.UpdateCurrentWeight(item, weightDelta);
                }

                targetSlot.Policy.OnItemPlaced(targetSlot, inventoryManager);
                sourceSlot.Policy.OnItemRemoved(sourceSlot, inventoryManager);

                dataModel.NotifySlotAreaChanged(targetSlot);
                if (sourceSlot.slotData.amount <= 0)
                    gridService.ClearSlotArea(sourceSlot);
                else
                    dataModel.NotifySlotAreaChanged(sourceSlot.GetAnchorSlot());

                if (item is BulletTypeIdentifier_SO)
                    inventoryManager.CalculateTotalBullets(overrideCurrentBullets: false);

                feedbackService.OnItemPlaced();
                return true;
            }
            // Target slot has matching stackable item and capacity
            else if (targetAnchor.slotData?.item == item && targetAnchor.slotData.amount < item.maxStack)
            {
                targetAnchor.slotData.amount += 1;
                sourceSlot.slotData.amount -= 1;

                if (sourceSlot.Policy.AffectsPlayerWeight != targetSlot.Policy.AffectsPlayerWeight)
                {
                    int weightDelta = targetSlot.Policy.AffectsPlayerWeight ? 1 : -1;
                    weightCalculator?.UpdateCurrentWeight(item, weightDelta);
                }

                targetAnchor.Policy.OnSlotModified(targetAnchor, inventoryManager);
                sourceSlot.Policy.OnItemRemoved(sourceSlot, inventoryManager);

                dataModel.NotifySlotAreaChanged(targetAnchor);
                if (sourceSlot.slotData.amount <= 0)
                    gridService.ClearSlotArea(sourceSlot);
                else
                    dataModel.NotifySlotAreaChanged(sourceSlot.GetAnchorSlot());

                if (item is BulletTypeIdentifier_SO)
                    inventoryManager.CalculateTotalBullets(overrideCurrentBullets: false);

                feedbackService.OnItemPlaced();
                return true;
            }

            return false;
        }

        public bool CanSwapGridItems(InventorySlot anchorA, InventorySlot anchorB)
        {
            if (anchorA == null || anchorB == null) return false;
            anchorA = anchorA.GetAnchorSlot();
            anchorB = anchorB.GetAnchorSlot();
            if (anchorA == anchorB) return false;

            if (!anchorA.HasValidItem || !anchorB.HasValidItem) return false;
            if (!anchorA.Policy.IsGridBased || !anchorB.Policy.IsGridBased) return false;

            Vector2Int sizeA = anchorA.GetItemSize();
            Vector2Int sizeB = anchorB.GetItemSize();

            InventorySlot[,] gridA = dataModel.GetGridForSlot(anchorA) ?? (anchorA.Policy.SlotType == SlotType.Inventory ? dataModel.InventorySlots : dataModel.ChestSlots);
            InventorySlot[,] gridB = dataModel.GetGridForSlot(anchorB) ?? (anchorB.Policy.SlotType == SlotType.Inventory ? dataModel.InventorySlots : dataModel.ChestSlots);
            if (gridA == null || gridB == null) return false;

            // Bounds check for Item A placed at anchorB in gridB
            if (!gridService.IsSlotWithinBounds(anchorB.row, anchorB.col, sizeA, gridB))
                return false;

            // Bounds check for Item B placed at anchorA in gridA
            if (!gridService.IsSlotWithinBounds(anchorA.row, anchorA.col, sizeB, gridA))
                return false;

            // In the same grid, the destination rectangles must not overlap each other
            if (gridA == gridB)
            {
                bool rowOverlap = anchorB.row < anchorA.row + sizeB.y && anchorB.row + sizeA.y > anchorA.row;
                bool colOverlap = anchorB.col < anchorA.col + sizeB.x && anchorB.col + sizeA.x > anchorA.col;
                if (rowOverlap && colOverlap) return false;
            }

            // Check collision for Item A at anchorB in gridB:
            // All slots in footprint must either belong to anchorA, anchorB, or be empty
            for (int i = 0; i < sizeA.y; i++)
            {
                for (int j = 0; j < sizeA.x; j++)
                {
                    InventorySlot slot = gridB[anchorB.row + i, anchorB.col + j];
                    if (slot == null) return false;
                    InventorySlot anchor = slot.anchorSlot;
                    if (anchor != null && anchor != anchorA && anchor != anchorB && anchor.HasValidItem)
                        return false;
                }
            }

            // Check collision for Item B at anchorA in gridA:
            // All slots in footprint must either belong to anchorA, anchorB, or be empty
            for (int i = 0; i < sizeB.y; i++)
            {
                for (int j = 0; j < sizeB.x; j++)
                {
                    InventorySlot slot = gridA[anchorA.row + i, anchorA.col + j];
                    if (slot == null) return false;
                    InventorySlot anchor = slot.anchorSlot;
                    if (anchor != null && anchor != anchorA && anchor != anchorB && anchor.HasValidItem)
                        return false;
                }
            }

            return true;
        }

        public void SwapGridItems(InventorySlot anchorA, InventorySlot anchorB, bool playSFX = true)
        {
            if (!CanSwapGridItems(anchorA, anchorB)) return;

            anchorA = anchorA.GetAnchorSlot();
            anchorB = anchorB.GetAnchorSlot();

            if (inventoryManager.UseFavouritesRadialMenu)
                inventoryManager._FavItemsMenu?.UpdatePinnedFavorites(anchorA, anchorB);

            // Deep clone both slot datas BEFORE clearing any slot areas!
            SlotData dataA = new SlotData(anchorA.slotData);
            SlotData dataB = new SlotData(anchorB.slotData);

            gridService.ClearSlotArea(anchorA);
            gridService.ClearSlotArea(anchorB);

            anchorA.slotData = dataB;
            anchorB.slotData = dataA;

            anchorA.anchorSlot = anchorA;
            anchorB.anchorSlot = anchorB;

            gridService.ReassignSlots(anchorA);
            gridService.ReassignSlots(anchorB);

            if (anchorA.Policy.AffectsPlayerWeight != anchorB.Policy.AffectsPlayerWeight)
            {
                int sign = anchorA.Policy.AffectsPlayerWeight ? 1 : -1;
                weightCalculator?.UpdateCurrentWeight(dataA.item, -sign * dataA.amount);
                weightCalculator?.UpdateCurrentWeight(dataB.item, sign * dataB.amount);
            }

            anchorA.Policy.OnSlotModified(anchorA, inventoryManager);
            anchorB.Policy.OnSlotModified(anchorB, inventoryManager);

            dataModel.NotifySlotChanged(anchorA);
            dataModel.NotifySlotChanged(anchorB);
            dataModel.NotifySlotAreaChanged(anchorA);
            dataModel.NotifySlotAreaChanged(anchorB);

            inventoryManager.CalculateTotalBullets(overrideCurrentBullets: false);
            if (playSFX) feedbackService.OnItemPlaced();
        }

        public bool CanPlaceOrSwap(InventorySlot sourceSlot, InventorySlot targetSlot)
        {
            if (sourceSlot == null || targetSlot == null) return false;

            InventorySlot sourceAnchor = sourceSlot.GetAnchorSlot();
            if (sourceAnchor == null || !sourceAnchor.HasValidItem) return false;

            Item_SO sourceItem = sourceAnchor.slotData.item;
            if (sourceItem == null) return false;

            InventorySlot targetAnchor = targetSlot.GetAnchorSlot();

            // Dropping back onto original slot
            if (sourceAnchor == targetAnchor)
            {
                if (targetSlot.Policy.IsGridBased)
                {
                    Vector2Int size = sourceAnchor.GetItemSize();
                    if (gridService.IsValidPosition(targetSlot, size)) return true;
                    if (targetSlot != sourceAnchor && gridService.IsValidPosition(sourceAnchor, size)) return true;
                    return false;
                }
                return true;
            }

            // Target slot must accept source item
            if (!targetSlot.AcceptsItem(sourceItem))
                return false;

            bool targetHasItem = targetAnchor != null && targetAnchor.HasValidItem;

            // If target slot is occupied, source slot must accept target item for swap
            if (targetHasItem)
            {
                Item_SO targetItem = targetAnchor.slotData.item;
                if (targetItem == null || !sourceAnchor.AcceptsItem(targetItem))
                    return false;
            }

            bool sourceIsGrid = sourceAnchor.Policy.IsGridBased;
            bool targetIsGrid = targetSlot.Policy.IsGridBased;

            // between Grids
            if (sourceIsGrid && targetIsGrid)
            {
                if (targetHasItem)
                {
                    return CanSwapGridItems(sourceAnchor, targetAnchor);
                }
                else
                {
                    Vector2Int size = sourceAnchor.GetItemSize();
                    return gridService.IsValidPosition(targetSlot, size);
                }
            }

            // between singles
            if (!sourceIsGrid && !targetIsGrid) return CanSwapOrMoveSingleSlots(sourceAnchor, targetSlot);

            // between Single and Grid
            if (!sourceIsGrid && targetIsGrid) return CanSwapOrMoveSingleAndGrid(sourceAnchor, targetSlot);
            else return CanSwapOrMoveSingleAndGrid(targetSlot, sourceAnchor);
        }

        public bool ExecutePlacementOrSwap(InventorySlot sourceSlot, InventorySlot targetSlot, bool playSFX = true)
        {
            if (!CanPlaceOrSwap(sourceSlot, targetSlot)) return false;

            InventorySlot sourceAnchor = sourceSlot.GetAnchorSlot();
            InventorySlot targetAnchor = targetSlot.GetAnchorSlot();

            if (sourceAnchor == targetAnchor)
            {
                if (targetSlot.Policy.IsGridBased)
                {
                    Vector2Int size = sourceAnchor.GetItemSize();
                    InventorySlot placementSlot = targetSlot;
                    if (targetSlot != sourceAnchor && !gridService.IsValidPosition(targetSlot, size) && gridService.IsValidPosition(sourceAnchor, size))
                        placementSlot = sourceAnchor;
                    SwapItems(placementSlot, playSFX);
                }
                return true;
            }

            bool sourceIsGrid = sourceAnchor.Policy.IsGridBased;
            bool targetIsGrid = targetSlot.Policy.IsGridBased;

            if (sourceIsGrid && targetIsGrid)
            {
                SwapItems(targetSlot, playSFX);
                return true;
            }

            if (!sourceIsGrid && !targetIsGrid)
            {
                return SwapOrMoveSingleSlots(sourceAnchor, targetSlot);
            }

            if (!sourceIsGrid && targetIsGrid)
            {
                return SwapOrMoveSingleAndGrid(sourceAnchor, targetSlot);
            }
            else
            {
                return SwapOrMoveSingleAndGrid(targetSlot, sourceAnchor);
            }
        }

        public bool CanSwapOrMoveSingleAndGrid(InventorySlot singleSlot, InventorySlot gridSlot)
        {
            if (singleSlot == null || gridSlot == null) return false;
            if (singleSlot.Policy.IsGridBased) return false;
            if (!gridSlot.Policy.IsGridBased) return false;

            InventorySlot gridAnchor = gridSlot.GetAnchorSlot();
            bool singleHasItem = singleSlot.HasValidItem;
            bool gridHasItem = gridAnchor != null && gridAnchor.HasValidItem;

            if (!singleHasItem && !gridHasItem) return false;

            if (singleHasItem && !gridSlot.AcceptsItem(singleSlot.slotData.item))
                return false;
            if (gridHasItem && !singleSlot.AcceptsItem(gridAnchor.slotData.item))
                return false;

            // Single has item, Grid is empty -> check if single's item fits in grid
            if (singleHasItem && !gridHasItem)
            {
                Vector2Int itemSize = singleSlot.GetItemSize();
                return gridService.IsValidPosition(gridSlot, itemSize);
            }

            // Grid has item, Single is empty -> single slot always fits 1 item
            if (gridHasItem && !singleHasItem)
            {
                return true;
            }

            // Both have items -> Swap check
            if (singleHasItem && gridHasItem)
            {
                Vector2Int singleItemSize = singleSlot.GetItemSize();
                InventorySlot[,] grid = dataModel.GetGridForSlot(gridAnchor) ?? (gridAnchor.Policy.SlotType == SlotType.Inventory ? dataModel.InventorySlots : dataModel.ChestSlots);
                if (grid == null) return false;

                if (!gridService.IsSlotWithinBounds(gridAnchor.row, gridAnchor.col, singleItemSize, grid))
                    return false;

                for (int i = 0; i < singleItemSize.y; i++)
                {
                    for (int j = 0; j < singleItemSize.x; j++)
                    {
                        InventorySlot slotToCheck = grid[gridAnchor.row + i, gridAnchor.col + j];
                        if (slotToCheck == null) return false;
                        InventorySlot anchor = slotToCheck.anchorSlot;
                        if (anchor != null && anchor != gridAnchor && anchor.HasValidItem)
                            return false;
                    }
                }

                return true;
            }

            return false;
        }

        public bool SwapOrMoveSingleAndGrid(InventorySlot singleSlot, InventorySlot gridSlot)
        {
            if (!CanSwapOrMoveSingleAndGrid(singleSlot, gridSlot)) return false;

            InventorySlot gridAnchor = gridSlot.GetAnchorSlot();

            singleSlot.Policy.OnBeforeItemRemoved(singleSlot, inventoryManager);

            bool singleHadItem = singleSlot.HasValidItem;
            bool gridHadItem = gridAnchor != null && gridAnchor.HasValidItem;

            SlotData oldSingleData = singleHadItem ? new SlotData(singleSlot.slotData) : new SlotData();
            SlotData oldGridData = gridHadItem ? new SlotData(gridAnchor.slotData) : new SlotData();

            if (oldGridData.item is Weapon_SO && oldGridData.amount <= 0)
                oldGridData.amount = 1;
            if (oldSingleData.item is Weapon_SO && oldSingleData.amount <= 0)
                oldSingleData.amount = 1;

            if (gridHadItem) gridService.ClearSlotArea(gridAnchor);

            if (singleHadItem && gridHadItem)
            {
                singleSlot.slotData = oldGridData;
                singleSlot.anchorSlot = null;

                gridAnchor.slotData = oldSingleData;
                gridAnchor.anchorSlot = gridAnchor;

                gridService.ReassignSlots(gridAnchor);
                dataModel.NotifySlotChanged(singleSlot);
                dataModel.NotifySlotChanged(gridAnchor);
                dataModel.NotifySlotAreaChanged(gridAnchor);
            }
            else if (gridHadItem && !singleHadItem)
            {
                singleSlot.slotData = oldGridData;
                singleSlot.anchorSlot = null;

                gridAnchor.slotData = new SlotData();
                gridAnchor.anchorSlot = null;

                dataModel.NotifySlotChanged(singleSlot);
                dataModel.NotifySlotChanged(gridAnchor);
                dataModel.NotifySlotAreaChanged(gridAnchor);
            }
            else if (singleHadItem && !gridHadItem)
            {
                gridSlot.slotData = oldSingleData;
                gridSlot.anchorSlot = gridSlot;

                singleSlot.slotData = new SlotData();
                singleSlot.anchorSlot = null;

                gridService.ReassignSlots(gridSlot);
                dataModel.NotifySlotChanged(singleSlot);
                dataModel.NotifySlotChanged(gridSlot);
                dataModel.NotifySlotAreaChanged(gridSlot);
            }

            // Lifecycle sync via policies
            singleSlot.Policy.OnItemPlaced(singleSlot, inventoryManager);

            InventorySlot effectiveGrid = gridAnchor ?? gridSlot;
            effectiveGrid?.Policy.OnSlotModified(effectiveGrid, inventoryManager);

            // Weight calculation
            bool singleAffects = singleSlot.Policy.AffectsPlayerWeight;
            bool gridAffects = effectiveGrid != null && effectiveGrid.Policy.AffectsPlayerWeight;
            if (singleAffects != gridAffects)
            {
                if (singleHadItem && !gridHadItem)
                    weightCalculator?.UpdateCurrentWeight(oldSingleData.item, gridAffects ? oldSingleData.amount : -oldSingleData.amount);
                else if (gridHadItem && !singleHadItem)
                    weightCalculator?.UpdateCurrentWeight(oldGridData.item, singleAffects ? oldGridData.amount : -oldGridData.amount);
                else if (singleHadItem && gridHadItem)
                {
                    weightCalculator?.UpdateCurrentWeight(oldSingleData.item, gridAffects ? oldSingleData.amount : -oldSingleData.amount);
                    weightCalculator?.UpdateCurrentWeight(oldGridData.item, singleAffects ? oldGridData.amount : -oldGridData.amount);
                }
            }

            if (inventoryManager.UseFavouritesRadialMenu)
                inventoryManager._FavItemsMenu?.UpdatePinnedFavorites(effectiveGrid, singleSlot);

            inventoryManager.CalculateTotalBullets(overrideCurrentBullets: false);
            feedbackService.OnItemPlaced();
            return true;
        }

        public bool CanSwapOrMoveSingleSlots(InventorySlot slotA, InventorySlot slotB)
        {
            if (slotA == null || slotB == null || slotA == slotB) return false;
            if (slotA.Policy.IsGridBased || slotB.Policy.IsGridBased) return false;

            bool aHasItem = slotA.HasValidItem;
            bool bHasItem = slotB.HasValidItem;

            if (!aHasItem && !bHasItem) return false;

            if (aHasItem && !slotB.AcceptsItem(slotA.slotData.item))
                return false;
            if (bHasItem && !slotA.AcceptsItem(slotB.slotData.item))
                return false;

            return true;
        }

        public bool SwapOrMoveSingleSlots(InventorySlot slotA, InventorySlot slotB)
        {
            if (!CanSwapOrMoveSingleSlots(slotA, slotB)) return false;

            slotA.Policy.OnBeforeItemRemoved(slotA, inventoryManager);
            slotB.Policy.OnBeforeItemRemoved(slotB, inventoryManager);

            SlotData dataA = slotA.HasValidItem ? new SlotData(slotA.slotData) : new SlotData();
            SlotData dataB = slotB.HasValidItem ? new SlotData(slotB.slotData) : new SlotData();

            slotA.slotData = dataB;
            slotA.anchorSlot = null;

            slotB.slotData = dataA;
            slotB.anchorSlot = null;

            dataModel.NotifySlotChanged(slotA);
            dataModel.NotifySlotChanged(slotB);

            slotA.Policy.OnItemPlaced(slotA, inventoryManager);
            slotB.Policy.OnItemPlaced(slotB, inventoryManager);

            if (inventoryManager.UseFavouritesRadialMenu)
                inventoryManager._FavItemsMenu?.UpdatePinnedFavorites(slotA, slotB);

            inventoryManager.CalculateTotalBullets(overrideCurrentBullets: false);
            feedbackService.OnItemPlaced();
            return true;
        }

        public bool TransferWeaponToHotbarSlot(InventorySlot sourceSlot, InventorySlot targetHotbarSlot)
        {
            if (sourceSlot == null || targetHotbarSlot == null) return false;
            sourceSlot = sourceSlot.GetAnchorSlot();
            if (!sourceSlot.HasValidItem || !sourceSlot.IsItemWeapon) return false;
            if (targetHotbarSlot.Policy.SlotType != SlotType.Hotbar) return false;

            if (sourceSlot == targetHotbarSlot) return false;

            if (!sourceSlot.Policy.IsGridBased)
            {
                return SwapOrMoveSingleSlots(sourceSlot, targetHotbarSlot);
            }
            else
            {
                return SwapOrMoveSingleAndGrid(targetHotbarSlot, sourceSlot);
            }
        }

        public void HandleHotbarNumberKey(int hotbarIndex)
        {
            if (!inventoryManager.IsInventoryOpen) return;
            InventorySlot[] hotbarSlots = inventoryManager.HotbarSlots;
            if (hotbarSlots == null || hotbarIndex < 0 || hotbarIndex >= hotbarSlots.Length) return;

            InventorySlot hoveredSlot = inventoryManager.highlightedSlot;
            InventorySlot sourceSlot = null;

            if (inventoryManager.draggedSlot != null && inventoryManager.draggedSlot.HasValidItem)
            {
                sourceSlot = inventoryManager.draggedSlot.GetAnchorSlot();
            }
            else if (hoveredSlot != null && hoveredSlot.HasValidItem)
            {
                sourceSlot = hoveredSlot.GetAnchorSlot();
            }

            if (sourceSlot == null || !sourceSlot.HasValidItem || !sourceSlot.IsItemWeapon)
                return;

            InventorySlot targetHotbarSlot = hotbarSlots[hotbarIndex];
            if (targetHotbarSlot == null) return;

            bool wasDragging = inventoryManager.draggedSlot != null;
            bool success = TransferWeaponToHotbarSlot(sourceSlot, targetHotbarSlot);

            if (success)
            {
                if (wasDragging)
                {
                    inventoryManager.NavigationBehaviour?.CancelClickGrab();
                    inventoryManager.UIController?.StopDragVisuals();
                    inventoryManager.NavigationBehaviour?.ClearDraggedHighlightedReferences();
                }

                if (hoveredSlot != null)
                {
                    inventoryManager.NavigationBehaviour?.HighlightSlot(hoveredSlot);
                    inventoryManager._TooltipManager?.Disable();
                }
            }
        }

        private void SyncHotbarSlotWeapon(int colId)
        {
            InventorySlot[] hotbarSlots = dataModel.HotbarSlots ?? inventoryManager.HotbarSlots;
            if (hotbarSlots == null || colId < 0 || colId >= hotbarSlots.Length) return;

            InventorySlot slot = hotbarSlots[colId];
            if (slot != null && slot.HasValidItem && slot.slotData.item is Weapon_SO weaponSO)
            {
                if (weaponController.CurrentWeaponIndex == colId && weaponController.Inventory[colId] != null)
                    weaponController.ReleaseCurrentWeapon();
                else if (weaponController.Inventory[colId] != null)
                {
                    Object.Destroy(weaponController.Inventory[colId].gameObject);
                }

                inventoryManager.PlayerDependencies.UIManager?.GetComponentInChildren<InventoryHUDModule>()?.SetInventoryUISlotWeapon(colId, weaponSO);
                weaponController.InstantiateWeapon(weaponSO, colId, slot.slotData.bulletsLeftInMagazine, slot.slotData.totalBullets);

                SlotData slotData = slot.slotData;
                var attachments = new[] {
                    slotData.barrel, slotData.scope, slotData.stock, slotData.grip,
                    slotData.magazine, slotData.flashlight, slotData.laser
                };
                foreach (var attachment in attachments)
                {
                    if (attachment != null)
                        weaponController.attachmentsSystem.AssignNewAttachmentToWeapon(attachment, colId);
                }

                if (weaponController.CurrentWeaponIndex == colId)
                {
                    if (weaponController.Id == null && weaponController.Inventory[colId] != null)
                        weaponController.weaponInventoryBehaviour?.UnHolster(weaponController.Inventory[colId].gameObject, false);
                    weaponController.Weapon = weaponSO;
                }
            }
            else
            {
                if (weaponController.CurrentWeaponIndex == colId && weaponController.Inventory[colId] != null)
                    weaponController.ReleaseCurrentWeapon();
                else if (weaponController.Inventory[colId] != null)
                {
                    Object.Destroy(weaponController.Inventory[colId].gameObject);
                    weaponController.Inventory[colId] = null;
                }
                inventoryManager.PlayerDependencies.UIManager?.GetComponentInChildren<InventoryHUDModule>()?.SetInventoryUISlotWeapon(colId, null);
            }
        }
    }
}
