using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using System.Linq; 

namespace cowsins.Inventory
{
    /// <summary>
    /// Used by FavItemSlots as a reference to a slot in the Inventory
    /// </summary>
    [System.Serializable]
    public class FavItemReference
    {
        public int row, col;
        public Item_SO item;
        // Since we can also pin Hotbar slots. Chest Slots cannot be pinned
        public bool isInventorySlot;

        // Default Constructor
        public FavItemReference()
        {
            this.row = 0;
            this.col = 0;
            this.item = null;
            this.isInventorySlot = true;
        }

        // Recommended Constructor
        public FavItemReference(int row, int col, Item_SO item, bool isInventorySlot)
        {
            this.row = row;
            this.col = col;
            this.item = item;
            this.isInventorySlot = isInventorySlot;
        }
    }

    public class FavItemsMenu : MonoBehaviour
    {
        [SerializeField, Range(1, 15), Tooltip("Amount of items that can be pinned to favorite")] private int radialMenuSize = 8;
        [SerializeField, Tooltip("Button to instantiate at the radial menu.")] private GameObject buttonPrefab;
        [SerializeField, Tooltip("Defines the size of the Menu in the UI")] private float menuRadius = 150;
        [SerializeField, Tooltip("When hovering an Item, this text displays its Name")] private TextMeshProUGUI highlightText;
        [SerializeField, Tooltip("When hovering an Item, this Image displays its Icon")] private Image highlightIcon;

        [HideInInspector] public int selectedControllerSlotIndex = 0;
        [HideInInspector] public List<FavItemReference> favoritesList;

        // GETTERS
        public int RadialMenuSize => radialMenuSize;
        public FavItemSlot SelectedSlot => inventorySlots[selectedControllerSlotIndex];

        private InventoryProManager inventoryProManager;

        private WeaponController weaponController;

        private FavItemSlot[] inventorySlots;

        public FavItemSlot[] InventorySlots => inventorySlots;

        private void Awake()
        {
            // Avoid highlighted items when starting
            highlightText.gameObject.SetActive(false);
            highlightIcon.gameObject.SetActive(false);
            CreateMenu();
        }

        // Used by InventoryProManager to initialize the required references
        public void Initialize(InventoryProManager inventoryProManager, WeaponController weaponController)
        {
            this.inventoryProManager = inventoryProManager;
            this.weaponController = weaponController;
        }

        /// <summary>
        /// Generates and Populates the Radial Menu
        /// </summary>
        public void CreateMenu()
        {
            favoritesList = new List<FavItemReference>();

            // Creates Default Fav list & Populates it with blank References
            for (int i = 0; i < radialMenuSize; i++)
            {
                favoritesList.Add(new FavItemReference());
            }

            inventorySlots = new FavItemSlot[radialMenuSize];
            float startAngle = 90f;
            // Based on the amount of buttons to instantiate, step defines the distance between them
            float angleStep = 360f / radialMenuSize;

            // Create & Initialize Buttons
            for (int i = 0; i < radialMenuSize; i++)
            {
                GameObject newButton = Instantiate(buttonPrefab, transform);

                float angle = (startAngle + (i * angleStep)) * Mathf.Deg2Rad;
                float x = Mathf.Cos(angle) * menuRadius;
                float y = Mathf.Sin(angle) * menuRadius;

                FavItemSlot inventorySlot = newButton.GetComponent<FavItemSlot>();
                newButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(x, y);

                inventorySlot.Initialize(i, favoritesList[i], this, inventoryProManager, weaponController); 
                inventorySlots[i] = inventorySlot;
            }
        }

        /// <summary>
        /// Gamepad Navigation.
        /// Selects or Deselects slots based on the Joystick Direction
        /// </summary>
        /// <param name="context"></param>
        public void RadialNavigation(InputAction.CallbackContext context)
        {
            int selectedSlot = selectedControllerSlotIndex;
            Vector2 stickInput = context.ReadValue<Vector2>();

            if (stickInput.magnitude > 0.5f)
            {
                float angle = Mathf.Atan2(stickInput.y, stickInput.x) * Mathf.Rad2Deg;
                if (angle < 0) angle += 360;

                float startAngle = 90f;
                float angleStep = 360f / inventorySlots.Length;
                int newSlot = Mathf.RoundToInt((angle - startAngle) / angleStep);

                newSlot = (newSlot + radialMenuSize) % radialMenuSize;

                if (newSlot != selectedSlot)
                {
                    // Deselect previous slot
                    if (selectedSlot >= 0)
                        inventorySlots[selectedSlot].Deselect();

                    // Select the new slot
                    inventorySlots[newSlot].Select();

                    // Update the selected slot tracker
                    selectedControllerSlotIndex = newSlot;
                }
            }
        }

        public bool IsPinned(InventorySlot slot)
        {
            if (slot == null) return false;
            InventorySlot anchor = slot.GetAnchorSlot();
            if (anchor == null || !anchor.HasValidItem || anchor.slotData?.item == null) return false;
            FavItemReference favItemReference = new FavItemReference(anchor.row, anchor.col, anchor.slotData.item, anchor.IsInventorySlot);
            return ContainsItemReferenceInPinned(favItemReference).Item1;
        }

        public bool PinItem(InventorySlot slot)
        {
            if (slot == null) return false;

            InventorySlot anchor = slot.GetAnchorSlot();
            if (anchor == null || !anchor.HasValidItem || anchor.slotData == null || anchor.slotData.item == null) return false;

            FavItemReference favItemReference = new FavItemReference(
                anchor.row, anchor.col, anchor.slotData.item, anchor.IsInventorySlot);

            if (ContainsItemReferenceInPinned(favItemReference).Item1)
            {
                if (ToastManager.Instance != null && !string.IsNullOrEmpty(ToastManager.Instance.AlreadyPinnedToastMsg))
                    ToastManager.Instance.ShowToast(ToastManager.Instance.AlreadyPinnedToastMsg);
                return false;
            }

            bool added = AddItemReferenceToPinned(favItemReference);
            if (ToastManager.Instance != null)
            {
                string msg = !added
                    ? ToastManager.Instance.FavPinnedMenuIsFullMsg
                    : $"{favItemReference.item._name} {ToastManager.Instance.ItemPinnedToastMsg}";
                if (!string.IsNullOrEmpty(msg))
                    ToastManager.Instance.ShowToast(msg);
            }
            return added;
        }

        /// <summary>
        /// Check if any existing FavItemReference in the already pinned favorites matches the passed favItemReference
        /// </summary>
        /// <param name="favItemReference"></param>
        /// <returns></returns>
        public (bool, FavItemReference) ContainsItemReferenceInPinned(FavItemReference favItemReference)
        {
            foreach (var existingFav in favoritesList)
            {
                if (existingFav.row == favItemReference.row &&
                    existingFav.col == favItemReference.col &&
                    existingFav.item == favItemReference.item &&
                    existingFav.isInventorySlot == favItemReference.isInventorySlot)
                {
                    // Item already exists in the pinned favorites
                    return (true, existingFav);
                }
            }
            // Item does not exist in the favorites
            return (false, null);
        }


        public bool AddItemReferenceToPinned(FavItemReference favItemReference)
        {
            // Try to find an empty slot in favoritesList
            for (int i = 0; i < favoritesList.Count; i++)
            {
                if (favoritesList[i].item == null)
                {
                    // Assign the new item reference to the empty slot
                    favoritesList[i] = favItemReference;
                    inventorySlots[i].Initialize(i, favItemReference, this, inventoryProManager, weaponController);
                    return true;
                }
            }

            // No empty slot found, return false
            return false;
        }

        // On consuming an Item, update the Fav Items Menu
        public void UpdatePinnedFavorites(InventorySlot draggedAnchor, InventorySlot targetAnchor)
        {
            if (draggedAnchor == null || targetAnchor == null) return;
            // Find the favorite items in the list
            FavItemReference draggedFav = favoritesList.FirstOrDefault(f => f.row == draggedAnchor.row && f.col == draggedAnchor.col);
            FavItemReference targetFav = favoritesList.FirstOrDefault(f => f.row == targetAnchor.row && f.col == targetAnchor.col);

            // Handle both cases ( Dragged & Target )
            HandlePinnedItemUpdate(draggedFav, draggedAnchor, targetAnchor);
            HandlePinnedItemUpdate(targetFav, targetAnchor, draggedAnchor);
        }

        private void HandlePinnedItemUpdate(FavItemReference fav, InventorySlot oldSlot, InventorySlot newSlot)
        {
            // Avoid null references
            if (fav == null) return;

            // External container items cannot be pinned.
            if (newSlot.Policy.IsExternalContainer) UnpinFromFavMenu(oldSlot);
            else
            {
                // Update the favorite's position
                // We need to keep track of the Favorite Item through the Inventory/Hotbar every time we move it
                fav.row = newSlot.row;
                fav.col = newSlot.col;
                fav.isInventorySlot = newSlot.IsInventorySlot;
            }
        }

        /// <summary>
        /// Unpins an Inventory Slot from the favoritesList
        /// </summary>
        /// <param name="slot"></param>
        public void UnpinFromFavMenu(InventorySlot slot)
        {
            int index = favoritesList.FindIndex(f => f.row == slot.row && f.col == slot.col);

            // Make sure item exists
            if (index != -1)
            {
                UnpinBasedOnIndex(index);
            }
        }

        /// <summary>
        /// Unpins an Inventory Slot from the favoritesList based on the index it occupies in favoritesList
        /// </summary>
        /// <param name="slotIndex"></param>
        public void UnpinBasedOnIndex(int slotIndex)
        {
            // Reset properties
            favoritesList[slotIndex].item = null;
            favoritesList[slotIndex].isInventorySlot = false;
            favoritesList[slotIndex].row = -1;
            favoritesList[slotIndex].col = -1;

            // Reinitialize the inventory slot
            inventorySlots[slotIndex].Initialize(slotIndex, favoritesList[slotIndex], this, inventoryProManager, weaponController);
        }
        // Highlight a Favorite Item and display its Icon & Name
        public void Highlight(FavItemReference favItemReference)
        {
            if (favItemReference.item == null) return;

            highlightText.gameObject.SetActive(true);
            highlightIcon.gameObject.SetActive(true);
            highlightText.text = $"{favItemReference.item._name}";
            highlightIcon.sprite = favItemReference.item.icon;
        }

        // Unhighlights a Favorite Item.
        public void Unhighlight()
        {
            highlightText.gameObject.SetActive(false);
            highlightIcon.gameObject.SetActive(false);
        }
    }

}