using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

namespace cowsins.Inventory
{
    public class InventoryInputHandler
    {
        private InventoryProManager manager;
        private InventoryProManagerInputActions invInputActions;

        public int SelectedRow { get; set; } = 0;
        public int SelectedColumn { get; set; } = 0;

        public InventoryInputHandler(InventoryProManager manager, InventoryProManagerInputActions invInputActions)
        {
            this.manager = manager;
            this.invInputActions = invInputActions;
        }

        public void ResetSelection()
        {
            SelectedRow = 0;
            SelectedColumn = 0;
        }

        public void InitializeControllerNavigation()
        {
            // Radial Only Actions
            if (manager.UseFavouritesRadialMenu)
            {
                invInputActions.navigateRadialAction.performed += ctx => manager._FavItemsMenu?.RadialNavigation(ctx);
                invInputActions.selectAction.performed += ctx => {
                    // Simulates Left Click for Controllers
                    var pointerData = new PointerEventData(EventSystem.current) 
                    {
                        button = PointerEventData.InputButton.Left
                    };

                    manager._FavItemsMenu?.SelectedSlot?.ClickAction(pointerData);
                };
                invInputActions.navigateRadialAction.Enable();
            }
            invInputActions.navigateLeftAction.performed += ctx => NavigateToSlot(0, -1);
            invInputActions.navigateRightAction.performed += ctx => NavigateToSlot(0, 1);
            invInputActions.navigateUpAction.performed += ctx => NavigateToSlot(-1, 0);
            invInputActions.navigateDownAction.performed += ctx => NavigateToSlot(1, 0);
            invInputActions.selectAction.performed += ctx => manager.NavigationBehaviour.SelectSlotController();
            invInputActions.rotateItemAction.performed += ctx =>
            {
                if (!manager.IsInventoryOpen) return;
                manager._GridGenerator?.Operations?.RotateItem(manager.ItemRotation + 90);
            };
            invInputActions.rotateItemLeftAction.performed += ctx =>
            {
                if (!manager.IsInventoryOpen) return;
                manager._GridGenerator?.Operations?.RotateItem(manager.ItemRotation - 90);
            };
            invInputActions.rotateItemRightAction.performed += ctx =>
            {
                if (!manager.IsInventoryOpen) return;
                manager._GridGenerator?.Operations?.RotateItem(manager.ItemRotation + 90);
            };

            // Shared Actions
            invInputActions.closeAction.performed += ctx => 
            {
                if (manager.IsInventoryOpen) manager.UIController.CloseInventory();
            };
            invInputActions.dropAction.performed += ctx =>
            {
                if (!manager.IsInventoryOpen) return;
                InventorySlot slot = manager.highlightedSlot;
                if (slot == null) return;
                InventorySlot anchor = slot.GetAnchorSlot();
                if (anchor != null && anchor.HasValidItem)
                {
                    manager._GridGenerator?.Operations?.DropOutsideInventory(anchor);
                }
            };
            invInputActions.pinAction.performed += ctx =>
            {
                if (!manager.IsInventoryOpen || !manager.UseFavouritesRadialMenu || manager._FavItemsMenu == null) return;
                // When an item is currently dragged or click-grabbed, North button is dedicated to rotating the held item
                if (manager.draggedSlot != null || (manager.NavigationBehaviour != null && manager.NavigationBehaviour.IsClickGrabbed)) return;

                InventorySlot slot = manager.highlightedSlot;
                if (slot == null) return;
                InventorySlot anchor = slot.GetAnchorSlot();
                if (anchor != null && anchor.HasValidItem)
                {
                    manager._FavItemsMenu.PinItem(anchor);
                }
            };
            invInputActions.anyKeyAction = new InputAction(type: InputActionType.PassThrough, binding: "<Keyboard>/anyKey");
            invInputActions.anyKeyAction.performed += ctx => manager.UIController.ToggleDeviceSpecificUI(false);
            invInputActions.mouseLeftClickAction = new InputAction(type: InputActionType.Button, binding: "<Mouse>/press");
            invInputActions.mouseLeftClickAction.performed += ctx =>
            {
                manager.UIController.ToggleDeviceSpecificUI(false);
                if (manager.IsInventoryOpen && manager.NavigationBehaviour != null && manager.NavigationBehaviour.IsClickGrabbed && manager.draggedSlot != null)
                {
                    if (manager.highlightedSlot == null)
                    {
                        manager.NavigationBehaviour.IsClickGrabbed = false;
                        manager.Operations.OnEndDrag(true);
                    }
                }
            };
            invInputActions.mouseMoveAction = new InputAction(type: InputActionType.Value, binding: "<Mouse>/delta");
            invInputActions.mouseMoveAction.performed += ctx => manager.UIController.ToggleDeviceSpecificUI(false);

            invInputActions.hotbarNumberActions = new InputAction[10];
            for (int i = 0; i < 10; i++)
            {
                int slotIndex = i;
                int digit = (i + 1) % 10;
                var action = new InputAction(name: $"HotbarKey{digit}", type: InputActionType.Button);
                action.AddBinding($"<Keyboard>/{digit}");
                action.AddBinding($"<Keyboard>/numpad{digit}");
                action.performed += ctx =>
                {
                    if (!manager.IsInventoryOpen) return;
                    if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null)
                    {
                        var go = EventSystem.current.currentSelectedGameObject;
                        if (go.GetComponent<TMPro.TMP_InputField>() != null || go.GetComponent<UnityEngine.UI.InputField>() != null)
                            return;
                    }
                    manager._GridGenerator?.Operations?.HandleHotbarNumberKey(slotIndex);
                };
                invInputActions.hotbarNumberActions[i] = action;
            }

            invInputActions.Enable();
        }

        public void NavigateToSlot(int rowChange, int colChange)
        {
            if (!manager._GridGenerator.gameObject.activeSelf) return;

            // Enable Gamepad specific UI since we are using a Gamepad right now.
            manager.UIController.ToggleDeviceSpecificUI(true);

            // If no slot is highlighted yet, default to the first inventory slot.
            if (manager.highlightedSlot == null) manager.UIController.UpdateSlotAndSelection(manager.InventorySlots[0, 0]);

            switch (manager.highlightedSlot.Policy.SlotType)
            {
                case SlotType.Hotbar:
                    NavigateFromHotbar(rowChange, colChange);
                    break;
                case SlotType.Inventory:
                    NavigateFromInventory(rowChange, colChange);
                    break;
                case SlotType.Chest:
                    NavigateFromChest(rowChange, colChange);
                    break;
                case SlotType.Custom:
                    NavigateFromCustomSlot(rowChange, colChange);
                    break;
                case SlotType.Backpack:
                    NavigateFromBackpack(rowChange, colChange);
                    break;
            }
        }

        private void NavigateFromHotbar(int rowChange, int colChange)
        {
            Vector2 screenDir = new Vector2(colChange, rowChange).normalized;

            if (manager.CustomSlots != null && manager.CustomSlots.Count > 0)
            {
                InventorySlot customCandidate = FindClosestSlotInDirection(manager.highlightedSlot, screenDir, includeCustom: true, includeGrid: false);
                if (customCandidate != null && colChange != 0)
                {
                    SelectSlot(customCandidate);
                    return;
                }
            }

            if (rowChange > 0)
            {
                SelectedRow = 0;
                manager.UIController.UpdateSlotAndSelection(manager.InventorySlots[SelectedRow, SelectedColumn]);
                return;
            }
            else if (rowChange < 0)
            {
                SelectedRow = manager._GridGenerator.Rows - 1;
                manager.UIController.UpdateSlotAndSelection(manager.InventorySlots[SelectedRow, SelectedColumn]);
                return;
            }

            int newHotbarIndex = SelectedColumn + colChange;
            if (newHotbarIndex >= manager.HotbarSlots.Length) newHotbarIndex = 0;
            else if (newHotbarIndex < 0) newHotbarIndex = manager.HotbarSlots.Length - 1;

            SelectedColumn = newHotbarIndex;
            manager.UIController.UpdateSlotAndSelection(manager.HotbarSlots[newHotbarIndex]);
        }

        private void NavigateFromInventory(int rowChange, int colChange)
        {
            Vector2 screenDir = new Vector2(colChange, rowChange).normalized;
            int newRow = SelectedRow + rowChange;
            int newColumn = SelectedColumn + colChange;

            bool outOfBounds = newRow < 0 || newRow >= manager._GridGenerator.Rows || newColumn < 0 || newColumn >= manager._GridGenerator.Columns;
            if (outOfBounds && manager.CustomSlots != null && manager.CustomSlots.Count > 0)
            {
                InventorySlot customCandidate = FindClosestSlotInDirection(manager.highlightedSlot, screenDir, includeCustom: true, includeGrid: false);
                if (customCandidate != null)
                {
                    SelectSlot(customCandidate);
                    return;
                }
            }

            if (newRow < 0 || newRow >= manager._GridGenerator.Rows)
            {
                SelectedRow = -1;
                manager.UIController.UpdateSlotAndSelection(manager.HotbarSlots[0]);
                return;
            }

            if (newColumn >= manager._GridGenerator.Columns)
            {
                newColumn = 0;
            }
            else if (newColumn < 0)
            {
                bool hasChest = manager.isChestOpen && manager.ChestSlots?.Length > 0;
                bool hasBackpack = manager.isBackpackOpen && manager.BackpackSlots?.Length > 0;

                if (hasChest && hasBackpack)
                {
                    int chestRows = manager._GridGenerator.CachedChest.ChestSize.x;
                    int chestCols = manager._GridGenerator.CachedChest.ChestSize.y;
                    if (newRow < chestRows)
                    {
                        newColumn = chestCols - 1;
                        SelectedRow = newRow;
                        SelectedColumn = newColumn;
                        manager.UIController.UpdateSlotAndSelection(manager.ChestSlots[newRow, newColumn]);
                        return;
                    }
                    else
                    {
                        int bpRows = manager.BackpackSlots.GetLength(0);
                        int bpCols = manager.BackpackSlots.GetLength(1);
                        int targetBpRow = Mathf.Clamp(newRow - chestRows, 0, bpRows - 1);
                        newColumn = bpCols - 1;
                        SelectedRow = targetBpRow;
                        SelectedColumn = newColumn;
                        manager.UIController.UpdateSlotAndSelection(manager.BackpackSlots[targetBpRow, newColumn]);
                        return;
                    }
                }
                else if (hasChest)
                {
                    newColumn = manager._GridGenerator.CachedChest.ChestSize.y - 1;
                    if (newRow > manager._GridGenerator.CachedChest.ChestSize.x - 1)
                        newRow = manager._GridGenerator.CachedChest.ChestSize.x - 1;

                    SelectedRow = newRow;
                    SelectedColumn = newColumn;
                    manager.UIController.UpdateSlotAndSelection(manager.ChestSlots[newRow, newColumn]);
                    return;
                }
                else if (hasBackpack)
                {
                    int bpRows = manager.BackpackSlots.GetLength(0);
                    int bpCols = manager.BackpackSlots.GetLength(1);
                    newColumn = bpCols - 1;
                    if (newRow > bpRows - 1) newRow = bpRows - 1;

                    SelectedRow = newRow;
                    SelectedColumn = newColumn;
                    manager.UIController.UpdateSlotAndSelection(manager.BackpackSlots[newRow, newColumn]);
                    return;
                }
                else
                {
                    newColumn = manager._GridGenerator.Columns - 1;
                }
            }

            SelectedRow = newRow;
            SelectedColumn = newColumn;
            manager.UIController.UpdateSlotAndSelection(manager.InventorySlots[newRow, newColumn]);
        }

        private void NavigateFromChest(int rowChange, int colChange)
        {
            Vector2 screenDir = new Vector2(colChange, rowChange).normalized;
            int newRow = SelectedRow + rowChange;
            int newColumn = SelectedColumn + colChange;

            bool outOfBounds = newRow < 0 || newRow >= manager._GridGenerator.CachedChest.ChestSize.x || newColumn < 0 || newColumn >= manager._GridGenerator.CachedChest.ChestSize.y;
            if (outOfBounds && manager.CustomSlots != null && manager.CustomSlots.Count > 0)
            {
                InventorySlot customCandidate = FindClosestSlotInDirection(manager.highlightedSlot, screenDir, includeCustom: true, includeGrid: false);
                if (customCandidate != null)
                {
                    SelectSlot(customCandidate);
                    return;
                }
            }

            if (rowChange > 0 && newRow >= manager._GridGenerator.CachedChest.ChestSize.x)
            {
                if (manager.isBackpackOpen && manager.BackpackSlots != null && manager.BackpackSlots.Length > 0)
                {
                    int bpCols = manager.BackpackSlots.GetLength(1);
                    int targetCol = Mathf.Clamp(SelectedColumn, 0, bpCols - 1);
                    SelectedRow = 0;
                    SelectedColumn = targetCol;
                    manager.UIController.UpdateSlotAndSelection(manager.BackpackSlots[0, targetCol]);
                    return;
                }
                newRow = 0;
            }
            else if (newRow > manager._GridGenerator.CachedChest.ChestSize.x - 1)
            {
                newRow = 0;
            }

            if (newColumn >= manager._GridGenerator.CachedChest.ChestSize.y)
            {
                newColumn = 0;
                SelectedRow = newRow;
                SelectedColumn = newColumn;
                manager.UIController.UpdateSlotAndSelection(manager.InventorySlots[newRow, newColumn]);
                return;
            }
            else if (newColumn < 0)
            {
                newColumn = manager._GridGenerator.Columns - 1;
                SelectedRow = newRow;
                SelectedColumn = newColumn;
                manager.UIController.UpdateSlotAndSelection(manager.InventorySlots[newRow, newColumn]);
                return;
            }

            SelectedRow = newRow;
            SelectedColumn = newColumn;
            manager.UIController.UpdateSlotAndSelection(manager.ChestSlots[newRow, newColumn]);
        }

        private void NavigateFromBackpack(int rowChange, int colChange)
        {
            var backpackSlots = manager.BackpackSlots;
            if (backpackSlots == null || backpackSlots.Length == 0) return;

            int bpRows = backpackSlots.GetLength(0);
            int bpCols = backpackSlots.GetLength(1);

            Vector2 screenDir = new Vector2(colChange, rowChange).normalized;
            int newRow = SelectedRow + rowChange;
            int newColumn = SelectedColumn + colChange;

            bool outOfBounds = newRow < 0 || newRow >= bpRows || newColumn < 0 || newColumn >= bpCols;
            if (outOfBounds && manager.CustomSlots != null && manager.CustomSlots.Count > 0)
            {
                InventorySlot customCandidate = FindClosestSlotInDirection(manager.highlightedSlot, screenDir, includeCustom: true, includeGrid: false);
                if (customCandidate != null)
                {
                    SelectSlot(customCandidate);
                    return;
                }
            }

            if (rowChange < 0 && newRow < 0)
            {
                if (manager.isChestOpen && manager.ChestSlots != null && manager.ChestSlots.Length > 0)
                {
                    int chestRows = manager._GridGenerator.CachedChest.ChestSize.x;
                    int chestCols = manager._GridGenerator.CachedChest.ChestSize.y;
                    int targetCol = Mathf.Clamp(SelectedColumn, 0, chestCols - 1);
                    SelectedRow = chestRows - 1;
                    SelectedColumn = targetCol;
                    manager.UIController.UpdateSlotAndSelection(manager.ChestSlots[chestRows - 1, targetCol]);
                    return;
                }
                newRow = bpRows - 1;
            }
            else if (newRow > bpRows - 1)
            {
                newRow = 0;
            }
            else if (newRow < 0)
            {
                newRow = bpRows - 1;
            }

            if (newColumn >= bpCols)
            {
                newColumn = 0;
                SelectedRow = Mathf.Clamp(newRow, 0, manager._GridGenerator.Rows - 1);
                SelectedColumn = newColumn;
                manager.UIController.UpdateSlotAndSelection(manager.InventorySlots[SelectedRow, SelectedColumn]);
                return;
            }
            else if (newColumn < 0)
            {
                newColumn = manager._GridGenerator.Columns - 1;
                SelectedRow = Mathf.Clamp(newRow, 0, manager._GridGenerator.Rows - 1);
                SelectedColumn = newColumn;
                manager.UIController.UpdateSlotAndSelection(manager.InventorySlots[SelectedRow, SelectedColumn]);
                return;
            }

            SelectedRow = newRow;
            SelectedColumn = newColumn;
            manager.UIController.UpdateSlotAndSelection(backpackSlots[newRow, newColumn]);
        }

        private void NavigateFromCustomSlot(int rowChange, int colChange)
        {
            Vector2 screenDir = new Vector2(colChange, rowChange).normalized;
            InventorySlot closest = FindClosestSlotInDirection(manager.highlightedSlot, screenDir, includeCustom: true, includeGrid: true);
            if (closest != null) SelectSlot(closest);
        }

        private void SelectSlot(InventorySlot targetSlot)
        {
            if (targetSlot == null) return;

            if (targetSlot.Policy.IsGridBased)
            {
                SelectedRow = targetSlot.row;
                SelectedColumn = targetSlot.col;
            }
            else if (targetSlot.Policy.SlotType == SlotType.Hotbar)
            {
                SelectedRow = -1;
                SelectedColumn = targetSlot.col;
            }
            else
            {
                SelectedRow = -1;
                SelectedColumn = -1;
            }

            manager.UIController.UpdateSlotAndSelection(targetSlot);
        }

        private InventorySlot FindClosestSlotInDirection(InventorySlot currentSlot, Vector2 screenDir, bool includeCustom = true, bool includeGrid = true)
        {
            if (currentSlot == null || screenDir == Vector2.zero) return null;

            Vector3 currentPos = currentSlot.transform.position;
            InventorySlot bestSlot = null;
            float bestScore = float.MaxValue;

            if (includeCustom && manager.CustomSlots != null)
            {
                for (int i = 0; i < manager.CustomSlots.Count; i++)
                {
                    EvaluateCandidate(manager.CustomSlots[i], currentSlot, currentPos, screenDir, ref bestSlot, ref bestScore);
                }
            }

            if (includeGrid && manager.HotbarSlots != null)
            {
                for (int i = 0; i < manager.HotbarSlots.Length; i++)
                {
                    EvaluateCandidate(manager.HotbarSlots[i], currentSlot, currentPos, screenDir, ref bestSlot, ref bestScore);
                }
            }

            if (includeGrid && manager.InventorySlots != null)
            {
                int rows = manager._GridGenerator.Rows;
                int cols = manager._GridGenerator.Columns;
                for (int r = 0; r < rows; r++)
                {
                    for (int c = 0; c < cols; c++)
                    {
                        EvaluateCandidate(manager.InventorySlots[r, c], currentSlot, currentPos, screenDir, ref bestSlot, ref bestScore);
                    }
                }
            }

            if (includeGrid && manager.ChestSlots != null && manager._GridGenerator.CachedChest != null)
            {
                int rows = manager._GridGenerator.CachedChest.ChestSize.x;
                int cols = manager._GridGenerator.CachedChest.ChestSize.y;
                for (int r = 0; r < rows; r++)
                {
                    for (int c = 0; c < cols; c++)
                    {
                        EvaluateCandidate(manager.ChestSlots[r, c], currentSlot, currentPos, screenDir, ref bestSlot, ref bestScore);
                    }
                }
            }

            if (includeGrid && manager.BackpackSlots != null && manager.isBackpackOpen)
            {
                int bpRows = manager.BackpackSlots.GetLength(0);
                int bpCols = manager.BackpackSlots.GetLength(1);
                for (int r = 0; r < bpRows; r++)
                {
                    for (int c = 0; c < bpCols; c++)
                    {
                        EvaluateCandidate(manager.BackpackSlots[r, c], currentSlot, currentPos, screenDir, ref bestSlot, ref bestScore);
                    }
                }
            }

            return bestSlot;
        }

        private void EvaluateCandidate(InventorySlot candidate, InventorySlot currentSlot, Vector3 currentPos, Vector2 screenDir, ref InventorySlot bestSlot, ref float bestScore)
        {
            if (candidate == null || candidate == currentSlot || !candidate.gameObject.activeInHierarchy) return;

            Vector3 diff = candidate.transform.position - currentPos;
            Vector2 diff2D = new Vector2(diff.x, diff.y);
            float sqrDist = diff2D.sqrMagnitude;
            if (sqrDist < 0.001f) return;

            float dot = Vector2.Dot(diff2D.normalized, screenDir);
            if (dot <= 0.25f) return;

            float score = sqrDist / (dot * dot * dot);
            if (score < bestScore)
            {
                bestScore = score;
                bestSlot = candidate;
            }
        }
    }
}
