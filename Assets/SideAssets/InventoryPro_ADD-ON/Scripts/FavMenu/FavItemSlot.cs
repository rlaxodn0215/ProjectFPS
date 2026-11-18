using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections;

namespace cowsins.Inventory
{
    /// <summary>
    /// Button that contains Favorite Items Data from the Inventory.
    /// On clicking this FavItemSlot, an action can be performed, generally using or consuming the object from the Inventory,
    /// allowing for quick access for Players´ favorites or most used items.
    /// </summary>
    public class FavItemSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [Title("References")]
        [SerializeField, Tooltip("Image that displays the Icon of the assigned Favorite Item (if exists)")] private Image iconImage;
        [SerializeField, Tooltip("Contains Visuals. It gets scaled on hovering & unhovering it.")] private Transform inventoryContainer;
        [SerializeField,Tooltip("This Object gets enabled when the FavItemSlot is hovered only.")] private GameObject highlightedBorder;

        [Title("Inventory Slot Interactions", upMargin = 10)]
        [SerializeField, Tooltip("On hovering, target scale of this object. When unhovering, the FavItemSlot will go back to" +
            "the original scale = 1f")] private float highlightedScale = 1.2f;
        [SerializeField, Tooltip("Speed to lerp from default Scale to highlighted Scale.")] private float lerpSpeed = 5f;

        [ReadOnly, Title("Debug", upMargin = 10), SerializeField, Tooltip("Displays the index of this specific FavItemSlot in favoritesList from the Menu")] private int index;
        [ReadOnly, SerializeField, Tooltip("Assigned Reference to an Inventory/Hotbar slot")] private FavItemReference favItemReference;

        // INTERNAL USE & REFERENCES
        private FavItemsMenu favItemsMenu;
        private Vector3 normalScale;
        private InventoryProManager inventoryProManager;
        private WeaponController weaponController;

        // Gather Default scale for unhovering
        private void Awake() => normalScale = transform.localScale;

        // Avoids selecting the slot when opening the Menu if it was previously hovered
        private void OnDisable() => OnPointerExit(null);
        public void Initialize(int index, FavItemReference reference, FavItemsMenu favItemsMenu, InventoryProManager inventoryProManager, WeaponController weaponController)
        {
            this.index = index;
            this.favItemReference = reference;
            this.weaponController = weaponController;
            this.inventoryProManager = inventoryProManager;
            this.favItemsMenu = favItemsMenu;

            // Initially deselect the slot to avoid Visual Issues
            Deselect();
            UpdateSlotGraphics();
        }

        // INPUT HANDLING
        public void OnPointerEnter(PointerEventData eventData) => Select();
        public void OnPointerExit(PointerEventData eventData) => Deselect();
        public void OnPointerClick(PointerEventData eventData) => ClickAction(eventData);

        // Updates the Visuals of the Slot.
        // Generally called after changing the favItemReference.
        public void UpdateSlotGraphics()
        {
            iconImage.gameObject.SetActive(true);
            iconImage.sprite = GatherSprite();

            if (favItemReference?.item == null)
            {
                iconImage.sprite = null;
                iconImage.gameObject.SetActive(false);
                return;
            }
            iconImage.gameObject.SetActive(true);
            iconImage.sprite = GatherSprite();
        }

        /// <summary>
        /// Returns the Icon from the current Fav Item Reference
        /// </summary>
        /// <returns>Sprite</returns>
        protected Sprite GatherSprite()
        {
            return favItemReference?.item != null ? favItemReference.item.icon : null;
        }

        /// <summary>
        /// This is called when clicking on the Slot. It uses/consumes the item ( if the item in favItemReference is not null )
        /// And unpins it if the amount is zero after using it
        /// </summary>
        public void ClickAction(PointerEventData eventData)
        {
            if (favItemReference?.item == null) return;

            if (eventData.button == PointerEventData.InputButton.Left)
            {
                GridGenerator gridGenerator = inventoryProManager._GridGenerator;
                int currentWeapon = weaponController.CurrentWeaponIndex;

                InventorySlot originalSlot = favItemReference.isInventorySlot
                    ? inventoryProManager.InventorySlots[favItemReference.row, favItemReference.col]
                    : inventoryProManager.HotbarSlots[favItemReference.col];

#if INVENTORY_PRO_ADD_ON
                ToastManager.Instance.ShowToast($"{originalSlot.slotData.item._name} {ToastManager.Instance.ItemUsed}");
                favItemReference.item.Use(inventoryProManager, originalSlot);
#endif

                if (originalSlot.slotData.amount <= 0) favItemsMenu.UnpinBasedOnIndex(index);
            }
            else if (inventoryProManager.RightClickUnpinsItem)
            {
                ToastManager.Instance.ShowToast($"{favItemReference.item._name}{ToastManager.Instance.ItemUnpinnedToastMsg}");
                favItemsMenu.UnpinBasedOnIndex(index);
            }

        }

        // Visuals on Selecting the Slot
        public void Select()
        {
            if (!isActiveAndEnabled) return;

            highlightedBorder.SetActive(true);
            favItemsMenu.Highlight(favItemReference);
            StartCoroutine(SmoothScaleCoroutine(Vector3.one * highlightedScale));
        }


        // Visuals on Deselecting the Slot
        public void Deselect()
        {
            highlightedBorder.SetActive(false);
            if (gameObject.activeInHierarchy) StartCoroutine(SmoothScaleCoroutine(normalScale));
            else inventoryContainer.localScale = normalScale;
            favItemsMenu.Unhighlight();
        }

        // Handles Scale when Hover/Unhovered
        private IEnumerator SmoothScaleCoroutine(Vector3 targetScale)
        {
            Vector3 startScale = inventoryContainer.localScale;
            float duration = 1f / lerpSpeed;
            float elapsedTime = 0f;

            while (elapsedTime < duration)
            {
                float t = elapsedTime / duration;
                inventoryContainer.localScale = Vector3.Lerp(startScale, targetScale, t);
                elapsedTime += Time.deltaTime;
                yield return null;
            }

            inventoryContainer.localScale = targetScale;
        }
    }
}