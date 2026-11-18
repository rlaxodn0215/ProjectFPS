using UnityEngine;
using UnityEngine.InputSystem;

namespace cowsins.Inventory
{
    /// <summary>
    /// InventoryProManager has a specific set of Actions. To avoid modifying base PlayerActions file, this class stores all the different Actions, and keybinds can be assigned
    /// in the InventoryProManager component.
    /// </summary>
    [System.Serializable]   
    public class InventoryProManagerInputActions
    {
        public InputAction closeAction;
        public InputAction navigateLeftAction;
        public InputAction navigateRightAction;
        public InputAction navigateUpAction;
        public InputAction navigateDownAction;
        public InputAction selectAction;
        public InputAction dropAction;
        public InputAction pinAction;
        public InputAction navigateRadialAction;
        public InputAction rotateItemAction;
        public InputAction rotateItemLeftAction;
        public InputAction rotateItemRightAction;

        [HideInInspector] public InputAction anyKeyAction;
        [HideInInspector] public InputAction mouseLeftClickAction;
        [HideInInspector] public InputAction mouseMoveAction;
        [HideInInspector] public InputAction[] hotbarNumberActions;

        /// <summary>
        /// Enables each Action when opening the InventoryProManager
        /// </summary>
        public void Enable()
        {
            closeAction.Enable();
            navigateLeftAction.Enable();
            navigateRightAction.Enable();
            navigateUpAction.Enable();
            navigateDownAction.Enable();
            selectAction.Enable();
            dropAction.Enable();
            pinAction.Enable();
            anyKeyAction.Enable();
            mouseLeftClickAction.Enable();
            mouseMoveAction.Enable();
            navigateRadialAction.Enable();
            rotateItemAction.Enable();
            rotateItemLeftAction.Enable();
            rotateItemRightAction.Enable();
            if (hotbarNumberActions != null)
            {
                for (int i = 0; i < hotbarNumberActions.Length; i++)
                    hotbarNumberActions[i]?.Enable();
            }
        }

        /// <summary>
        /// Disables each Action when closing the InventoryProManager
        /// </summary>
        public void Disable()
        {
            closeAction.Disable();
            navigateLeftAction.Disable();
            navigateRightAction.Disable();
            navigateUpAction.Disable();
            navigateDownAction.Disable();
            selectAction.Disable();
            dropAction.Disable();
            pinAction.Disable();
            anyKeyAction.Disable();
            mouseLeftClickAction.Disable();
            mouseMoveAction.Disable();
            navigateRadialAction.Disable();
            rotateItemAction.Disable();
            rotateItemLeftAction.Disable();
            rotateItemRightAction.Disable();
            if (hotbarNumberActions != null)
            {
                for (int i = 0; i < hotbarNumberActions.Length; i++)
                    hotbarNumberActions[i]?.Disable();
            }
        }
    }
}