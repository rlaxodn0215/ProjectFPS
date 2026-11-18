using cowsins.SaveLoad;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using System.Collections.Generic;
using System;
using UnityEngine.Events;

namespace cowsins.Inventory
{
    /// <summary>
    /// The InventoryProManager is the most essential part of the Inventory System. It manages Inventory Slots, Highlighted Slots, Fav Menus, Grid Generation, Inventory Visibility 
    /// & much more! 
    /// </summary>
    public class InventoryProManager : MonoBehaviour
    {
        /// <summary>
        /// Defines the style of the Inventory & its behaviour.
        /// Grid Slots take 1 space from the Inventory (1x1 Slots only)
        /// while Tetris slots can take different sizes ( 2x1, 2x2, 3x1, 3x2, etc... )
        /// </summary>
        [System.Serializable]
        public enum InventoryStyle
        {
            Grid, Tetris
        }

        /// <summary>
        /// Defines how items can be moved in the inventory
        /// HoldToDragOnly: Only hold left-click to drag items.
        /// HoldAndClickToMove: Both hold-to-drag and click-to-grab/click-to-place are enabled (Recommended)
        /// </summary>
        [System.Serializable]
        public enum DragMode
        {
            HoldToDragOnly,
            HoldAndClickToMove
        }

        /// <summary>
        /// Defines the behaviour when right clicking on an Inventory Slot.
        /// None: Nothing Happens. Right click behaviour is disabled.
        /// Drop: The Item will be dropped.
        /// ContextMenu: Context Menu appears, allowing multiple options ( Recommended )
        /// </summary>
        [System.Serializable]
        public enum SlotRightClickEvent
        {
            None, Drop, ContextMenu
        }

        [System.Serializable]
        public enum RotationDirections
        {
            TwoDirections,
            FourDirections
        }

        [System.Serializable]
        public class InventoryEvents
        {
            public UnityEvent onInventoryGenerated, onOpenInventory, onCloseInventory, onHotbarGenerated, onOpenChest, onCloseChest,
                onSlotBeginDrag, onSlotEndDrag, onRotateItem, onDropItem, onHighlightSlot, onStackItems, onPlaceItem;
            public UnityEvent<string> onItemDeleted;
            public UnityEvent<string> onItemBroken;
        }

        [SerializeField, Tooltip("Defines the style of the Inventory & its behaviour.\n\n" +
            "Grid Slots take 1 space from the Inventory (1x1 Slots only)\n\n" +
            "while Tetris slots can take different sizes ( 2x1, 2x2, 3x1, 3x2, etc... )")]
        private InventoryStyle inventoryStyle;
        [SerializeField, Tooltip("If true, the Player will be able to pin favorite Items in a Radial Menu for Quick Access & usage\n\n")] private bool useFavouritesRadialMenu;
        [SerializeField, Tooltip("Defines the behaviour when right clicking on an Inventory Slot.\n\n" +
            "None: Nothing Happens. Right click behaviour is disabled.\n\n" +
            "Drop: The Item will be dropped.\n\n" +
            "ContextMenu: Context Menu appears, allowing multiple options ( Recommended )")]
        SlotRightClickEvent slotRightClickEvent;
        [SerializeField] private PlayerDependencies playerDependencies;
        [SerializeField] private TooltipManager tooltipManager;
        [SerializeField] private FavItemsMenu favItemsMenu;
        [SerializeField] private bool rightClickUnpinsItem;
        [SerializeField] private GridGenerator gridGenerator;
        [SerializeField, Tooltip("if true & the hotbhar is full of weapons: Weapon will be stored in the Inventory.\n\n" +
            "If false & the hotbar is full of weapons: Weapon will be swapped in the hotbar for the current weapon.")]
        private bool storeWeaponsIfHotbarFull;
        [SerializeField, Tooltip("Defines how items can be moved in the inventory:\n\n" + "HoldToDragOnly: Only hold left-click to drag items.\n\n" + "HoldAndClickToMove: Both hold-to-drag and click-to-grab/click-to-place are enabled (Recommended)")]
        private DragMode dragMode = DragMode.HoldAndClickToMove;
        [SerializeField, Tooltip("Defines how many rotation directions items can have in Tetris inventory:\n\nTwoDirections: 0° and 90° (Default).\n\nFourDirections: 0°, 90°, 180°, and 270° (-90°).")]
        private RotationDirections rotationDirections = RotationDirections.TwoDirections;
        [SerializeField, Tooltip("If true, a toast notification is displayed whenever an item breaks")]
        private bool showToastOnItemBreak = true;
        [SerializeField, Tooltip("Attach the generic pickeable object here")] private Pickeable weaponGenericPickeable, attachmentGenericPickeable, bulletsGenericPickeable, itemGenericPickeable;
        [SerializeField, Tooltip("Reference to the Button that closes the Inventory on press. This action is automatically assigned.")] private Button closeButton;
        [SerializeField, Tooltip("Reference to the Area that deletes Items from the Inventory on drop. This action is automatically assigned.")] private RectTransform trashButton;
        [SerializeField, Tooltip("UI that appears when using a Gamepad with the Inventory")] private GameObject gamepadSpecificUI;
        [SerializeField, Tooltip("When highlighted, slots will take on a tint of this color.")] private Color highlightSlotColor;
        [SerializeField, Tooltip("When available while dragging a slot, slots will take on a tint of this color.")] private Color availableSlotColor;
        [SerializeField, Tooltip("When occupied while dragging a slot, slots will take on a tint of this color.")] private Color occupiedSlotColor;
        [SerializeField, Tooltip("UI that appears when examining an item. Check ExamineItem.cs for more information.")] private GameObject examinationUI;
        [SerializeField, Tooltip("Text to display examined name.")] private TextMeshProUGUI examinationText;
        [SerializeField, Tooltip("If true, and lootAllChestButton is not null, lootAllChestButton will loot the entire chest on click. This feature" +
            "is separate from Context Menu�s Loot All Button")]
        private bool allowLootAllChest;
        [SerializeField] private Button lootAllChestButton;
        [SerializeField, Tooltip("If slotRightClickEvent is set to ContextMenu, this object will be enabled when right clicking on slots, allowing" +
            "multiple actions and additional functionality.")]
        private ContextMenu contextMenu;
        [SerializeField, Tooltip("To avoid displaying the ContextMenu exactly where the Mouse is placed, we can add a position offset (horizontal, vertical)")] private Vector2 contextMenuOffset;
        [SerializeField, Range(0f, 50f)] private float contextMenuRaycastPadding = 30;
        [SerializeField] private InventoryEvents events;

        [SerializeField] private AudioClip openInventorySFX;
        [SerializeField] private AudioClip closeInventorySFX;
        [SerializeField] private AudioClip highlightItemSFX;
        [SerializeField] private AudioClip rotateItemSFX;
        [SerializeField] private AudioClip placeItemSFX;
        [SerializeField] private AudioClip contextMenuClickSFX;

        // NAVIGATION INPUT ACTIONS
        [SerializeField] private InventoryProManagerInputActions invInputActions;
        public Action<InventorySlot> onSlotRightClick;

        // BEHAVIOURS
        public InventoryInputHandler InputHandler { get; private set; }
        public InventoryUIController UIController { get; private set; }
        public InventoryProNavigationBehaviour NavigationBehaviour { get; private set; }
        public InvProAttachmentBehaviour AttachmentBehaviour { get; private set; }
        public InventoryInteractionBehaviour InteractionBehaviour { get; private set; }

        // GETTERS FOR UI CONTROLLER
        public AudioClip OpenInventorySFX => openInventorySFX;
        public AudioClip CloseInventorySFX => closeInventorySFX;
        public AudioClip HighlightItemSFX => highlightItemSFX;
        public Button CloseButton => closeButton;
        public GameObject GamepadSpecificUI => gamepadSpecificUI;
        public ContextMenu ContextMenu => contextMenu;
        public AudioClip RotateItemSFX => rotateItemSFX;

        private int itemRotation = 0;
        private InputManager inputManager;
        private InteractManager interactManager;
        private WeaponController weaponController;
        private PlayerMovement playerMovement;
        private PlayerStats playerStats;
        private PlayerControl playerControl;
        private CameraEffects cameraEffects;


        public int ItemRotation { get => itemRotation; set => itemRotation = value; }
        public InputManager InputManager => inputManager;

        // PUBLIC ACCESS
        public InventorySlot highlightedSlot;
        public InventorySlot draggedSlot;

        // GETTERS & ACCESSORS
        // All these are accessible to read but not to write
        public InteractManager _InteractManager => interactManager;
        public Pickeable WeaponGenericPickeable => weaponGenericPickeable;
        public Pickeable AttachmentGenericPickeable => attachmentGenericPickeable;
        public Pickeable BulletsGenericPickeable => bulletsGenericPickeable;
        public Pickeable ItemGenericPickeable => itemGenericPickeable;
        public InventoryStyle _InventoryStyle => inventoryStyle;
        public bool UseFavouritesRadialMenu => useFavouritesRadialMenu;
        public bool StoreWeaponsIfHotbarFull => storeWeaponsIfHotbarFull;
        public DragMode _DragMode => dragMode;
        public void SetDragMode(DragMode mode) => dragMode = mode;
        public RotationDirections _RotationDirections => rotationDirections;
        public void SetRotationDirections(RotationDirections directions) => rotationDirections = directions;
        public Color AvailableSlotColor => availableSlotColor;
        public Color OccupiedSlotColor => occupiedSlotColor;
        public TooltipManager _TooltipManager => tooltipManager;
        public GridGenerator _GridGenerator => gridGenerator;
        public FavItemsMenu _FavItemsMenu => favItemsMenu;
        public PlayerDependencies PlayerDependencies => playerDependencies;

        public bool RightClickUnpinsItem => rightClickUnpinsItem;
        public InventorySlot[,] InventorySlots => gridGenerator.InventorySlots;
        public InventorySlot[,] ChestSlots => gridGenerator.ChestSlots;
        public bool isChestOpen => gridGenerator != null && gridGenerator.isChestOpen;
        public InventorySlot[,] BackpackSlots => gridGenerator?.BackpackSlots;
        public bool isBackpackOpen => gridGenerator != null && gridGenerator.isBackpackOpen;
        public InvBackpackBehaviour BackpackBehaviour => gridGenerator?.BackpackBehaviour;
        public IContainerItem ActiveContainerItem => gridGenerator?.BackpackBehaviour?.ActiveContainerItem;
        public InventorySlot[] HotbarSlots => gridGenerator.HotbarSlots;
        public InventoryDataModel DataModel => gridGenerator?.DataModel;
        public IInventoryFeedbackService FeedbackService => gridGenerator?.Operations?.FeedbackService;
        public int Rows => gridGenerator != null ? gridGenerator.Rows : 0;
        public int Columns => gridGenerator != null ? gridGenerator.Columns : 0;
        public bool ExpandInventory(int extraRows, int extraColumns) => gridGenerator != null && gridGenerator.ExpandInventory(extraRows, extraColumns);
        public bool SetInventoryDimensions(int targetRows, int targetColumns) => gridGenerator != null && gridGenerator.SetInventoryDimensions(targetRows, targetColumns);
        public RectTransform TrashButton => trashButton;
        public AudioClip ContextMenuClickSFX => contextMenuClickSFX;
        public AudioClip PlaceItemSFX => placeItemSFX;
        public SlotRightClickEvent _SlotRightClickEvent => slotRightClickEvent;
        public List<InventorySlot> highlightedSlotArea = new List<InventorySlot>();
        public bool IsInventoryOpen => UIController != null ? UIController.IsInventoryOpen : false;
        public bool IsFavRadialMenuOpen => UIController != null ? UIController.IsFavRadialMenuOpen : false;

        public bool AllowLootAllChest => allowLootAllChest;
        public Button LootAllChestButton => lootAllChestButton;
        public bool ShowToastOnItemBreak => showToastOnItemBreak;
        public InventoryEvents Events => events;

        public WeaponController _WeaponController => weaponController;

        // SPARE / CUSTOM SLOTS
        private readonly List<InventorySlot> customSlots = new List<InventorySlot>();
        public IReadOnlyList<InventorySlot> CustomSlots => customSlots;
        public Action<InventorySlot> onCustomSlotChanged;

        public void RegisterCustomSlot(InventorySlot slot)
        {
            if (slot != null && !customSlots.Contains(slot))
                customSlots.Add(slot);
        }

        public void UnregisterCustomSlot(InventorySlot slot)
        {
            if (slot != null)
                customSlots.Remove(slot);
        }

        public void EnsureCustomSlotsRegistered()
        {
#if UNITY_2023_1_OR_NEWER
            InventorySlot[] allSlots = FindObjectsByType<InventorySlot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            InventorySlot[] allSlots = FindObjectsOfType<InventorySlot>(true);
#endif
            if (allSlots == null || allSlots.Length == 0) return;

            System.Array.Sort(allSlots, (a, b) =>
            {
                if (a == b) return 0;
                if (a == null) return -1;
                if (b == null) return 1;
                return string.CompareOrdinal(GetHierarchyPath(a.transform), GetHierarchyPath(b.transform));
            });

            foreach (var slot in allSlots)
            {
                if (slot != null && slot.IsCustomSlot)
                {
                    slot.AutoInitializeCustomSlot();
                }
            }
        }

        private static string GetHierarchyPath(Transform transform)
        {
            if (transform == null) return string.Empty;
            string path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = transform.name + "/" + path;
            }
            return path;
        }

        public static InventoryProManager instance;

        private void Awake()
        {
            // Handle singleton
            if (instance == null) instance = this;
            else Destroy(this.gameObject);
        }

        private void Start()
        {
            if (playerDependencies == null)
            {
                Debug.LogError("<color=red>[COWSINS]</color> <b><color=yellow>Player Dependencies Reference not found!</color></b> " +
                    "Please, assign the Player (PlayerDependencies) on Inventory Pro Manager > References > PlayerDependencies to fix this error.");
                this.gameObject.SetActive(false);
                return;
            }
            InitializeSettings();
        }
        private void InitializeSettings()
        {
            // Gahter References
            inputManager = playerDependencies.InputManager;
            interactManager = playerDependencies.GetComponent<InteractManager>();
            weaponController = playerDependencies.GetComponent<WeaponController>();
            playerMovement = playerDependencies.GetComponent<PlayerMovement>();
            playerStats = playerDependencies.GetComponent<PlayerStats>();
            playerControl = playerDependencies.GetComponent<PlayerControl>();
            cameraEffects = playerDependencies.GetComponent<CameraEffects>();

            // Initialize the Pinned Menu for Quick Access if useFavouritesRadialMenu allows it
            if (useFavouritesRadialMenu && favItemsMenu)
            {
                favItemsMenu.gameObject.SetActive(true);
                favItemsMenu.Initialize(this, weaponController);
                favItemsMenu.gameObject.SetActive(false);
            }

            // Initialize Grid Generator References
            gridGenerator.Initialize(weaponController);

            // Handle On Slot Right Click Actions Subscription
            onSlotRightClick = slotRightClickEvent switch
            {
                SlotRightClickEvent.None => null,
                SlotRightClickEvent.Drop => (slot) => gridGenerator.Operations.DropOutsideInventory(slot),
                SlotRightClickEvent.ContextMenu => ShowContextMenu,
                _ => null,
            };

            // Listeners & Events
            interactManager.Events.OnPerformInteraction.AddListener(HandleFinishedInteraction);
            interactManager.userEvents.onDrop.AddListener(HandleDropHotbar);
            weaponController.Events.OnEquipWeapon.AddListener(HandleEquipWeapon);
            weaponController.settings.userEvents.OnShoot.AddListener(gridGenerator.UpdateBulletCountOnShooting);
            weaponController.settings.userEvents.OnShoot.AddListener(HandleWeaponShootDurability);
            if (playerStats != null) playerStats.Events.OnDamageTaken.AddListener(HandleDamageTakenDurability);
            weaponController.Events.OnFinishReload.AddListener(OnWeaponReload);

            if (inputManager != null)
            {
                inputManager.OnTogglePause += HandleTogglePauseCloseInventory;
                inputManager.OnInventoryOpenPressed += HandleToggleInventoryVisibility;
                inputManager.OnInventoryFavOpenPressed += HandleToggleFavRadialMenu;
            }

            if (closeButton != null) closeButton.onClick.AddListener(HandleCloseInventoryButtonClicked);
            if (allowLootAllChest && lootAllChestButton != null) lootAllChestButton.onClick.AddListener(HandleLootAllChestClicked);

            // If we are not pretending to save the game, GridGenerator should generate the inventory & Hotbar and then populate it, otherwise we'll allow
            // GameDataManager to do it based on the saved & loaded data.
            if (GameDataManager.instance == null || DataPersistenceManager.instance.SelectedProfileIdIsNull())
            {
                gridGenerator.GenerateInventory();
                gridGenerator.PopulateInventory();
                gridGenerator.GenerateHotbar();
            }

            // Auto-initialize any spare / custom slots placed in the UI hierarchy
            onCustomSlotChanged -= HandleCustomSlotChanged;
            onCustomSlotChanged += HandleCustomSlotChanged;
            EnsureCustomSlotsRegistered();

            InputHandler = new InventoryInputHandler(this, invInputActions);
            UIController = new InventoryUIController(this, examinationUI, examinationText);
            NavigationBehaviour = new InventoryProNavigationBehaviour(this);
            NavigationBehaviour.InitializeControllerNavigation(InputHandler);
            AttachmentBehaviour = new InvProAttachmentBehaviour(this);
            InteractionBehaviour = new InventoryInteractionBehaviour(this, weaponController, interactManager);

            UIController.CloseInventory(playSFX: false);
            UIController.ToggleDeviceSpecificUI(false);
        }

        private void HandleCustomSlotChanged(InventorySlot slot)
        {
            if (slot != null && slot.IsCustomSlot) slot.UpdateCustomSlotEffects();
        }

        private void HandleWeaponShootDurability()
        {
            if (weaponController == null || HotbarSlots == null) return;
            int index = weaponController.CurrentWeaponIndex;
            if (index < 0 || index >= HotbarSlots.Length) return;

            InventorySlot activeSlot = HotbarSlots[index];
            if (activeSlot == null || !activeSlot.HasValidItem) return;

            Item_SO item = activeSlot.slotData.item;
            if (item.UseDurability && item.DurabilityMethod == DurabilityReductionMethod.Usage)
            {
                activeSlot.ReduceDurability(1f);
            }
        }

        public void HandleDamageTakenDurability(float damage)
        {
            // Custom slots
            for (int i = customSlots.Count - 1; i >= 0; i--)
            {
                if (i >= customSlots.Count) continue;
                InventorySlot slot = customSlots[i];
                if (slot == null || !slot.HasValidItem) continue;

                Item_SO item = slot.slotData.item;
                if (item.UseDurability && item.DurabilityMethod == DurabilityReductionMethod.TakingDamage)
                {
                    slot.ReduceDurability(damage);
                }
            }
        }

        private void OnDisable()
        {
            onCustomSlotChanged -= HandleCustomSlotChanged;
            if (playerStats != null) playerStats.Events.OnDamageTaken.RemoveListener(HandleDamageTakenDurability);

            if (interactManager != null)
            {
                interactManager.Events.OnPerformInteraction.RemoveListener(HandleFinishedInteraction);
                interactManager.userEvents.onDrop.RemoveListener(HandleDropHotbar);
            }

            if (weaponController != null)
            {
                weaponController.Events.OnEquipWeapon.RemoveListener(HandleEquipWeapon);
                if (gridGenerator != null) weaponController.settings.userEvents.OnShoot.RemoveListener(gridGenerator.UpdateBulletCountOnShooting);
                weaponController.settings.userEvents.OnShoot.RemoveListener(HandleWeaponShootDurability);
                weaponController.Events.OnFinishReload.RemoveListener(OnWeaponReload);
            }

            if (inputManager != null)
            {
                inputManager.OnTogglePause -= HandleTogglePauseCloseInventory;
                inputManager.OnInventoryOpenPressed -= HandleToggleInventoryVisibility;
                inputManager.OnInventoryFavOpenPressed -= HandleToggleFavRadialMenu;
            }

            if (closeButton != null) closeButton.onClick.RemoveListener(HandleCloseInventoryButtonClicked);
            if (lootAllChestButton != null) lootAllChestButton.onClick.RemoveListener(HandleLootAllChestClicked);

            onSlotRightClick = null;

            invInputActions?.Disable();
        }

        private void HandleFinishedInteraction() => InteractionBehaviour?.OnFinishedInteraction();

        private void HandleDropHotbar(Pickeable pick)
        {
            if (gridGenerator != null && weaponController != null)
                gridGenerator.OnDropHotbar(pick, favItemsMenu, weaponController.CurrentWeaponIndex);
        }

        private void HandleEquipWeapon(WeaponIdentification weaponId) => CalculateTotalBullets(overrideCurrentBullets: false);

        private void HandleTogglePauseCloseInventory()
        {
            UIController?.CloseInventory(false);
            UIController?.CloseFavRadialMenu();
        }

        private void HandleToggleInventoryVisibility() => UIController?.ToggleInventoryVisibility();

        private void HandleToggleFavRadialMenu() => UIController?.ToggleFavRadialMenu();

        private void HandleCloseInventoryButtonClicked() => UIController?.CloseInventory();

        private void HandleLootAllChestClicked() => gridGenerator?.ChestBehaviour?.LootAllChest();

        /// <summary>
        /// Re-calculates the available bullets in the inventory that can be used by weapons with limited magazines
        /// </summary>
        /// <param name="overrideCurrentBullets">Overrides current Weapon Magazine with available bullets.</param>
        public void CalculateTotalBullets(bool overrideCurrentBullets) => gridGenerator.CalculateTotalBullets(overrideCurrentBullets, weaponController);

        private void OnWeaponReload() => gridGenerator?.OnWeaponReload(weaponController);

        /// <summary>
        /// <summary>
        /// Used by ContextMenu, separates half of the amounf of the passed slot, and drops it outside the Inventory.
        /// </summary>
        /// <param name="slot">Slot to split</param>
        public void SplitOutsideInventory(InventorySlot slot) => gridGenerator.Operations.SplitOutsideInventory(slot);

        /// <summary>
        /// Drops the item in the specified slot outside the inventory into the world.
        /// </summary>
        /// <param name="slot">Slot to drop</param>
        public void DropOutsideInventory(InventorySlot slot) => gridGenerator.Operations.DropOutsideInventory(slot);

        public void OpenBackpack(InventorySlot slot)
        {
            if (gridGenerator?.BackpackBehaviour == null || slot == null) return;
            gridGenerator.BackpackBehaviour.OpenBackpack(slot);
            if (UIController != null && !UIController.IsInventoryOpen)
                UIController.OpenInventory();
        }

        public void ToggleBackpack(InventorySlot slot)
        {
            if (gridGenerator?.BackpackBehaviour == null || slot == null) return;
            gridGenerator.BackpackBehaviour.ToggleBackpack(slot);
            if (UIController != null && !UIController.IsInventoryOpen && gridGenerator.BackpackBehaviour.IsBackpackOpen)
                UIController.OpenInventory();
        }

        public void CloseBackpack() => gridGenerator?.BackpackBehaviour?.CloseBackpack();

        public void OnBackpackEquipped(InventorySlot slot) => gridGenerator?.BackpackBehaviour?.OnEquipped(slot);

        public void OnBackpackUnequipped(InventorySlot slot) => gridGenerator?.BackpackBehaviour?.OnUnequipped(slot);

        public IInventoryGridService GridService => gridGenerator?.Operations?.GridService;
        public IInventoryCraftingService CraftingService => gridGenerator?.Operations?.CraftingService;
        public IInventoryItemService ItemService => gridGenerator?.Operations?.ItemService;
        public InventoryOperationsBehaviour Operations => gridGenerator?.Operations;

        // Enables Context Menu
        private void ShowContextMenu(InventorySlot anchor) => contextMenu.ShowContextMenu(anchor, (Vector3)Mouse.current?.position.ReadValue() + (Vector3)contextMenuOffset, contextMenuRaycastPadding);

        /// <summary>
        /// Returns whether the currented rotation is oriented or not
        /// </summary>
        /// <returns>Orientation Bool</returns>
        public bool IsOriented() { return itemRotation == 0 || itemRotation == 180; }

        /// <summary>
        /// Adds an item to the Inventory.
        /// </summary>
        public (bool, int) AddItem(Item_SO item, int amount, SlotData[] containedItems = null) => gridGenerator.Operations.AddItemToInventory(item, amount, containedItems);

        /// <summary>
        /// Adds a weapon to the Inventory.
        /// </summary>
        public bool AddWeapon(Weapon_SO weapon, int bulletsLeftInMagazine, int totalBullets) => gridGenerator.Operations.AddWeaponToInventory(weapon, bulletsLeftInMagazine, totalBullets);

        /// <summary>
        /// Checks if the inventory contains enough of a specific item.
        /// </summary>
        public bool HasEnoughOfItem(Item_SO item, int requiredAmount) => gridGenerator.Operations.HasEnoughOfItem(item, requiredAmount);

        /// <summary>
        /// Checks if the inventory contains enough ingredients for a given recipe.
        /// </summary>
        public bool HasEnoughIngredients(Recipe_SO recipe, int craftAmount) => gridGenerator.Operations.HasEnoughIngredients(recipe, craftAmount);

        /// <summary>
        /// Consumes the required ingredients for a given recipe from the inventory.
        /// </summary>
        public bool PayIngredients(Recipe_SO recipe, int craftAmount) => gridGenerator.Operations.PayIngredients(recipe, craftAmount);

        /// <summary>
        /// Deposits a single item from the source slot into the target slot
        /// </summary>
        public bool DepositSingleItem(InventorySlot sourceSlot, InventorySlot targetSlot) => gridGenerator?.Operations?.DepositSingleItem(sourceSlot, targetSlot) ?? false;

        /// <summary>
        /// Access to the modular quick-transfer service
        /// </summary>
        public IInventoryTransferService TransferService => gridGenerator?.Operations?.TransferService;

        /// <summary>
        /// Instantly transfers an item between containers (Shift + Click) based on container priority.
        /// </summary>
        public bool QuickTransfer(InventorySlot sourceSlot) => gridGenerator?.Operations?.QuickTransfer(sourceSlot) ?? false;

        /// <summary>
        /// Returns the transfer target available for the given slot, or null if none
        /// </summary>
        public IInventoryTransferTarget GetTransferTarget(InventorySlot sourceSlot) => gridGenerator?.Operations?.GetTransferTarget(sourceSlot);

        /// <summary>
        /// Returns whether a slot is occupied or not. An item is occupied if it has a valid Anchor or Item assigned.
        /// </summary>
        /// <param name="anchor">Ensure to pass the Anchor of the Slot you want to check if it�s occupied.</param>
        public bool IsSlotOccupied(InventorySlot anchor)
        {
            return anchor != null && anchor.HasValidItem && anchor != draggedSlot?.GetAnchorSlot();
        }

        public bool CanAttachDraggedAttachmentToWeaponSlot() => AttachmentBehaviour?.CanAttachDraggedAttachmentToWeaponSlot() ?? false;

        public bool PreventEndDrag() { return draggedSlot == null || !draggedSlot.HasValidItem;}

#if UNITY_EDITOR
        public void SetRightClickEvent(SlotRightClickEvent slotRightClickEvent) => UIController?.SetRightClickEvent(slotRightClickEvent);
#endif
    }
}