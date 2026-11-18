using UnityEngine;
using UnityEngine.UI; 
using TMPro;
using UnityEngine.EventSystems;
using System.Collections;
using UnityEngine.Rendering;

namespace cowsins.Inventory
{
    /// <summary>
    /// In InventoryProManager there are 4 types of Slots: Inventory, Hotbar, Chest, Custom (Spare slot)
    /// </summary>
    [System.Serializable]
    public enum SlotType
    {
        Inventory, Hotbar, Chest, Custom, Backpack
    }

    /// <summary>
    /// When using the Inventory or Chests in FPS Engine, Players will interact with InventorySlots. InventorySlot contains information about items, quantities & handles
    /// interactions between the InventoryProManager and the Player.
    /// </summary>
    public class InventorySlot : Identifiable, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        #region variables
        // READ-ONLY PROPERTIES FOR DEBUGGING
        [ReadOnly] public int row;
        [ReadOnly] public int col;
        [ReadOnly] public InventorySlot anchorSlot;
        [ReadOnly] public SlotData slotData;
        public SlotType slotType;

        public string CustomSlotId
        {
            get => UniqueIDValue;
            set => ReplaceUniqueID(value);
        }

        [SerializeField, Tooltip("Accepted Item_SO class name")]
        private string acceptedItemType = nameof(Item_SO);

        [SerializeField, Tooltip("Image that dipslays the Icon of the Item held by this InventorySlot")] private Image iconImage;
        [SerializeField, Tooltip("Text that shows the current amount or quantity stored in the slot data of this inventory slot.")] private TextMeshProUGUI amountText;
        [SerializeField, Tooltip("Background of the Slot. This is coloured to indicate if the Slot is Available or " +
            "occupied when hovered while dragging an object.")] private Image backgroundImage;
        [SerializeField, Tooltip("Image bar displaying durability")] private Image durabilityBar;
        [SerializeField] private GameObject durabilityBarParent;

        [Title("Inventory Slot Interactions")]
        [SerializeField, Tooltip("Inventory Slot object Scale when hovered/highlighted")] private float highlightedScale = 1.2f;
        [SerializeField, Tooltip("GameObject that gets enabled when the InventorySlot is highlighted. This particularly simulates a border.")] private GameObject highlightedBorder;
        [SerializeField, Tooltip("Speed to lerp from the default scale to the highlightedScale")] private float lerpSpeed = 5f;
        [SerializeField, Tooltip("Adjusts Icon Image Spacing to properly fit the Icons in the Slot")] private Vector3 iconImageMargins = new Vector3(10, -10, 0);
        [SerializeField, Tooltip("Object that contains the entire Inventory Slot")] private Transform inventoryContainer;

        // INTERNAL USE
        private Vector3 normalScale;
        protected InventoryProManager inventoryManager;
        protected WeaponController weaponController;
        protected SortingGroup sortingGroup;
        private float timeToShowTooltip = .4f;
        private RectTransform iconRect;
        protected Color defaultBackgroundColor;

        // GETTERS
        public Color DefaultBackgroundColor => defaultBackgroundColor;
        public Image BackgroundImage => backgroundImage;
        public Image DurabilityBar => durabilityBar;
        public GameObject DurabilityBarParent => durabilityBarParent;

        private ISlotPolicy policy;
        public ISlotPolicy Policy
        {
            get
            {
                if (policy == null || policy.SlotType != slotType)
                    policy = SlotPolicyRegistry.GetPolicy(slotType);
                return policy;
            }
            set => policy = value;
        }

        public bool IsHotbarSlot => Policy.SlotType == SlotType.Hotbar;

        public bool IsInventorySlot => Policy.SlotType == SlotType.Inventory;

        public bool IsChestSlot => Policy.SlotType == SlotType.Chest;

        public bool IsCustomSlot => Policy.SlotType == SlotType.Custom;
        public bool IsBackpackSlot => Policy.SlotType == SlotType.Backpack;

        public SlotType SlotType => Policy.SlotType;

        public string AcceptedItemType
        {
            get => string.IsNullOrEmpty(acceptedItemType) ? nameof(Item_SO) : acceptedItemType;
            set
            {
                acceptedItemType = value;
                cachedAcceptedType = null;
            }
        }

        /// <summary>
        /// Durability is only reduced when the item is equipped in a specific slot (not an "Any type" slot like the inventory backpack or chests).
        /// </summary>
        public bool AllowsDurability => Policy.AllowsDurabilityLoss && AcceptedType != null && AcceptedType != typeof(Item_SO);


        public void SetAcceptedType<T>() where T : Item_SO
        {
            acceptedItemType = typeof(T).Name;
            cachedAcceptedType = typeof(T);
        }

        public void SetAcceptedType(string typeName)
        {
            acceptedItemType = typeName;
            cachedAcceptedType = null;
        }

        public bool HasValidItem => slotData != null && slotData.item != null && slotData.amount > 0;

        public string ItemName => slotData?.item != null ? slotData.item._name : "";

        public Sprite ItemIcon => !Policy.IsGridBased || GetItemSize() == new Vector2Int(1, 1) ? slotData?.item?.icon :
                (slotData.item.irregularItemIcon != null ? slotData.item.irregularItemIcon : slotData.item.icon);

        public string ItemDescription
        {
            get
            {
                if (slotData?.item == null) return "";
                string desc = slotData.item.description ?? "";
                if (slotData.item.UseDurability)
                {
                    int current = Mathf.CeilToInt(slotData.currentDurability);
                    int max = Mathf.CeilToInt(slotData.item.MaxDurability);
                    desc = string.IsNullOrEmpty(desc)
                        ? $"Durability: {current} / {max}"
                        : $"{desc}\nDurability: {current} / {max}";
                }
                return desc;
            }
        }

        public bool IsItemWeapon => slotData?.item is Weapon_SO;

        public bool IsItemArmor => slotData?.item is Armor_SO;

        public bool IsItemAttachment => slotData?.item is AttachmentIdentifier_SO;

        public bool IsItemBullets => slotData?.item is BulletTypeIdentifier_SO;

        private static readonly System.Collections.Generic.Dictionary<string, System.Type> itemTypeCache =
            new System.Collections.Generic.Dictionary<string, System.Type>(System.StringComparer.Ordinal);
        private System.Type cachedAcceptedType;

        public System.Type AcceptedType
        {
            get
            {
                if (cachedAcceptedType == null)
                    cachedAcceptedType = ResolveItemType(acceptedItemType);
                return cachedAcceptedType;
            }
        }

        /// <summary>
        /// Validates whether the given item can be accepted into this slot based on its type and policy
        /// </summary>
        public bool AcceptsItem(Item_SO item)
        {
            return Policy.AcceptsItem(this, item);
        }

        private static System.Type ResolveItemType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName) || typeName == nameof(Item_SO))
                return typeof(Item_SO);

            if (itemTypeCache.TryGetValue(typeName, out var cached))
                return cached;

            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        if (typeof(Item_SO).IsAssignableFrom(type) && type.Name == typeName)
                        {
                            itemTypeCache[typeName] = type;
                            return type;
                        }
                    }
                }
                catch { }
            }

            return null;
        }

        #endregion

        #region initialization 

        private void Awake()
        {
            // For dynamically instantiated slots at runtime assign a new unique ID
            if (Application.isPlaying && slotType != SlotType.Custom) GenerateUniqueID();

            normalScale = transform.localScale;
            iconRect = iconImage.GetComponent<RectTransform>();
            sortingGroup = GetComponent<SortingGroup>();
            // Ensure the Inventory Slot is unselected by default.
            Unselect();
        }

        private void Start()
        {
            if (slotType == SlotType.Custom && inventoryManager == null) AutoInitializeCustomSlot();
        }

        private void OnEnable()
        {
            if (slotType == SlotType.Custom)
            {
                if (inventoryManager == null) AutoInitializeCustomSlot();
                else inventoryManager.RegisterCustomSlot(this);
            }
        }

        // Automatically initializes a spare slot placed anywhere in the UI hierarchy
        public void AutoInitializeCustomSlot()
        {
            if (inventoryManager == null)
                inventoryManager = InventoryProManager.instance;

            if (inventoryManager == null) return;

            weaponController = inventoryManager._WeaponController;
            if (backgroundImage != null)
                defaultBackgroundColor = backgroundImage.color;

            if (slotData == null)
                slotData = new SlotData();

            if (inventoryManager.DataModel != null)
                BindDataModel(inventoryManager.DataModel);

            inventoryManager.RegisterCustomSlot(this);
            UpdateSlotGraphics();
        }

        private Item_SO currentCustomEffectItem;
        public Item_SO CurrentCustomEffectItem => currentCustomEffectItem;

        public void UpdateCustomSlotEffects()
        {
            if (slotType != SlotType.Custom) return;

            Item_SO newItem = HasValidItem ? slotData.item : null;

            PlayerDependencies player = inventoryManager != null ? inventoryManager.PlayerDependencies : (InventoryProManager.instance != null ? InventoryProManager.instance.PlayerDependencies : null);
            if (player == null) player = FindObjectOfType<PlayerDependencies>();

            if (currentCustomEffectItem == newItem) return;

            if (currentCustomEffectItem != null) currentCustomEffectItem.OnUnequipCustomSlot(this, player);

            if (player == null && newItem != null)
            {
                currentCustomEffectItem = null;
                return;
            }

            currentCustomEffectItem = newItem;

            if (currentCustomEffectItem != null) currentCustomEffectItem.OnEquipCustomSlot(this, player);
        }

        /// <summary>
        /// Initializes all required references and properties to ensure the InventorySlot works accordingly.
        /// </summary>
        /// <param name="row">Vertical Position of the Inventory Slot.</param>
        /// <param name="col">Horizontal Position of the Inventory Slot</param>
        /// <param name="data">Initial SlotData stored.</param>
        /// <param name="weaponController">Reference to the Player's WeaponController</param>
        /// <param name="inventoryManager">Reference to the InventoryProManager</param>
        private InventoryDataModel boundDataModel;

        public void BindDataModel(InventoryDataModel model)
        {
            if (boundDataModel == model) return;
            UnbindDataModel();

            boundDataModel = model;
            if (boundDataModel != null)
            {
                boundDataModel.OnSlotChanged += HandleSlotChanged;
                boundDataModel.OnSlotAreaChanged += HandleSlotAreaChanged;
                boundDataModel.OnGridChanged += HandleGridChanged;
            }
        }

        public void UnbindDataModel()
        {
            if (boundDataModel != null)
            {
                boundDataModel.OnSlotChanged -= HandleSlotChanged;
                boundDataModel.OnSlotAreaChanged -= HandleSlotAreaChanged;
                boundDataModel.OnGridChanged -= HandleGridChanged;
                boundDataModel = null;
            }
        }

        private void OnDestroy()
        {
            UnbindDataModel();
            if (slotType == SlotType.Custom)
            {
                if (currentCustomEffectItem != null)
                {
                    PlayerDependencies player = inventoryManager != null ? inventoryManager.PlayerDependencies : (InventoryProManager.instance != null ? InventoryProManager.instance.PlayerDependencies : null);
                    if (player == null) player = FindObjectOfType<PlayerDependencies>();
                    currentCustomEffectItem.OnUnequipCustomSlot(this, player);
                    currentCustomEffectItem = null;
                }
                if (inventoryManager != null) inventoryManager.UnregisterCustomSlot(this);
            }
        }

        private void HandleSlotChanged(InventorySlot slot)
        {
            if (slot == null || slot.slotType != slotType) return;
            if (slot == this || (anchorSlot != null && slot == anchorSlot)) UpdateSlotGraphics();
        }

        private void HandleSlotAreaChanged(InventorySlot anchor)
        {
            if (anchor == null || anchor.slotType != slotType) return;
            if (this == anchor || anchorSlot == anchor) UpdateSlotGraphics();
        }

        private void HandleGridChanged() => UpdateSlotGraphics();

        public void Initialize(int row, int col, SlotData data, WeaponController weaponController, InventoryProManager inventoryManager)
        {
            this.inventoryManager = inventoryManager;
            defaultBackgroundColor = backgroundImage.color;

            this.row = row;
            this.col = col;
            this.slotData = data;
            this.weaponController = weaponController;

            if (inventoryManager?._GridGenerator?.DataModel != null) BindDataModel(inventoryManager._GridGenerator.DataModel);

            // Once the required information is filled in, update the slot graphics.
            UpdateSlotGraphics();

            // Ensure max amount on Weapons & attachments is 1.
            if (slotData.item is Weapon_SO || slotData.item is AttachmentIdentifier_SO) slotData.amount = 1;
        }
        #endregion

        #region input-detection

        // Calls when a drag happens
        public void OnBeginDrag(PointerEventData eventData)
        {
            if (IsShiftHeld()) return; // Shift is reserved for quick transfer, never start dragging on shift-click
            InventorySlot selectedAnchorSlot = GetAnchorSlot();
            bool isRightClick = eventData != null && eventData.button == PointerEventData.InputButton.Right;
            inventoryManager.NavigationBehaviour.OnBeginDrag(selectedAnchorSlot, AllowBeginDrag(selectedAnchorSlot), isRightClick);
        }

        // Calls while the drag happens
        public void OnDrag(PointerEventData eventData) { }

        // Calls when the slot is hovered
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (inventoryManager == null) return;

            Invoke(nameof(ShowTooltip), timeToShowTooltip);
            if (sortingGroup != null) sortingGroup.sortingOrder = 100; // Avoids clipping with other slots
            if (inventoryManager.NavigationBehaviour != null) inventoryManager.NavigationBehaviour.OnPointerEnter(this);
        }

        // Calls when the slot is not hovered anymore
        public void OnPointerExit(PointerEventData eventData)
        {
            CancelInvoke(nameof(ShowTooltip));
            if (sortingGroup != null) sortingGroup.sortingOrder = 15;
            if (inventoryManager?.NavigationBehaviour != null) inventoryManager.NavigationBehaviour.OnPointerExit(defaultBackgroundColor);
        }
        // Calls when a Drag action finished
        public void OnEndDrag(PointerEventData eventData)
        {
            if (inventoryManager?._GridGenerator?.Operations != null)
                inventoryManager._GridGenerator.Operations.OnEndDrag(true);
        }
        // Calls when The Inventory Slot is clicked.
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData == null || inventoryManager == null) return;

            // Shift + Left Click: Instant Container Quick Transfer
            bool isClickGrabbed = inventoryManager.NavigationBehaviour != null && inventoryManager.NavigationBehaviour.IsClickGrabbed;
            if (eventData.button == PointerEventData.InputButton.Left && !isClickGrabbed && IsShiftHeld())
            {
                InventorySlot anchor = GetAnchorSlot();
                if (anchor != null && anchor.HasValidItem)
                {
                    if (inventoryManager.QuickTransfer(anchor))
                    {
                        inventoryManager.UIController?.StopDragVisuals();
                        inventoryManager.NavigationBehaviour?.ResetHighlightedSlotsArea(defaultBackgroundColor, clearCurrentReferences: true);
                        return;
                    }
                }
            }

            // Handle Right Click
            if (eventData.button == PointerEventData.InputButton.Right)
            {
                // If an item is currently grabbed with click-grab deposit 1 item
                if (inventoryManager.NavigationBehaviour != null && inventoryManager.NavigationBehaviour.IsClickGrabbed && inventoryManager.draggedSlot != null && inventoryManager.draggedSlot.HasValidItem)
                {
                    InventorySlot targetSlot = this;
                    InventorySlot draggedAnchor = inventoryManager.draggedSlot.GetAnchorSlot();

                    bool deposited = inventoryManager.DepositSingleItem(draggedAnchor, targetSlot);
                    if (deposited)
                    {
                        if (inventoryManager.draggedSlot == null || inventoryManager.draggedSlot.slotData == null || inventoryManager.draggedSlot.slotData.amount <= 0)
                        {
                            inventoryManager.UIController.StopDragVisuals();
                            inventoryManager.NavigationBehaviour.ClearDraggedHighlightedReferences();
                        }
                    }
                    return;
                }

                inventoryManager.onSlotRightClick?.Invoke(GetAnchorSlot());
                return;
            }

            // Handle Left Click
            if (eventData.button == PointerEventData.InputButton.Left) HandleLeftClick();
        }

        private static bool IsShiftHeld()
        {
            return UnityEngine.InputSystem.Keyboard.current != null &&
                   (UnityEngine.InputSystem.Keyboard.current.leftShiftKey.isPressed || UnityEngine.InputSystem.Keyboard.current.rightShiftKey.isPressed);
        }

        private void HandleLeftClick()
        {
            if (inventoryManager?.NavigationBehaviour == null) return;

            var nav = inventoryManager.NavigationBehaviour;

            // We are already holding an item (click-grabbed or controller-selected)
            if (inventoryManager.draggedSlot != null)
            {
                InventorySlot draggedAnchor = inventoryManager.draggedSlot.GetAnchorSlot();
                InventorySlot targetAnchor = GetAnchorSlot();

                // If clicking the same slot/item again
                if (draggedAnchor == targetAnchor)
                {
                    bool wasRotated = nav.WasDraggedItemRotated();
                    if (wasRotated)
                    {
                        InventorySlot targetSlot = this;
                        if (inventoryManager.Operations.CanPlaceOrSwap(draggedAnchor, targetSlot))
                        {
                            nav.PlaceOrSwapClickGrab(targetSlot);
                            return;
                        }
                        if (targetSlot != draggedAnchor && inventoryManager.Operations.CanPlaceOrSwap(draggedAnchor, draggedAnchor))
                        {
                            nav.PlaceOrSwapClickGrab(draggedAnchor);
                            return;
                        }
                    }

                    nav.CancelClickGrab();
                    return;
                }

                // Placing or swapping onto another slot
                nav.PlaceOrSwapClickGrab(this);
                return;
            }

            // If drag mode is set to HoldToDragOnly, click-to-grab is disabled
            if (inventoryManager._DragMode == InventoryProManager.DragMode.HoldToDragOnly)
                return;

            // Not currently holding an item -> pick up / grab this item
            InventorySlot clickedAnchor = GetAnchorSlot();
            if (clickedAnchor != null && clickedAnchor.HasValidItem && AllowBeginDrag(clickedAnchor))
            {
                nav.StartClickGrab(clickedAnchor);
            }
        }

        #endregion

        #region utilities

        /// <summary>
        /// Displays a tooltip with the name and description of the stored Item in SlotData
        /// </summary>
        private void ShowTooltip()
        {
            // First ensure we can show the tooltip ( InventorySlot has a valid Item )
            if (CanShowTooltip())
            {
                //Always show the tooltip based on the Anchor�s Item
                InventorySlot anchor = GetAnchorSlot();
                inventoryManager._TooltipManager.SetTooltipVisibility(true, anchor.ItemName, anchor.ItemDescription);
            }
        }

        /// <summary>
        /// Returns whether the tooltip can be shown or not.
        /// </summary>
        private bool CanShowTooltip()
        {
            InventorySlot anchor = GetAnchorSlot();
            return anchor.slotData != null && anchor.slotData.item != null && anchor.slotData.amount > 0;
        }

        /// <summary>
        /// Updates a specific InventorySlot visuals. Displays the Icon of the stored Item in SlotData, and displays the amount ( If Item is not null ).
        /// In case Item is null, it will reset the graphics to the default/blank state.
        /// </summary>
        public void UpdateSlotGraphics()
        {
            // First check if this slot is anchored in a 2D spatial grid
            if (anchorSlot && Policy.IsGridBased)
            {
                // Initially set the amount text to false, it will be enabled later if required.
                amountText.gameObject.SetActive(false);

                // This is the anchor slot = origin of the slot
                if (anchorSlot == this)
                {
                    // Update Icon�s sprite & sizing.
                    // If Inventory Style is Tetris, GatherSprite can return any size ( ex: 1x1, 2x1, 2x2, etc... )
                    // If it�s Grid, it can only return 1x1. 
                    iconImage.gameObject.SetActive(true);
                    iconImage.sprite = ItemIcon;
                    Vector2Int originalSize = GetItemSize();

                    // Original size is 1x1
                    if (originalSize == new Vector2Int(1, 1))
                    {
                        // Set the Amount Text but only display it if amount is greater than 1
                        amountText.text = slotData.amount.ToString();
                        amountText.gameObject.SetActive(slotData?.amount > 1 && slotData?.item ? true : false);
                    }
                    Vector2 newSize = transform.localScale * (Vector2)originalSize;

                    if (iconRect == null)
                        iconRect = iconImage.GetComponent<RectTransform>();

                    // Handle Icon orientation & sizing
                    int rot = slotData.rotationAngle;
                    if (rot == 0 && !slotData.isOriented) rot = 90;
                    rot = ((rot % 360) + 360) % 360;

                    float adjustedX = iconImageMargins.x + (iconRect.rect.width / 2 * originalSize.x) + (10 * (originalSize.x - 1));

                    switch (rot)
                    {
                        case 90:
                            iconRect.localScale = new Vector2(newSize.y, newSize.x);
                            iconRect.localRotation = Quaternion.Euler(0, 0, -90);
                            iconRect.pivot = new Vector2(0, 0.5f);
                            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 1);
                            iconRect.localPosition = new Vector3(adjustedX, iconImageMargins.y, 0);
                            break;
                        case 180:
                            iconRect.localScale = newSize;
                            iconRect.localRotation = Quaternion.Euler(0, 0, 180);
                            iconRect.pivot = new Vector2(1, 0);
                            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0, 1);
                            iconRect.localPosition = iconImageMargins;
                            break;
                        case 270:
                            iconRect.localScale = new Vector2(newSize.y, newSize.x);
                            iconRect.localRotation = Quaternion.Euler(0, 0, 90);
                            iconRect.pivot = new Vector2(1, 0.5f);
                            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 1);
                            iconRect.localPosition = new Vector3(adjustedX, iconImageMargins.y, 0);
                            break;
                        default: // 0
                            iconRect.localScale = newSize;
                            iconRect.localRotation = Quaternion.identity;
                            iconRect.pivot = new Vector2(0, 1);
                            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0, 1);
                            iconRect.localPosition = iconImageMargins;
                            break;
                    }

                    // Prevent the icons from being behind other buttons
                    this.transform.SetAsLastSibling();
                    slotData?.EnsureDurabilityInitialized();
                    UpdateDurabilityUI();
                }
                else
                {
                    // This InventorySlot is anchored to a slot that's not itself. This can only happen in Tetris Inventory.
                    // Since the Icon is handled by the slot, deactivate Icon Image here.
                    iconImage.sprite = null;
                    iconImage.gameObject.SetActive(false);
                    SetDurabilityBarActive(false);
                    // Amount Text needs to be displayed at the right bottom corner always.
                    Vector2Int anchorSize = anchorSlot != null ? anchorSlot.GetItemSize() : Vector2Int.one;
                    if ((row == anchorSlot.row + anchorSize.y - 1 && col == anchorSlot.col + anchorSize.x - 1) ||
                        (row == anchorSlot.row + anchorSize.x - 1 && col == anchorSlot.col + anchorSize.y - 1))
                    {
                        amountText.text = anchorSlot.slotData.amount.ToString();
                        amountText.gameObject.SetActive(anchorSlot.slotData?.amount > 1 && anchorSlot.slotData?.item ? true : false);
                    }
                }
                return;

            }

            // Restart Visuals to Default/Blank if no SlotData or Item is stored
            if ((slotData == null || slotData.amount <= 0 || slotData.item == null) && !anchorSlot)
            {
                iconImage.sprite = null;
                iconImage.gameObject.SetActive(false);
                amountText.gameObject.SetActive(false);
                UpdateDurabilityUI();
                if (slotType == SlotType.Custom) UpdateCustomSlotEffects();
                return;
            }

            slotData?.EnsureDurabilityInitialized();
            iconImage.gameObject.SetActive(true);
            iconImage.sprite = ItemIcon;
            amountText.text = slotData.amount.ToString();
            amountText.gameObject.SetActive(slotData?.amount > 1 && slotData?.item ? true : false);
            UpdateDurabilityUI();
            if (slotType == SlotType.Custom) UpdateCustomSlotEffects();
        }

        public float CurrentDurability => slotData != null ? slotData.currentDurability : 0f;
        public float MaxDurability => (slotData != null && slotData.item != null) ? slotData.item.MaxDurability : 0f;

        public void ReduceDurability(float amount)
        {
            if (slotData == null || slotData.item == null || !slotData.item.UseDurability) return;
            if (!AllowsDurability) return;
            if (slotData.currentDurability <= 0) return;

            float prev = slotData.currentDurability;
            slotData.currentDurability = Mathf.Max(0f, slotData.currentDurability - amount);
            slotData.item.OnDurabilityReduced(this, prev, slotData.currentDurability);

            if (slotData.currentDurability <= 0) BreakItem();
            else UpdateDurabilityUI();
        }

        public void BreakItem()
        {
            if (slotData == null || slotData.item == null) return;
            Item_SO item = slotData.item;
            string itemName = item._name;
            PlayerDependencies player = inventoryManager != null ? inventoryManager.PlayerDependencies : (InventoryProManager.instance != null ? InventoryProManager.instance.PlayerDependencies : null);
            if (player == null) player = FindObjectOfType<PlayerDependencies>();

            item.OnDurabilityDepleted(this, player);

            if (inventoryManager?._GridGenerator?.Operations != null) inventoryManager._GridGenerator.Operations.DeleteItem(this, notify: false);
            else ClearSlot();

            if (inventoryManager?.FeedbackService != null) inventoryManager.FeedbackService.OnItemBroken(itemName);
            else
            {
                bool showToast = inventoryManager != null ? inventoryManager.ShowToastOnItemBreak : (InventoryProManager.instance != null && InventoryProManager.instance.ShowToastOnItemBreak);
                if (showToast && ToastManager.Instance != null)
                {
                    string brokeMsg = !string.IsNullOrEmpty(ToastManager.Instance.ItemBroke) ? ToastManager.Instance.ItemBroke : "broke";
                    ToastManager.Instance.ShowToast($"{itemName} {brokeMsg}");
                }
            }
        }

        public void UpdateDurabilityUI()
        {
            if (durabilityBar == null)
            {
                if (durabilityBarParent != null) durabilityBarParent.SetActive(false);
                return;
            }

            if (slotData == null || slotData.item == null || !slotData.item.UseDurability || slotData.item.MaxDurability <= 0)
            {
                SetDurabilityBarActive(false);
                return;
            }

            slotData.EnsureDurabilityInitialized();

            // Durability bar is only visible if Eligible for durability reduction or Lower than full durability
            if (!AllowsDurability || slotData.currentDurability >= slotData.item.MaxDurability)
            {
                SetDurabilityBarActive(false);
                return;
            }

            float ratio = Mathf.Clamp01(slotData.currentDurability / slotData.item.MaxDurability);
            SetDurabilityBarActive(true);
            if (durabilityBar.type != Image.Type.Filled)
            {
                durabilityBar.type = Image.Type.Filled;
                durabilityBar.fillMethod = Image.FillMethod.Horizontal;
                durabilityBar.fillOrigin = (int)Image.OriginHorizontal.Left;
            }
            durabilityBar.fillAmount = ratio;
        }

        public void SetDurabilityBarActive(bool active)
        {
            if (durabilityBarParent != null) durabilityBarParent.SetActive(active);
            if (durabilityBar != null) durabilityBar.gameObject.SetActive(active);
        }

        /// <summary>
        /// Resets the Slot Data of an Ivnentory Slot & Updates its Visuals
        /// </summary>
        public void ClearSlot()
        {
            slotData = new SlotData();
            anchorSlot = null;
            if (backgroundImage != null) backgroundImage.color = defaultBackgroundColor;
            UpdateSlotGraphics();
        }

        /// <summary>
        /// Returns current Anchor Slot. If Anchor Slot is null, it will return itself, as it cannot be anchored to anything else.
        /// </summary>
        public InventorySlot GetAnchorSlot()
        {
            return anchorSlot != null ? anchorSlot : this;

        }

        // Returns true if a Drag action can begin
        protected bool AllowBeginDrag(InventorySlot anchor)
        {
            return anchor.slotData != null && anchor.slotData.item != null && anchor.slotData.amount > 0;
        }

        /// <summary>
        /// Returns the Item Size of the Item in SlotData.
        /// If Item is null, a default 1x1 size will be returned.
        /// </summary>
        public Vector2Int GetItemSize()
        {
            if (slotData.item == null) return new Vector2Int(1, 1);

            Vector2Int calculatedItemSize = inventoryManager._GridGenerator.IsGridInventory() ? new Vector2Int(1, 1) : slotData.item.itemSize;
            calculatedItemSize = slotData.isOriented ? calculatedItemSize : new Vector2Int(calculatedItemSize.y, calculatedItemSize.x);
            return calculatedItemSize;
        }

        /// <summary>
        /// Highlights this Inventory Slot and represents it visually. Often used by Gamepad / Controller.
        /// </summary>
        public void Select()
        {
            if (!isActiveAndEnabled) return;
            highlightedBorder.SetActive(true);
            StartCoroutine(SmoothScaleCoroutine(Vector3.one * highlightedScale));
        }

        /// <summary>
        /// Unselects this slot and represents it visually. Often used by Gamepad / Controller.
        /// </summary>
        public void Unselect()
        {
            highlightedBorder.SetActive(false);
            CancelInvoke(nameof(ShowTooltip));
            if (gameObject.activeInHierarchy) StartCoroutine(SmoothScaleCoroutine(normalScale));
            else inventoryContainer.localScale = normalScale;
        }

        // Handles scaling when hovered/unhovered
        private IEnumerator SmoothScaleCoroutine(Vector3 targetScale)
        {
            float elapsedTime = 0f;
            while (elapsedTime < 1f)
            {
                elapsedTime += Time.deltaTime * lerpSpeed;
                inventoryContainer.localScale = Vector3.Lerp(inventoryContainer.localScale, targetScale, Time.deltaTime * lerpSpeed);
                yield return null;
            }

            // Ensure the final scale is exactly the target scale
            inventoryContainer.localScale = targetScale;
        }
        #if UNITY_EDITOR
        public override void OnValidate()
        {
            if (slotType != SlotType.Custom) return;
            base.OnValidate();
        }
        #endif
        #endregion
    }

}