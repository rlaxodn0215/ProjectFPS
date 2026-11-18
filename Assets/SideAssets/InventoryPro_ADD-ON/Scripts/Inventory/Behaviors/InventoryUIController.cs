using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using TMPro;

namespace cowsins.Inventory
{
    /// <summary>
    /// Handles the UI logic for the inventory, separately from the UIController, these are not related.
    /// </summary>
    public class InventoryUIController
    {
        private InventoryProManager manager;
        private GameObject examinationUI;
        private TextMeshProUGUI examinationText;

        private bool isInventoryOpen;
        private bool isFavRadialMenuOpen;

        public bool IsInventoryOpen => isInventoryOpen;
        public bool IsFavRadialMenuOpen => isFavRadialMenuOpen;

        public InventoryUIController(InventoryProManager manager, GameObject examinationUI, TextMeshProUGUI examinationText)
        {
            this.manager = manager;
            this.examinationUI = examinationUI;
            this.examinationText = examinationText;
        }

        public void ToggleInventoryVisibility()
        {
            if (PauseMenu.isPaused || manager.PlayerDependencies.GetComponent<PlayerStats>().IsDead) return;

            if (!isInventoryOpen && (!manager._InteractManager.Inspecting && manager._InteractManager.RealtimeAttachmentCustomization || !manager._InteractManager.RealtimeAttachmentCustomization)) 
                OpenInventory();
            else 
                CloseInventory();
        }

        public void OpenInventory()
        {
            if (isFavRadialMenuOpen) CloseFavRadialMenu();
            isInventoryOpen = true;

            InputManager.ToggleUIControls(true);
            manager.PlayerDependencies.GetComponent<PlayerControl>().LoseControl();
            manager.PlayerDependencies.GetComponent<PlayerMovement>().speedLinesBehaviour.StopSpeedlines();
            manager.PlayerDependencies.UIManager?.UnlockMouse();
            manager.PlayerDependencies.Crosshair?.SetVisibility(false);
            manager.ContextMenu.HideContextMenu();

            manager._GridGenerator.gameObject.SetActive(true);
            manager.InputHandler?.ResetSelection();

            if (EventSystem.current != null && Gamepad.current != null && manager.InventorySlots != null) 
                EventSystem.current.SetSelectedGameObject(manager.InventorySlots[0, 0].gameObject);

            manager.draggedSlot = null;
            manager.NavigationBehaviour.HighlightSlot(null);

            if (manager._GridGenerator?.BackpackBehaviour?.EquippedBackpackSlot != null && !manager._GridGenerator.isBackpackOpen)
                manager._GridGenerator.BackpackBehaviour.OpenBackpack(manager._GridGenerator.BackpackBehaviour.EquippedBackpackSlot);

            manager.Events.onOpenInventory?.Invoke();
            SoundManager.Instance.PlaySound(manager.OpenInventorySFX, 0, 0, false);
        }

        public void CloseInventory() => CloseInventory(true);

        public void CloseInventory(bool playSFX)
        {
            isInventoryOpen = false;
            manager._GridGenerator.gameObject.SetActive(false);

            manager.InputManager.Dropping = false;
            manager.InputManager.Crouching = false;
            manager.InputManager.Shooting = false;
            manager.PlayerDependencies.GetComponent<CameraEffects>().Trauma = 0;

            manager.PlayerDependencies.GetComponent<PlayerControl>().CheckIfCanGrantControl();
            manager.PlayerDependencies.UIManager?.LockMouse();
            manager.PlayerDependencies.Crosshair?.SetVisibility(true);

            manager._TooltipManager.Disable();

            manager._GridGenerator.ChestBehaviour?.CloseChest();
            manager._GridGenerator.BackpackBehaviour?.CloseBackpack();
            InputManager.ToggleGameControls(true);
            InputManager.ToggleUIControls(false);

            manager.NavigationBehaviour?.CancelClickGrab();
            manager.draggedSlot = null;
            manager.NavigationBehaviour?.HighlightSlot(null);

            manager.ContextMenu.HideContextMenu();

            manager.Events.onCloseInventory?.Invoke();
            if (playSFX) SoundManager.Instance.PlaySound(manager.CloseInventorySFX, 0, 0, false);
        }

        public void ToggleFavRadialMenu()
        {
            if (PauseMenu.isPaused || manager.PlayerDependencies.GetComponent<PlayerStats>().IsDead || manager._FavItemsMenu == null) return;

            if (isInventoryOpen) CloseInventory();

            if (!isFavRadialMenuOpen && (!manager._InteractManager.Inspecting && manager._InteractManager.RealtimeAttachmentCustomization || !manager._InteractManager.RealtimeAttachmentCustomization)) 
                OpenFavRadialMenu();
            else 
                CloseFavRadialMenu();
        }

        public void OpenFavRadialMenu()
        {
            if (manager._FavItemsMenu == null) return;

            isFavRadialMenuOpen = true;
            manager._FavItemsMenu.gameObject.SetActive(true);

            InputManager.ToggleUIControls(true);
            manager.PlayerDependencies.GetComponent<PlayerControl>().LoseControl();
            manager.PlayerDependencies.UIManager?.UnlockMouse();
            manager.PlayerDependencies.Crosshair?.SetVisibility(false);
            manager.ContextMenu.HideContextMenu();

            manager._FavItemsMenu.selectedControllerSlotIndex = 0;
            if (manager._FavItemsMenu.SelectedSlot != null) 
                EventSystem.current.SetSelectedGameObject(manager._FavItemsMenu.SelectedSlot.gameObject);
        }

        public void CloseFavRadialMenu()
        {
            if (manager._FavItemsMenu == null) return;

            isFavRadialMenuOpen = false;
            manager._FavItemsMenu.gameObject.SetActive(false);

            manager.PlayerDependencies.GetComponent<PlayerControl>().CheckIfCanGrantControl();
            manager.PlayerDependencies.UIManager?.LockMouse();
            manager.PlayerDependencies.Crosshair?.SetVisibility(true);
        }

        public void ToggleDeviceSpecificUI(bool isGamepad)
        {
            if (manager.CloseButton) manager.CloseButton.gameObject.SetActive(!isGamepad);
            if (manager.TrashButton) manager.TrashButton.gameObject.SetActive(!isGamepad);
            manager.GamepadSpecificUI?.SetActive(isGamepad);
        }

        public void UpdateSlotAndSelection(InventorySlot newSlot)
        {
            manager.highlightedSlot?.OnPointerExit(null);
            manager.NavigationBehaviour.HighlightSlot(newSlot);
            manager.highlightedSlot.OnPointerEnter(null);
            manager._TooltipManager.SetPosition(manager.highlightedSlot.transform.position);
            EventSystem.current.SetSelectedGameObject(manager.highlightedSlot.gameObject);
        }

        /// <summary>
        /// Enables or Disables the Examination UI
        /// </summary>
        /// <param name="visible">This boolean controls the visibility state of the Examination UI</param>
        /// <param name="text">Name of the examined Item</param>
        public void SetExaminationUIVisibility(bool visible, string text)
        {
            examinationText.text = text + "\n\nE - Cancel \nF - Interact";
            examinationUI.SetActive(visible);
        }

        /// <summary>
        /// Enables Drag Icon and disables tooltip when we start to drag an Item
        /// </summary>
        public void StartDragVisuals(Item_SO item)
        {
            if (item == null)
            {
                StopDragVisuals();
                return;
            }

            manager._TooltipManager.SetDragImageVisibility(true, item.icon, manager._InventoryStyle == InventoryProManager.InventoryStyle.Tetris && DeviceDetection.Instance.mode == DeviceDetection.InputMode.Keyboard);
            manager._TooltipManager.SetTooltipVisibility(false, "", "");
        }

        /// Disables Drag Icon and hides the drag ghost
        public void StopDragVisuals() => manager._TooltipManager.SetDragImageVisibility(false, null, false);

#if UNITY_EDITOR
        /// <summary>
        /// Sets the right click event behavior for inventory slots
        /// </summary>
        public void SetRightClickEvent(InventoryProManager.SlotRightClickEvent slotRightClickEvent)
        {
            var field = manager.GetType().GetField("slotRightClickEvent", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(manager, slotRightClickEvent);
        }
#endif
    }
}
