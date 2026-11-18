using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace cowsins.Inventory
{
    /// <summary>
    /// You can Right click on an Inventory, Hotbar or Chest Slot to open the ContextMenu.
    /// The Context Menu contains different Actions that you can perform based on the Highlighted Slot.
    /// </summary>
    [RequireComponent(typeof(VerticalLayoutGroup))]
    public class ContextMenu : MonoBehaviour
    {
        [SerializeField, Title("CONTEXT MENU REFERENCES")] private InventoryProManager inventoryProManager;
        [SerializeField] private Button useButton, pinButton, unpinButton, dropButton, splitButton, moveToButton, lootAllChestButton;
        
        // INTERNAL USE
        private InventorySlot currentSlot;
        private CanvasGroup canvasGroup;
        private AudioClip clickSFX;
        private Image image;

        private void Awake()
        {
            // Gather references
            canvasGroup = GetComponent<CanvasGroup>();
            image = GetComponent<Image>();

            // Add Buttons Listeners so they can perform their respective Actions on being clicked.
            useButton.onClick.AddListener(UseItem);
            dropButton.onClick.AddListener(DropItem);
            splitButton.onClick.AddListener(SplitItemCount);
            if (inventoryProManager.UseFavouritesRadialMenu)
            {
                pinButton.onClick.AddListener(PinToFavorite);
                unpinButton.onClick.AddListener(UnpinFromFavorites);
            }

            clickSFX = inventoryProManager.ContextMenuClickSFX;

            // Ensure Context Menu is disabled ( hidden ) by default
            HideContextMenu();
        }

        private void OnDestroy()
        {
            // Disable Buttons functionality
            useButton.onClick.RemoveListener(UseItem);
            dropButton.onClick.RemoveListener(DropItem);
            splitButton.onClick.RemoveListener(SplitItemCount);
            if (inventoryProManager.UseFavouritesRadialMenu)
                pinButton.onClick.RemoveListener(PinToFavorite);
        }

        public void ShowContextMenu(InventorySlot slot, Vector3 position, float raycastPadding)
        {
            // Avoid showing the context menu if the slot is null or if it does not contain an item.
            if (slot == null || !slot.HasValidItem)
            {
                HideContextMenu();
                return;
            }

            // Adjust visibility + position
            RectTransform rectTransform = transform as RectTransform;
            RectTransform parentRect = rectTransform.parent as RectTransform;
            Canvas canvas = GetComponentInParent<Canvas>();
            Camera uiCamera = canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas?.worldCamera;

            if (parentRect != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, position, uiCamera, out Vector2 localPoint))
            {
                rectTransform.localPosition = localPoint;
            }
            else
            {
                transform.position = position;
            }
            inventoryProManager._TooltipManager.SetTooltipVisibility(false, string.Empty, string.Empty);

            currentSlot = slot;

            // Make it visible
            canvasGroup.alpha = 1;
            canvasGroup.interactable = true;
            image.raycastPadding = Vector4.one * -raycastPadding;

            // Check if the "Use" method is overridden
            Item_SO item = currentSlot.slotData.item;

            // To avoid Import issues, this code is only compiled if Inventory Pro Add-On is properly installed.
#if INVENTORY_PRO_ADD_ON
            bool canUseSlot = slot.Policy != null && slot.Policy.AllowsItemUsage;
            bool canUseItem = item != null && item.CanUse(slot, inventoryProManager);
            bool isUsable = canUseSlot && canUseItem;

            // Favourites Menu Pin / Unpin
            bool canPinSlot = slot.Policy != null && slot.Policy.AllowsFavoritesPinning;
            bool isPinned = inventoryProManager.UseFavouritesRadialMenu &&
                            inventoryProManager._FavItemsMenu != null &&
                            inventoryProManager._FavItemsMenu.IsPinned(slot);

            if (isPinned)
            {
                unpinButton.gameObject.SetActive(true);
                pinButton.gameObject.SetActive(false);
            }
            else
            {
                unpinButton.gameObject.SetActive(false);
                pinButton.gameObject.SetActive(inventoryProManager.UseFavouritesRadialMenu && canPinSlot && isUsable);
            }

            // Use Button
            if (useButton)
            {
                useButton.gameObject.SetActive(isUsable);
                var useText = useButton.GetComponentInChildren<TextMeshProUGUI>();
                if (useText != null && item != null) useText.text = item.GetContextActionName(slot, inventoryProManager);
            }

            // Split button can only appear if amount is greater than 1 and policy permits splitting
            if (splitButton)
            {
                bool hasMultipleAmount = slot.slotData.amount > 1;
                splitButton.gameObject.SetActive(hasMultipleAmount && currentSlot.Policy.AllowsSplitting);
            }

            // Move / Quick Transfer Button
            if (moveToButton)
            {
                var target = currentSlot.Policy != null && currentSlot.Policy.AllowsFastTransfer
                    ? inventoryProManager.GetTransferTarget(currentSlot)
                    : null;

                if (target != null)
                {
                    moveToButton.gameObject.SetActive(true);
                    var moveText = moveToButton.GetComponentInChildren<TextMeshProUGUI>();
                    if (moveText != null) moveText.text = target.ActionLabel;
                    moveToButton.onClick.RemoveAllListeners();
                    moveToButton.onClick.AddListener(ExecuteTransfer);
                }
                else moveToButton.gameObject.SetActive(false);
            }

            // Loot All Button
            if (lootAllChestButton)
            {
                bool canLoot = currentSlot.Policy != null && currentSlot.Policy.CanLootAll;
                lootAllChestButton.gameObject.SetActive(canLoot);
                if (canLoot)
                {
                    lootAllChestButton.onClick.RemoveAllListeners();
                    lootAllChestButton.onClick.AddListener(ExecuteLootAll);
                }
            }
#endif
        }

        /// <summary>
        /// Disables Context Menu from UI.
        /// </summary>
        public void HideContextMenu()
        {
            transform.position = Vector3.zero;
            canvasGroup.interactable = false;
            canvasGroup.alpha = 0;
            currentSlot = null;
        }

        private void UseItem()
        {
            // Avoid calling the use method wrong if the currentSlot is null
            if (currentSlot == null || !currentSlot.HasValidItem)
            {
                HideContextMenu();
                return;
            }

            Item_SO item = currentSlot.slotData.item;
            if (item != null && item.ShowUseToast)
            {
                string msg = item.GetUseToastMessage();
                if (!string.IsNullOrEmpty(msg))
                    ToastManager.Instance?.ShowToast(msg);
            }

#if INVENTORY_PRO_ADD_ON
            item?.Use(inventoryProManager, currentSlot);
#endif
            SoundManager.Instance.PlaySound(clickSFX, 0, 0, false);
            HideContextMenu();
        }

        private void DropItem()
        {
            // Avoid calling the drop method wrong if the currentSlot is null
            if (currentSlot == null)
            {
                HideContextMenu();
                return;
            }

            inventoryProManager.DropOutsideInventory(currentSlot);
            SoundManager.Instance.PlaySound(clickSFX, 0, 0, false);
            HideContextMenu();
        }

        private void SplitItemCount()
        {
            // Avoid calling the split method wrong if the currentSlot is null
            inventoryProManager.SplitOutsideInventory(currentSlot.GetAnchorSlot());
            SoundManager.Instance.PlaySound(clickSFX, 0, 0, false);
            HideContextMenu();
        }

        private void ExecuteTransfer()
        {
            if (currentSlot == null || !currentSlot.HasValidItem)
            {
                HideContextMenu();
                return;
            }

            inventoryProManager.QuickTransfer(currentSlot);
            SoundManager.Instance.PlaySound(clickSFX, 0, 0, false);
            HideContextMenu();
        }

        private void ExecuteLootAll()
        {
            if (currentSlot == null)
            {
                HideContextMenu();
                return;
            }

            currentSlot.Policy?.LootAll(inventoryProManager);
            SoundManager.Instance.PlaySound(clickSFX, 0, 0, false);
            HideContextMenu();
        }

        private void PinToFavorite()
        {
            // Avoid calling the pin method wrong if the currentSlot is null
            if (currentSlot == null)
            {
                HideContextMenu();
                return;
            }
            inventoryProManager._FavItemsMenu?.PinItem(currentSlot);

            SoundManager.Instance.PlaySound(clickSFX, 0, 0, false);
            HideContextMenu();
        }

        private void UnpinFromFavorites()
        {
            if (currentSlot == null)
            {
                HideContextMenu();
                return;
            }
            InventorySlot anchor = currentSlot.GetAnchorSlot();

            if (inventoryProManager._FavItemsMenu != null && anchor?.slotData?.item != null)
            {
                inventoryProManager._FavItemsMenu.UnpinFromFavMenu(anchor);
                SoundManager.Instance.PlaySound(clickSFX, 0, 0, false);
                ToastManager.Instance?.ShowToast($"{anchor.slotData.item._name} {ToastManager.Instance.ItemUnpinnedToastMsg}");
            }
            HideContextMenu();
        }
    }
}