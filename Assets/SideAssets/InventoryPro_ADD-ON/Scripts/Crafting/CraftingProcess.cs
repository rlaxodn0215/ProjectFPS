using UnityEngine;

namespace cowsins.Inventory
{
    /// <summary>
    /// Represents a crafting process where certain items require time to be crafted.
    /// This class tracks the progress, amount remaining, and whether the crafting is in progress.
    /// </summary>

    [System.Serializable]
    public class CraftingProcess
    {
        public Recipe_SO recipe;
        // Amount that´s still left to Craft
        public int remainingAmountToCraft;
        // Amount that´s ready to be collected.
        public int totalCrafted;
        public float craftingProgress;
        public bool isCrafting;
        public Coroutine craftingCoroutine;

        // Crafting Process Constructor
        public CraftingProcess(Recipe_SO recipe, int remainingAmountToCraft)
        {
            this.recipe = recipe;
            this.remainingAmountToCraft = remainingAmountToCraft;
            this.totalCrafted = 0;
            this.craftingProgress = 0f;
            this.isCrafting = false;
            this.craftingCoroutine = null;
        }
    }
}