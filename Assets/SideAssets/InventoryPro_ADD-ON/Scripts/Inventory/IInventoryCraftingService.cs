namespace cowsins.Inventory
{
    /// <summary>
    /// Responsible for validating crafting recipe requirements and consuming ingredients.
    /// </summary>
    public interface IInventoryCraftingService
    {
        bool HasEnoughIngredients(Recipe_SO recipe, int craftAmount);
        bool HasEnoughOfItem(Item_SO item, int requiredAmount);
        int GetAmountOfItem(Item_SO item);
        bool PayIngredients(Recipe_SO recipe, int craftAmount);
    }
}
