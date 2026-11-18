using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace cowsins.Inventory
{
    public sealed class InventoryProNavigationBehaviour
    {
        private readonly InventoryProManager manager;
        private bool isRightClickDrag = false;
        private readonly HashSet<InventorySlot> depositedSlotsThisDrag = new HashSet<InventorySlot>();
        private bool isClickGrabbed = false;
        private int dragStartRotationAngle = 0;
        private bool dragStartIsOriented = true;

        public bool IsRightClickDragging => isRightClickDrag;
        public int DepositedCountThisDrag => depositedSlotsThisDrag.Count;
        public bool IsClickGrabbed
        {
            get => isClickGrabbed;
            set => isClickGrabbed = value;
        }
        public int DragStartRotationAngle => dragStartRotationAngle;
        public bool DragStartIsOriented => dragStartIsOriented;

        public bool WasDraggedItemRotated()
        {
            if (manager.draggedSlot == null || manager.draggedSlot.slotData == null) return false;
            return manager.draggedSlot.slotData.rotationAngle != dragStartRotationAngle || manager.draggedSlot.slotData.isOriented != dragStartIsOriented;
        }

        public InventorySlot GetCurrentHoveredSlot()
        {
            if (manager.highlightedSlot != null) return manager.highlightedSlot;

            // Try raycasting under current pointer position
            if (EventSystem.current != null)
            {
                Vector2 screenPos = UnityEngine.InputSystem.Mouse.current != null ? UnityEngine.InputSystem.Mouse.current.position.ReadValue() : (Vector2)Input.mousePosition;
                PointerEventData pointerData = new PointerEventData(EventSystem.current) { position = screenPos };
                var results = new List<RaycastResult>();
                EventSystem.current.RaycastAll(pointerData, results);
                for (int i = 0; i < results.Count; i++)
                {
                    InventorySlot slot = results[i].gameObject.GetComponentInParent<InventorySlot>();
                    if (slot != null)
                    {
                        HighlightSlot(slot);
                        return slot;
                    }
                }
            }

            // Fallback to currently dragged slot anchor if available
            if (manager.draggedSlot != null)
            {
                InventorySlot anchor = manager.draggedSlot.GetAnchorSlot();
                HighlightSlot(anchor);
                return anchor;
            }

            return null;
        }

        public void RevertDraggedRotation()
        {
            if (manager.draggedSlot != null && manager.draggedSlot.slotData != null)
            {
                manager.draggedSlot.slotData.rotationAngle = dragStartRotationAngle;
                manager.draggedSlot.slotData.isOriented = dragStartIsOriented;
                manager.ItemRotation = dragStartRotationAngle;
                manager.draggedSlot.GetAnchorSlot().UpdateSlotGraphics();
            }
        }

        public void EndRightClickDrag()
        {
            isRightClickDrag = false;
            depositedSlotsThisDrag.Clear();
        }

        public void StartClickGrab(InventorySlot slot)
        {
            if (slot == null || !slot.HasValidItem) return;

            isClickGrabbed = true;
            manager.draggedSlot = slot;
            HighlightSlot(slot);

            int initialRotation = 0;
            if (slot.slotData != null)
            {
                initialRotation = slot.slotData.rotationAngle;
                if (initialRotation == 0 && !slot.slotData.isOriented)
                    initialRotation = 90;
                dragStartRotationAngle = initialRotation;
                dragStartIsOriented = initialRotation == 0 || initialRotation == 180;
            }
            manager.ItemRotation = initialRotation;

            var operations = manager._GridGenerator.Operations;
            operations.RotateItem(manager.ItemRotation);

            manager.UIController.StartDragVisuals(slot.slotData.item);
            SetAreaColorOnPointerEnter(slot);

            manager.Events.onSlotBeginDrag?.Invoke();
            manager.FeedbackService?.PlaySound(manager.HighlightItemSFX);
        }

        public void CancelClickGrab()
        {
            isClickGrabbed = false;
            RevertDraggedRotation();
            manager.UIController.StopDragVisuals();
            if (manager.draggedSlot != null) ResetHighlightedSlotsArea(manager.draggedSlot.DefaultBackgroundColor, clearCurrentReferences: true);
            else ClearDraggedHighlightedReferences();
        }

        public void PlaceOrSwapClickGrab(InventorySlot targetSlot)
        {
            if (manager.draggedSlot == null) return;

            isClickGrabbed = false;
            manager.highlightedSlot = targetSlot;
            manager._GridGenerator.Operations.OnEndDrag(clearCurrentReferences: true);
        }

        public InventoryProNavigationBehaviour(InventoryProManager manager)
        {
            this.manager = manager;
        }

        public void InitializeControllerNavigation(InventoryInputHandler inputHandler)
        {
            inputHandler?.InitializeControllerNavigation();
        }

        public void SelectSlotController()
        {
            if (!manager.highlightedSlot) return;

            if (manager.highlightedSlot.HasValidItem || manager.highlightedSlot.anchorSlot)
            {
                if (manager.draggedSlot)
                {
                    manager._GridGenerator.Operations.OnEndDrag(false);
                    manager.draggedSlot = null;
                }
                else
                {
                    manager.draggedSlot = manager.highlightedSlot.GetAnchorSlot();
                    manager.draggedSlot.OnBeginDrag(null);
                }
            }
            else if (manager.draggedSlot)
            {
                manager._GridGenerator.Operations.OnEndDrag(false);
                manager.draggedSlot = null;
            }

            manager.UIController.ToggleDeviceSpecificUI(true);
        }

        public void HighlightSlot(InventorySlot slot)
        {
            manager.highlightedSlot?.Unselect();
            manager.highlightedSlot = slot;
            manager.highlightedSlot?.Select();

            if (manager.highlightedSlot)
            {
                manager.Events.onHighlightSlot?.Invoke();
                manager.FeedbackService?.PlaySound(manager.HighlightItemSFX);
            }
        }

        public void SetAreaColorOnPointerEnter(InventorySlot slot)
        {
            if (slot == null) return;

            if (!slot.Policy.IsGridBased)
            {
                manager.highlightedSlotArea.Clear();
                manager.highlightedSlotArea.Add(slot);
                bool canAttachDraggedToWeapon = slot.IsHotbarSlot && manager.CanAttachDraggedAttachmentToWeaponSlot();
                bool canPlaceOrSwap = manager.draggedSlot != null && manager._GridGenerator.Operations.CanPlaceOrSwap(manager.draggedSlot, slot);
                bool canStack = manager.draggedSlot != null && manager.Operations.CanStackItems(manager.draggedSlot.slotData);

                bool isAvailable = canAttachDraggedToWeapon || canPlaceOrSwap || canStack;
                SetSlotsColors(isAvailable ? manager.AvailableSlotColor : manager.OccupiedSlotColor);
                return;
            }

            HighlightAvailableSlots(slot);
        }

        public void ClearDraggedHighlightedReferences()
        {
            isClickGrabbed = false;
            EndRightClickDrag();
            manager.draggedSlot = null;
            HighlightSlot(null);
        }

        public void HighlightAvailableSlots(InventorySlot slot)
        {
            if (manager.highlightedSlot?.GetAnchorSlot()?.slotData?.item != null && manager.CanAttachDraggedAttachmentToWeaponSlot())  HighlightAttachmentToWeaponSlots(slot);
            else HighlightStandardItemSlots(slot);
        }

        private void HighlightAttachmentToWeaponSlots(InventorySlot slot)
        {
            manager.highlightedSlotArea.Clear();
            int startAnchorRow = slot.GetAnchorSlot().row;
            int startAnchorCol = slot.GetAnchorSlot().col;
            Vector2Int AnchorItemSize = slot.GetAnchorSlot().GetItemSize();
            InventorySlot[,] inventorySlots = manager.DataModel?.GetGridForSlot(slot) ?? (slot.IsInventorySlot ? manager.InventorySlots : manager.ChestSlots);

            for (int i = 0; i < AnchorItemSize.y; i++)
            {
                for (int j = 0; j < AnchorItemSize.x; j++)
                {
                    int targetRow = startAnchorRow + i;
                    int targetCol = startAnchorCol + j;

                    if (manager._GridGenerator.Operations.IsSlotWithinBounds(targetRow, targetCol, inventorySlots))
                    {
                        manager.highlightedSlotArea.Add(inventorySlots[targetRow, targetCol]);
                    }
                }
            }
            SetSlotsColors(manager.AvailableSlotColor);
        }

        private void HighlightStandardItemSlots(InventorySlot slot)
        {
            manager.highlightedSlotArea.Clear();
            bool draggedSlotHasItem = manager.draggedSlot != null && manager.draggedSlot.HasValidItem;
            Vector2Int itemSize = draggedSlotHasItem ? manager.draggedSlot.GetItemSize() : new Vector2Int(1, 1);
            int startRow = slot.row;
            int startCol = slot.col;

            InventorySlot[,] inventorySlots = manager.DataModel?.GetGridForSlot(slot) ?? (slot.IsInventorySlot ? manager.InventorySlots : manager.ChestSlots);
            if (inventorySlots == null) return;

            bool isOutOfBounds = !manager._GridGenerator.Operations.IsSlotWithinBounds(startRow, startCol, itemSize, inventorySlots);

            for (int i = 0; i < itemSize.y; i++)
            {
                for (int j = 0; j < itemSize.x; j++)
                {
                    int targetRow = startRow + i;
                    int targetCol = startCol + j;

                    if (manager._GridGenerator.Operations.IsSlotWithinBounds(targetRow, targetCol, inventorySlots)) manager.highlightedSlotArea.Add(inventorySlots[targetRow, targetCol]);
                }
            }

            bool isOccupied = false;

            if (isOutOfBounds || manager.highlightedSlotArea.Count < itemSize.x * itemSize.y) isOccupied = true;
            else if (draggedSlotHasItem)
            {
                bool canStack = manager.Operations.CanStackItems(manager.draggedSlot.slotData);
                bool canPlaceOrSwap = manager._GridGenerator.Operations.CanPlaceOrSwap(manager.draggedSlot, slot);
                isOccupied = !canStack && !canPlaceOrSwap;
            }

            Color color = isOccupied ? manager.OccupiedSlotColor : manager.AvailableSlotColor;

            SetSlotsColors(color);
        }

        public void SetSlotsColors(Color color)
        {
            if (manager.highlightedSlotArea == null) return;
            foreach (var slot in manager.highlightedSlotArea)
            {
                if (slot != null) SetBackgroundColor(slot, color);
            }
        }

        public void SetBackgroundColor(InventorySlot slot, Color color)
        {
            if (slot != null && slot.BackgroundImage != null)
                slot.BackgroundImage.color = color;
        }

        public void ResetHighlightedSlotsArea(Color defaultBackgroundColor, bool clearCurrentReferences)
        {
            if (manager.highlightedSlot != null) manager.highlightedSlotArea.Add(manager.highlightedSlot);
            if (manager.draggedSlot != null) manager.highlightedSlotArea.Add(manager.draggedSlot);

            if (manager.highlightedSlot != null)
            {
                Vector2Int itemSize = (manager.draggedSlot != null && manager.draggedSlot.HasValidItem) ?
                              manager.draggedSlot.GetItemSize() :
                              new Vector2Int(1, 1);

                int startRow = manager.highlightedSlot.row;
                int startCol = manager.highlightedSlot.col;
                var targetSlots = manager.DataModel?.GetGridForSlot(manager.highlightedSlot) ?? (manager.highlightedSlot.IsInventorySlot ? manager.InventorySlots : manager.ChestSlots);
                if (targetSlots != null)
                {
                    for (int i = 0; i < itemSize.y; i++)
                    {
                        for (int j = 0; j < itemSize.x; j++)
                        {
                            int targetRow = startRow + i;
                            int targetCol = startCol + j;

                            if (manager._GridGenerator.Operations.IsSlotWithinBounds(targetRow, targetCol, targetSlots))
                                SetBackgroundColor(targetSlots[targetRow, targetCol], defaultBackgroundColor);
                        }
                    }
                }
            }

            SetSlotsColors(defaultBackgroundColor);
            manager.highlightedSlotArea.Clear();

            if (clearCurrentReferences) ClearDraggedHighlightedReferences();

            manager.CalculateTotalBullets(overrideCurrentBullets: false);
        }

        /// <summary>
        /// Called when beginning to drag a slot
        /// </summary>
        public void OnBeginDrag(InventorySlot selectedAnchorSlot, bool allowBeginDrag, bool isRightClick = false)
        {
            isClickGrabbed = false;
            isRightClickDrag = isRightClick && selectedAnchorSlot != null && selectedAnchorSlot.slotData != null && selectedAnchorSlot.slotData.amount > 1;
            depositedSlotsThisDrag.Clear();

            if (allowBeginDrag)
            {
                manager.draggedSlot = selectedAnchorSlot;
                HighlightSlot(selectedAnchorSlot);

                int initialRotation = 0;
                if (selectedAnchorSlot?.slotData != null)
                {
                    initialRotation = selectedAnchorSlot.slotData.rotationAngle;
                    if (initialRotation == 0 && !selectedAnchorSlot.slotData.isOriented)
                        initialRotation = 90;
                    dragStartRotationAngle = initialRotation;
                    dragStartIsOriented = initialRotation == 0 || initialRotation == 180;
                }
                manager.ItemRotation = initialRotation;

                var operations = manager._GridGenerator.Operations;
                operations.RotateItem(manager.ItemRotation);

                manager.UIController.StartDragVisuals(selectedAnchorSlot.slotData.item);
                SetAreaColorOnPointerEnter(manager.draggedSlot);
            }

            manager.Events.onSlotBeginDrag?.Invoke();
        }

        /// <summary>
        /// Called when hovering the given slot
        /// </summary>
        /// <param name="slot">InventorySlot to highlight</param>
        public void OnPointerEnter(InventorySlot slot)
        {
            HighlightSlot(slot);
            manager.ContextMenu.HideContextMenu();
            if (!manager.draggedSlot || !manager.draggedSlot.HasValidItem) return;

            bool isRmbHeld = UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.rightButton.isPressed;

            if ((isRightClickDrag || isRmbHeld) && manager.draggedSlot.slotData != null && manager.draggedSlot.slotData.amount >= 1 && manager.draggedSlot.slotData.item != null && manager.draggedSlot.slotData.item.maxStack > 1)
            {
                InventorySlot anchor = slot.GetAnchorSlot();
                InventorySlot draggedAnchor = manager.draggedSlot.GetAnchorSlot();

                if (anchor != draggedAnchor && !depositedSlotsThisDrag.Contains(anchor))
                {
                    bool deposited = manager.DepositSingleItem(draggedAnchor, slot);
                    if (deposited)
                    {
                        depositedSlotsThisDrag.Add(anchor);
                        isRightClickDrag = true;

                        if (manager.draggedSlot == null || manager.draggedSlot.slotData == null || manager.draggedSlot.slotData.amount <= 0)
                        {
                            manager.UIController.StopDragVisuals();
                            ResetHighlightedSlotsArea(slot.DefaultBackgroundColor, clearCurrentReferences: false);
                            return;
                        }
                    }
                }
            }

            SetAreaColorOnPointerEnter(slot);
        }

        /// <summary>
        /// Called when unhovering the given slot
        /// </summary>
        public void OnPointerExit(Color defaultBackgroundColor)
        {
            HighlightSlot(null);
            manager._TooltipManager.SetTooltipVisibility(false, "", "");
            SetSlotsColors(defaultBackgroundColor);
            manager.highlightedSlotArea = new System.Collections.Generic.List<InventorySlot>();
        }
    }
}
