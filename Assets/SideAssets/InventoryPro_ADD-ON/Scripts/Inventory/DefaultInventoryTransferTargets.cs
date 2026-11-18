using System;
using UnityEngine;

namespace cowsins.Inventory
{
    public abstract class GridTransferTargetBase : IInventoryTransferTarget
    {
        protected readonly InventoryProManager manager;
        protected readonly InventoryDataModel dataModel;
        protected readonly IInventoryGridService gridService;
        protected readonly IInventoryFeedbackService feedbackService;
        protected readonly InvWeightCalculator weightCalculator;
        protected readonly WeaponController weaponController;

        public abstract string ContainerId { get; }
        public abstract string ActionLabel { get; }
        public abstract int Priority { get; }
        public abstract bool IsAvailable { get; }

        protected abstract SlotType TargetSlotType { get; }
        protected abstract bool DestinationAffectsWeight { get; }
        protected abstract bool IsInventoryGrid { get; }
        protected abstract InventorySlot[,] GetTargetGrid();
        protected abstract InventorySlot FindAvailableTargetSlot(Item_SO item);

        protected GridTransferTargetBase(InventoryProManager manager, InventoryDataModel dataModel, IInventoryGridService gridService,
            IInventoryFeedbackService feedbackService, InvWeightCalculator weightCalculator, WeaponController weaponController)
        {
            this.manager = manager;
            this.dataModel = dataModel;
            this.gridService = gridService;
            this.feedbackService = feedbackService;
            this.weightCalculator = weightCalculator;
            this.weaponController = weaponController;
        }

        public virtual bool CanAccept(InventorySlot sourceSlot)
        {
            if (sourceSlot == null || sourceSlot.Policy.SlotType == TargetSlotType) return false;
            return true;
        }

        public virtual bool TryTransfer(InventorySlot sourceSlot)
        {
            if (sourceSlot == null || !sourceSlot.HasValidItem) return false;
            InventorySlot[,] targetGrid = GetTargetGrid();
            if (targetGrid == null) return false;

            InventorySlot anchor = sourceSlot.GetAnchorSlot();
            Item_SO item = anchor.slotData?.item;
            if (item == null) return false;

            bool anyTransferred = false;
            bool sourceAffectsWeight = anchor.Policy.AffectsPlayerWeight;

            // If stackable, try merging into existing matching target slots
            if (item.maxStack > 1)
            {
                int rows = targetGrid.GetLength(0);
                int cols = targetGrid.GetLength(1);

                for (int r = 0; r < rows; r++)
                {
                    for (int c = 0; c < cols; c++)
                    {
                        InventorySlot targetSlot = targetGrid[r, c];
                        if (targetSlot == null || targetSlot.anchorSlot == null || targetSlot.anchorSlot != targetSlot) continue;

                        if (targetSlot.slotData?.item == item && targetSlot.slotData.amount < item.maxStack)
                        {
                            int space = item.maxStack - targetSlot.slotData.amount;
                            int toTransfer = Math.Min(anchor.slotData.amount, space);

                            targetSlot.slotData.amount += toTransfer;
                            anchor.slotData.amount -= toTransfer;
                            anyTransferred = true;

                            ApplyWeightAdjustment(item, toTransfer, sourceAffectsWeight);

                            dataModel.NotifySlotAreaChanged(targetSlot);
                            targetSlot.Policy.OnSlotModified(targetSlot, manager);

                            if (anchor.slotData.amount <= 0)
                            {
                                gridService.ClearSlotArea(anchor);
                                anchor.Policy.OnItemRemoved(anchor, manager);
                                manager.NavigationBehaviour?.ResetHighlightedSlotsArea(anchor.DefaultBackgroundColor, clearCurrentReferences: true);
                                if (item is BulletTypeIdentifier_SO) manager.CalculateTotalBullets(false);
                                feedbackService.OnItemPlaced();
                                return true;
                            }
                        }
                    }
                }
            }

            // If items remain, find an empty available slot in target grid
            if (anchor.slotData != null && anchor.slotData.amount > 0)
            {
                InventorySlot targetSlot = FindAvailableTargetSlot(item);
                if (targetSlot != null && (targetSlot.slotData == null || targetSlot.slotData.item == null))
                {
                    Vector2Int itemSize = anchor.GetItemSize();
                    if (gridService.ItemFits(targetSlot, itemSize, targetSlot.row, targetSlot.col, IsInventoryGrid))
                    {
                        SlotData transferData = anchor.slotData;
                        gridService.ClearSlotArea(anchor);

                        ApplyWeightAdjustment(item, transferData.amount, sourceAffectsWeight);

                        anchor.Policy.OnItemRemoved(anchor, manager);

                        targetSlot.slotData = transferData;
                        targetSlot.anchorSlot = targetSlot;
                        gridService.ReassignSlots(targetSlot);
                        targetSlot.Policy.OnItemPlaced(targetSlot, manager);

                        manager.NavigationBehaviour?.ResetHighlightedSlotsArea(anchor.DefaultBackgroundColor, clearCurrentReferences: true);
                        if (item is BulletTypeIdentifier_SO) manager.CalculateTotalBullets(false);
                        feedbackService.OnItemPlaced();
                        return true;
                    }
                }
            }

            // Partial transfer remaining
            if (anyTransferred)
            {
                dataModel.NotifySlotAreaChanged(anchor);
                if (item is BulletTypeIdentifier_SO) manager.CalculateTotalBullets(false);
                feedbackService.OnItemPlaced();
                return true;
            }

            return false;
        }

        protected virtual void ApplyWeightAdjustment(Item_SO item, int amount, bool sourceAffectsWeight)
        {
            if (weightCalculator == null || item == null) return;

            // Leaving player weight zone (e.g. Player Inventory -> Chest)
            if (sourceAffectsWeight && !DestinationAffectsWeight)
            {
                weightCalculator.UpdateCurrentWeight(item, -amount);
            }
            // Entering player weight zone (e.g. Chest -> Player Inventory)
            else if (!sourceAffectsWeight && DestinationAffectsWeight)
            {
                weightCalculator.UpdateCurrentWeight(item, amount);
            }
        }
    }

    /// <summary>
    /// Transfer target for open chests. High priority (100) when a chest is open
    /// </summary>
    public sealed class ChestTransferTarget : GridTransferTargetBase
    {
        public override string ContainerId => "Chest";
        public override string ActionLabel => "MOVE TO CHEST";
        public override int Priority => 100;
        public override bool IsAvailable => manager != null && manager._GridGenerator != null && manager._GridGenerator.isChestOpen && dataModel.ChestSlots != null;

        protected override SlotType TargetSlotType => SlotType.Chest;
        protected override bool DestinationAffectsWeight => false;
        protected override bool IsInventoryGrid => false;

        public ChestTransferTarget(InventoryProManager manager, InventoryDataModel dataModel, IInventoryGridService gridService,
            IInventoryFeedbackService feedbackService, InvWeightCalculator weightCalculator, WeaponController weaponController)
            : base(manager, dataModel, gridService, feedbackService, weightCalculator, weaponController)
        {
        }

        protected override InventorySlot[,] GetTargetGrid() => dataModel.ChestSlots;

        protected override InventorySlot FindAvailableTargetSlot(Item_SO item) => gridService.FindAvailableSlotChest(item);

        internal static void ClearHotbarWeapon(InventorySlot hotbarSlot, InventoryProManager manager, WeaponController weaponController)
        {
            HotbarSlotPolicy.ClearHotbarWeapon(hotbarSlot, manager);
        }
    }

    /// <summary>
    /// Transfer target for Player Inventory. Evaluated when items come from chests, hotbar, or future containers.
    /// </summary>
    public sealed class PlayerInventoryTransferTarget : GridTransferTargetBase
    {
        public override string ContainerId => "PlayerInventory";
        public override string ActionLabel => "MOVE TO INVENTORY";
        public override int Priority => 80;
        public override bool IsAvailable => dataModel.InventorySlots != null;

        protected override SlotType TargetSlotType => SlotType.Inventory;
        protected override bool DestinationAffectsWeight => true;
        protected override bool IsInventoryGrid => true;

        public PlayerInventoryTransferTarget(InventoryProManager manager, InventoryDataModel dataModel, IInventoryGridService gridService,
            IInventoryFeedbackService feedbackService, InvWeightCalculator weightCalculator, WeaponController weaponController)
            : base(manager, dataModel, gridService, feedbackService, weightCalculator, weaponController)
        {
        }

        protected override InventorySlot[,] GetTargetGrid() => dataModel.InventorySlots;

        protected override InventorySlot FindAvailableTargetSlot(Item_SO item) => gridService.FindAvailableSlotInventory(item);
    }

    /// <summary>
    /// Transfer target for Player Hotbar. Evaluated for weapons when chest is closed or player inventory is full.
    /// </summary>
    public sealed class HotbarTransferTarget : IInventoryTransferTarget
    {
        private readonly InventoryProManager manager;
        private readonly InventoryDataModel dataModel;
        private readonly IInventoryGridService gridService;
        private readonly IInventoryFeedbackService feedbackService;
        private readonly InvWeightCalculator weightCalculator;
        private readonly WeaponController weaponController;

        public string ContainerId => "Hotbar";
        public string ActionLabel => "MOVE TO HOTBAR";
        public int Priority => 50;
        public bool IsAvailable => dataModel.HotbarSlots != null || manager.HotbarSlots != null;

        public HotbarTransferTarget(InventoryProManager manager, InventoryDataModel dataModel, IInventoryGridService gridService,
            IInventoryFeedbackService feedbackService, InvWeightCalculator weightCalculator, WeaponController weaponController)
        {
            this.manager = manager;
            this.dataModel = dataModel;
            this.gridService = gridService;
            this.feedbackService = feedbackService;
            this.weightCalculator = weightCalculator;
            this.weaponController = weaponController;
        }

        public bool CanAccept(InventorySlot sourceSlot)
        {
            if (sourceSlot == null || sourceSlot.Policy.SlotType == SlotType.Hotbar) return false;
            return sourceSlot.IsItemWeapon;
        }

        public bool TryTransfer(InventorySlot sourceSlot)
        {
            if (sourceSlot == null || !sourceSlot.HasValidItem) return false;

            InventorySlot anchor = sourceSlot.GetAnchorSlot();
            if (!anchor.IsItemWeapon) return false;

            InventorySlot targetHotbar = gridService.FindAvailableSlotHotbar();
            if (targetHotbar == null) return false;

            SlotData transferData = anchor.slotData;
            bool sourceAffectsWeight = anchor.Policy.AffectsPlayerWeight;
            gridService.ClearSlotArea(anchor);
            anchor.Policy.OnItemRemoved(anchor, manager);

            if (sourceAffectsWeight)
                weightCalculator?.UpdateCurrentWeight(transferData.item, -transferData.amount);

            targetHotbar.slotData = transferData;
            targetHotbar.anchorSlot = targetHotbar;
            dataModel.NotifySlotChanged(targetHotbar);
            manager.NavigationBehaviour?.ResetHighlightedSlotsArea(anchor.DefaultBackgroundColor, clearCurrentReferences: true);

            targetHotbar.Policy.OnItemPlaced(targetHotbar, manager);

            feedbackService.OnItemPlaced();
            return true;
        }
    }

    /// <summary>
    /// Transfer target for Custom / Spare slots
    /// </summary>
    public sealed class CustomSlotTransferTarget : IInventoryTransferTarget
    {
        private readonly InventoryProManager manager;
        private readonly InventoryDataModel dataModel;
        private readonly IInventoryFeedbackService feedbackService;
        private readonly InvWeightCalculator weightCalculator;

        public string ContainerId => "CustomSlots";
        public string ActionLabel => "EQUIP";
        public int Priority => 90; // Higher than inventory (80) so shift-clicking in inventory can equip into spare slot if available
        public bool IsAvailable => manager != null && manager.CustomSlots != null && manager.CustomSlots.Count > 0;

        public CustomSlotTransferTarget(InventoryProManager manager, InventoryDataModel dataModel,
            IInventoryFeedbackService feedbackService, InvWeightCalculator weightCalculator)
        {
            this.manager = manager;
            this.dataModel = dataModel;
            this.feedbackService = feedbackService;
            this.weightCalculator = weightCalculator;
        }

        public bool CanAccept(InventorySlot sourceSlot)
        {
            if (sourceSlot == null || sourceSlot.Policy.SlotType == SlotType.Custom || !sourceSlot.HasValidItem) return false;
            Item_SO item = sourceSlot.GetAnchorSlot().slotData?.item;
            if (item == null) return false;

            for (int i = 0; i < manager.CustomSlots.Count; i++)
            {
                var customSlot = manager.CustomSlots[i];
                if (customSlot != null && customSlot.gameObject.activeInHierarchy && !customSlot.HasValidItem && customSlot.AcceptsItem(item))
                    return true;
            }
            return false;
        }

        public bool TryTransfer(InventorySlot sourceSlot)
        {
            if (sourceSlot == null || !sourceSlot.HasValidItem) return false;

            InventorySlot anchor = sourceSlot.GetAnchorSlot();
            Item_SO item = anchor.slotData?.item;
            if (item == null) return false;

            InventorySlot targetCustomSlot = null;
            for (int i = 0; i < manager.CustomSlots.Count; i++)
            {
                var customSlot = manager.CustomSlots[i];
                if (customSlot != null && customSlot.gameObject.activeInHierarchy && !customSlot.HasValidItem && customSlot.AcceptsItem(item))
                {
                    targetCustomSlot = customSlot;
                    break;
                }
            }

            if (targetCustomSlot == null) return false;

            SlotData transferData = new SlotData(anchor.slotData);
            transferData.amount = 1;

            bool sourceAffectsWeight = anchor.Policy.AffectsPlayerWeight;

            anchor.slotData.amount -= 1;
            if (anchor.slotData.amount <= 0)
            {
                manager._GridGenerator.Operations.ClearSlotArea(anchor);
                anchor.Policy.OnItemRemoved(anchor, manager);
            }
            else dataModel.NotifySlotAreaChanged(anchor);

            if (sourceAffectsWeight)
                weightCalculator?.UpdateCurrentWeight(item, -1);

            targetCustomSlot.slotData = transferData;
            targetCustomSlot.anchorSlot = null;
            dataModel.NotifySlotChanged(targetCustomSlot);

            targetCustomSlot.Policy.OnItemPlaced(targetCustomSlot, manager);
            manager.NavigationBehaviour?.ResetHighlightedSlotsArea(anchor.DefaultBackgroundColor, clearCurrentReferences: true);

            feedbackService.OnItemPlaced();
            return true;
        }
    }

    /// <summary>
    /// Transfer target for open Backpack container
    /// </summary>
    public sealed class BackpackTransferTarget : GridTransferTargetBase
    {
        public override string ContainerId => "Backpack";
        public override string ActionLabel => "MOVE TO BACKPACK";
        public override int Priority => 85;
        public override bool IsAvailable => manager != null && manager._GridGenerator != null && manager._GridGenerator.isBackpackOpen && dataModel.BackpackSlots != null && dataModel.BackpackSlots.Length > 0;

        protected override SlotType TargetSlotType => SlotType.Backpack;
        protected override bool DestinationAffectsWeight => true;
        protected override bool IsInventoryGrid => false;

        public BackpackTransferTarget(InventoryProManager manager, InventoryDataModel dataModel, IInventoryGridService gridService, IInventoryFeedbackService feedbackService, InvWeightCalculator weightCalculator, WeaponController weaponController)
            : base(manager, dataModel, gridService, feedbackService, weightCalculator, weaponController)
        {
        }

        protected override InventorySlot[,] GetTargetGrid() => dataModel.BackpackSlots;

        protected override InventorySlot FindAvailableTargetSlot(Item_SO item) => gridService.FindAvailableSlot(item, dataModel.BackpackSlots);

        public override bool CanAccept(InventorySlot sourceSlot)
        {
            if (!base.CanAccept(sourceSlot)) return false;

            // Do not transfer the backpack item itself into its own grid
            if (sourceSlot == dataModel.ActiveBackpackSlot) return false;

            var activeContainer = manager?.ActiveContainerItem;
            if (activeContainer != null && sourceSlot?.slotData?.item != null)
            {
                if (!activeContainer.CanStoreItem(sourceSlot.slotData.item))
                    return false;
            }
            return true;
        }

        protected override void ApplyWeightAdjustment(Item_SO item, int amount, bool sourceAffectsWeight)
        {
            if (weightCalculator == null || item == null) return;

            var container = manager?.ActiveContainerItem;
            float reductionMultiplier = container != null ? (1f - container.WeightReductionPercentage) : 1f;

            if (sourceAffectsWeight && DestinationAffectsWeight)
            {
                // Moving from Player Inventory (100% weight) to Backpack (reduced weight)
                weightCalculator.UpdateCurrentWeight(item, -amount);
                weightCalculator.UpdateCurrentWeight(item, amount, reductionMultiplier);
            }
            else if (!sourceAffectsWeight && DestinationAffectsWeight)
            {
                // Moving from Chest (0% weight) to Backpack
                weightCalculator.UpdateCurrentWeight(item, amount, reductionMultiplier);
            }
        }
    }
}
