using cowsins.SaveLoad;
using UnityEngine;
using System.Linq; 
using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace cowsins.Inventory
{
    /// <summary>
    /// GridGenerator Handles Generation, Population and Management of all grids in Inventory Pro Manager Add-On, including Inventory + Chests. 
    /// </summary>
    public class GridGenerator : MonoBehaviour
    {
        [SerializeField, Tooltip("Reference to the InventoryProManager (parent of GridGenerator)")] private InventoryProManager inventoryManager;
        [SerializeField, Tooltip("Stores Initial Slot Data Information & Customization about the Player Inventory.")] private InventoryGridData_SO inventoryGridData;
        [SerializeField, Tooltip("Transform that stores all Hotbar Inventory Slots.")] private Transform hotbarParent;
        [SerializeField, Tooltip("Transform that stores all Inventory Slots from the Inventory.")] private Transform buttonParent;
        [SerializeField, Tooltip("Transform that stores all Chest Inventory Slots. Chests Grid are created dynamically.")] private Transform chestParent;
        [SerializeField, Tooltip("Transform that stores all Backpack Inventory Slots.")] private Transform backpackParent;
        [SerializeField, Tooltip("Horizontal Gap/Spacing between Inventory Slots")] private float gapX = 120f;
        [SerializeField, Tooltip("Vertical Gap/Spacing between Inventory Slots")] private float gapY = 120f;
        [SerializeField, Tooltip("Hotbar Spacing from the Inventory")] private Padding hotbarPadding;
        [SerializeField, Tooltip("Minimum multiplier applied when fully encumbered.")] private float minWeightMultiplier = 0.2f;

        // REFERENCES 
        private WeaponController weaponController;
        public InventoryDataModel DataModel { get; private set; }

        // BEHAVIOURS
        public InvWeightCalculator WeightCalculator { get; private set; }
        public InvChestBehaviour ChestBehaviour { get; private set; }
        public InvBackpackBehaviour BackpackBehaviour { get; private set; }
        public InventoryProGeneratorBehaviour Generator { get; private set; }
        public InventoryOperationsBehaviour Operations { get; private set; }
        public IInventoryGridService GridService => Operations?.GridService;
        public IInventoryCraftingService CraftingService => Operations?.CraftingService;
        public IInventoryItemService ItemService => Operations?.ItemService;
        public InvBulletsHandlingBehaviour BulletsHandler { get; private set; }

        // CONTROLLER NAVIGATION (Handled in InputHandler)
        [SerializeField, HideInInspector] private int _selectedControllerSlotRow = 0;
        [SerializeField, HideInInspector] private int _selectedControllerSlotColumn = 0;

        public int selectedControllerSlotRow
        {
            get => inventoryManager?.InputHandler != null ? inventoryManager.InputHandler.SelectedRow : _selectedControllerSlotRow;
            set
            {
                _selectedControllerSlotRow = value;
                if (inventoryManager?.InputHandler != null) inventoryManager.InputHandler.SelectedRow = value;
            }
        }

        public int selectedControllerSlotColumn
        {
            get => inventoryManager?.InputHandler != null ? inventoryManager.InputHandler.SelectedColumn : _selectedControllerSlotColumn;
            set
            {
                _selectedControllerSlotColumn = value;
                if (inventoryManager?.InputHandler != null) inventoryManager.InputHandler.SelectedColumn = value;
            }
        }

        // GETTERS
        public Transform HotbarParent => hotbarParent;
        public Transform ChestParent => ChestBehaviour != null ? ChestBehaviour.ChestParent : chestParent;
        public InventorySlot[,] InventorySlots => DataModel?.InventorySlots;
        public InventorySlot[] HotbarSlots => DataModel?.HotbarSlots;
        public InventorySlot[,] ChestSlots => DataModel?.ChestSlots;
        public InventorySlot[,] BackpackSlots => DataModel?.BackpackSlots;
        public Chest CachedChest => DataModel?.CachedChest;
        public bool isChestOpen => ChestBehaviour != null && ChestBehaviour.IsChestOpen;
        public Transform BackpackParent => BackpackBehaviour != null ? BackpackBehaviour.BackpackParent : backpackParent;
        public bool isBackpackOpen => BackpackBehaviour != null && BackpackBehaviour.IsBackpackOpen;
        public InventoryGridData_SO _InventoryGridData_SO => inventoryGridData;
        public int Rows => DataModel != null && DataModel.Rows > 0 ? DataModel.Rows : inventoryGridData.rows;
        public int Columns => DataModel != null && DataModel.Columns > 0 ? DataModel.Columns : inventoryGridData.columns;
        public float GapX => gapX;
        public float GapY => gapY;
        public Padding HotbarPadding => hotbarPadding;

        /// <summary>
        /// Returns true if the given slot fits in the target Slot based on its size. This is used for Tetris Inventories.
        /// </summary>
        private bool ItemFits(InventorySlot slot, Vector2Int itemSize, int row, int col, bool isInventorySlot) => Operations.ItemFits(slot, itemSize, row, col, isInventorySlot);

        /// <summary>
        /// Populate Player Inventory with given Data, generally used by GameDataManager with Loaded Data.
        /// </summary>
        /// <param name="loadedData"></param>
        public void PopulateInventoryWithSerializableInventoryData(GameData.SerializableInventoryData[] loadedData)
        {
            Generator.PopulateInventoryWithSerializableInventoryData(DataModel.InventorySlots, loadedData, WeightCalculator);

            // After populating the entire Inventory, Update the Size of the Slots for Tetris Inventor.
            UpdateSlotsSize(DataModel.InventorySlots);
            Generator.UpdateHotbarSlotsVisuals(DataModel.HotbarSlots);
        }

        /// <summary>
        /// Generates the inventory grid, optionally with custom dimensions.
        /// </summary>
        public void GenerateInventory(int? customRows = null, int? customColumns = null)
        {
            int rows = customRows.HasValue && customRows.Value > 0 ? customRows.Value : inventoryGridData.rows;
            int cols = customColumns.HasValue && customColumns.Value > 0 ? customColumns.Value : inventoryGridData.columns;
            DataModel.SetDimensions(rows, cols);
            WeightCalculator?.UpdateDimensions(rows, cols);
            DataModel.InventorySlots = Generator.GenerateInventory(rows, cols);
        }

        /// <summary>
        /// Expands the player's inventory grid by adding extra rows and/or extra columns at runtime.
        /// Existing items remain untouched in their current slots.
        /// </summary>
        public bool ExpandInventory(int extraRows, int extraColumns)
        {
            if (DataModel == null || DataModel.InventorySlots == null) return false;
            if (extraRows <= 0 && extraColumns <= 0) return false;

            int currentRows = DataModel.Rows > 0 ? DataModel.Rows : Rows;
            int currentCols = DataModel.Columns > 0 ? DataModel.Columns : Columns;
            int newRows = currentRows + Mathf.Max(0, extraRows);
            int newCols = currentCols + Mathf.Max(0, extraColumns);

            InventorySlot[,] newSlots = Generator.ExpandInventory(
                DataModel.InventorySlots,
                currentRows,
                currentCols,
                newRows,
                newCols
            );

            if (newSlots == null) return false;

            DataModel.SetDimensions(newRows, newCols);
            DataModel.InventorySlots = newSlots;
            WeightCalculator?.UpdateDimensions(newRows, newCols);
            DataModel.NotifyGridChanged();
            return true;
        }

        /// <summary>
        /// Sets the inventory dimensions to target values (only allows expansion, not shrinking).
        /// </summary>
        public bool SetInventoryDimensions(int targetRows, int targetColumns)
        {
            if (DataModel == null || DataModel.InventorySlots == null) return false;
            int currentRows = DataModel.Rows > 0 ? DataModel.Rows : Rows;
            int currentCols = DataModel.Columns > 0 ? DataModel.Columns : Columns;

            if (targetRows < currentRows || targetColumns < currentCols)
                return false;

            int extraRows = targetRows - currentRows;
            int extraCols = targetColumns - currentCols;
            if (extraRows == 0 && extraCols == 0) return true;

            return ExpandInventory(extraRows, extraCols);
        }

        /// <summary>
        /// Generates the hotbar slots
        /// </summary>
        public void GenerateHotbar() => DataModel.HotbarSlots = Generator.GenerateHotbar();

        /// <summary>
        /// Populates pLayer Inventory with Initial Data from the Inventory Grid Data. This is called when a game is started for the first time or if GameDataManager 
        /// doe snopt exist in the scene.
        /// </summary>
        public void PopulateInventory()
        {
            Generator.PopulateInventory(DataModel.InventorySlots, WeightCalculator);

            // After populating the entire Inventory, Update the Size of the Slots for Tetris Inventor.
            UpdateSlotsSize(DataModel.InventorySlots);
        }

        private void UpdateSlotsSize(InventorySlot[,] slotsArray) => Operations.UpdateSlotsSize(slotsArray);

        public void CalculateTotalBullets(bool overrideCurrentBullets, WeaponController weaponController) => BulletsHandler?.CalculateTotalBullets(overrideCurrentBullets, weaponController, DataModel.HotbarSlots);

        /// <summary>
        /// Updates the Hotbar slot's bullets left in magazine when a shot is fired
        /// </summary>
        public void UpdateBulletCountOnShooting() => BulletsHandler?.UpdateBulletCountOnShooting(weaponController, DataModel.HotbarSlots);

        public void OnWeaponReload(WeaponController weaponController) => BulletsHandler?.OnWeaponReload(weaponController, DataModel.HotbarSlots);

        public void UpdateBulletsHotbar(InventorySlot draggedSlot, InventorySlot highlightedSlot, WeaponController weaponController) => BulletsHandler?.UpdateBulletsHotbar(draggedSlot, highlightedSlot, weaponController);

        // Converts an index into a tuple representing the corresponding row and column in a grid.
        private (int row, int col) GetRowAndColumnOutOfIndex(int index) => Operations.GetRowAndColumnOutOfIndex(index);

        /// <summary>
        /// Updates GridGenerator HotbarSlot information so it coordinates with inventory in WeaponController
        /// </summary>
        public void UpdateHotbarSlot(int slotIndex, AttachmentIdentifier_SO Barrel, AttachmentIdentifier_SO Scope, AttachmentIdentifier_SO Stock, AttachmentIdentifier_SO Grip,
            AttachmentIdentifier_SO Magazine, AttachmentIdentifier_SO Flashlight, AttachmentIdentifier_SO Laser)
        {
            InventorySlot currentInventorySlot = DataModel.HotbarSlots[slotIndex];
            currentInventorySlot.slotData.item = weaponController.Inventory[slotIndex].weapon;
            currentInventorySlot.slotData.amount = 1;
            currentInventorySlot.UpdateSlotGraphics();

            currentInventorySlot.slotData.bulletsLeftInMagazine = weaponController.Inventory[slotIndex].bulletsLeftInMagazine;
            currentInventorySlot.slotData.totalBullets = weaponController.Inventory[slotIndex].totalBullets;
            currentInventorySlot.slotData.barrel = Barrel;
            currentInventorySlot.slotData.scope = Scope;
            currentInventorySlot.slotData.stock = Stock;
            currentInventorySlot.slotData.grip = Grip;
            currentInventorySlot.slotData.magazine = Magazine;
            currentInventorySlot.slotData.flashlight = Flashlight;
            currentInventorySlot.slotData.laser = Laser;
        }

        /// <summary>
        /// Called when an Item is dropped outside the Inventory from the hotbar.
        /// Updates the HotbarSlots visuals to ensure it is up-to-date with inventory in WeaponController.
        /// </summary>
        public void OnDropHotbar(Pickeable pick, FavItemsMenu favItemsMenu, int currentWeaponIndex)
        {
            InventorySlot hotbarSlot = DataModel.HotbarSlots[currentWeaponIndex];
            favItemsMenu?.UnpinFromFavMenu(hotbarSlot);
            hotbarSlot.slotData = new SlotData();
            hotbarSlot.UpdateSlotGraphics();

#if SAVE_LOAD_ADD_ON
            if (cowsins.SaveLoad.GameDataManager.instance != null) cowsins.SaveLoad.GameDataManager.instance.OnDropHotbar(pick);
#endif
        }

        /// <summary>
        /// Returns whether the style of the Inventory is Grid or Tetris. True if Grid is returned.
        /// </summary>
        public bool IsGridInventory() { return inventoryManager._InventoryStyle == InventoryProManager.InventoryStyle.Grid; }

        /// <summary>
        /// Initializes Basic References
        /// </summary>
        public void Initialize(WeaponController controller, InventoryDataModel existingDataModel = null, IInventoryFeedbackService feedbackService = null)
        {
            weaponController = controller;
            Generator = new InventoryProGeneratorBehaviour(inventoryManager, inventoryGridData, hotbarParent, buttonParent, chestParent, gapX, gapY, hotbarPadding);
            Generator.Initialize(weaponController);
            var playerMultipliers = weaponController.GetComponent<PlayerMultipliers>();
            DataModel = existingDataModel ?? new InventoryDataModel(Rows, Columns);
            WeightCalculator = new InvWeightCalculator(playerMultipliers, Rows, Columns, minWeightMultiplier);
            Operations = new InventoryOperationsBehaviour(inventoryManager, inventoryGridData, WeightCalculator, weaponController, DataModel, feedbackService: feedbackService);
            BulletsHandler = new InvBulletsHandlingBehaviour(WeightCalculator, DataModel, Operations.GridService, inventoryManager);
            ChestBehaviour = new InvChestBehaviour(inventoryManager, weaponController, chestParent, this, UpdateSlotsSize, DataModel);
            BackpackBehaviour = new InvBackpackBehaviour(inventoryManager, weaponController, BackpackParent, this, UpdateSlotsSize, DataModel);
        }
    }
}