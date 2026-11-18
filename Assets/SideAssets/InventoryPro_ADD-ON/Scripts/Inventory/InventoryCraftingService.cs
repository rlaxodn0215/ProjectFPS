using System.Collections.Generic;
using System.Linq;

namespace cowsins.Inventory
{
    /// <summary>
    /// Handles recipe verification, ingredient availability checks, and ingredient payment transactions.
    /// </summary>
    public class InventoryCraftingService : IInventoryCraftingService
    {
        private readonly InventoryDataModel dataModel;
        private readonly InvWeightCalculator weightCalculator;
        private readonly InventoryProManager inventoryManager;

        public InventoryCraftingService(InventoryDataModel dataModel, InvWeightCalculator weightCalculator, InventoryProManager inventoryManager)
        {
            this.dataModel = dataModel;
            this.weightCalculator = weightCalculator;
            this.inventoryManager = inventoryManager;
        }

        public bool HasEnoughIngredients(Recipe_SO recipe, int craftAmount)
        {
            if (recipe == null || recipe.ingredients == null || recipe.ingredients.Length == 0) return true;
            InventorySlot[,] inventorySlots = dataModel.InventorySlots;
            if (inventorySlots == null) return false;

            Dictionary<Item_SO, int> inventoryDict = new Dictionary<Item_SO, int>();

            int rows = inventorySlots.GetLength(0);
            int cols = inventorySlots.GetLength(1);

            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    Item_SO item = inventorySlots[i, j].slotData?.item;
                    if (item != null)
                    {
                        if (!inventoryDict.ContainsKey(item))
                            inventoryDict[item] = 0;

                        inventoryDict[item] += inventorySlots[i, j].slotData.amount;
                    }
                }
            }

            Dictionary<Item_SO, int> recipeDict = recipe.ingredients
                .GroupBy(ing => ing.item)
                .ToDictionary(g => g.Key, g => g.Sum(ing => ing.quantity) * craftAmount);

            foreach (var req in recipeDict)
            {
                if (!inventoryDict.TryGetValue(req.Key, out int count) || count < req.Value)
                    return false;
            }

            return true;
        }

        public bool HasEnoughOfItem(Item_SO item, int requiredAmount)
        {
            InventorySlot[,] inventorySlots = dataModel.InventorySlots;
            if (inventorySlots == null || item == null || requiredAmount <= 0)
                return false;

            int totalFound = 0;
            int rows = inventorySlots.GetLength(0);
            int cols = inventorySlots.GetLength(1);

            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    var slotData = inventorySlots[i, j].slotData;
                    if (slotData?.item == item)
                    {
                        totalFound += slotData.amount;
                        if (totalFound >= requiredAmount)
                            return true;
                    }
                }
            }

            return false;
        }

        public int GetAmountOfItem(Item_SO item)
        {
            InventorySlot[,] inventorySlots = dataModel.InventorySlots;
            if (inventorySlots == null || item == null)
                return 0;

            int totalFound = 0;
            int rows = inventorySlots.GetLength(0);
            int cols = inventorySlots.GetLength(1);

            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    var slotData = inventorySlots[i, j].slotData;
                    if (slotData?.item == item)
                    {
                        totalFound += slotData.amount;
                    }
                }
            }

            return totalFound;
        }

        public bool PayIngredients(Recipe_SO recipe, int craftAmount)
        {
            if (recipe == null || recipe.ingredients == null) return false;
            InventorySlot[,] inventorySlots = dataModel.InventorySlots;
            if (inventorySlots == null) return false;

            Dictionary<Item_SO, int> requiredIngredients = recipe.ingredients
                .GroupBy(ing => ing.item)
                .ToDictionary(g => g.Key, g => g.Sum(ing => ing.quantity) * craftAmount);

            int rows = inventorySlots.GetLength(0);
            int cols = inventorySlots.GetLength(1);

            foreach (var req in requiredIngredients)
            {
                Item_SO requiredItem = req.Key;
                int remainingAmountToRemove = req.Value;

                for (int i = 0; i < rows && remainingAmountToRemove > 0; i++)
                {
                    for (int j = 0; j < cols && remainingAmountToRemove > 0; j++)
                    {
                        var slot = inventorySlots[i, j];

                        if (slot.slotData?.item == requiredItem)
                        {
                            int slotAmount = slot.slotData.amount;

                            if (slotAmount >= remainingAmountToRemove)
                            {
                                slot.slotData.amount -= remainingAmountToRemove;
                                weightCalculator?.UpdateCurrentWeight(requiredItem, -remainingAmountToRemove);
                                if (slot.slotData.amount == 0)
                                {
                                    if (inventoryManager.UseFavouritesRadialMenu) inventoryManager._FavItemsMenu?.UnpinFromFavMenu(slot);
                                    slot.slotData.item = null;
                                    slot.ClearSlot();
                                }
                                else dataModel.NotifySlotChanged(slot);
                                remainingAmountToRemove = 0;
                            }
                            else
                            {
                                remainingAmountToRemove -= slotAmount;
                                weightCalculator?.UpdateCurrentWeight(requiredItem, -slotAmount);

                                if (inventoryManager.UseFavouritesRadialMenu) inventoryManager._FavItemsMenu?.UnpinFromFavMenu(slot);
                                slot.slotData.amount = 0;
                                slot.slotData.item = null;
                                slot.ClearSlot();
                            }
                        }
                    }
                }

                if (remainingAmountToRemove > 0)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
