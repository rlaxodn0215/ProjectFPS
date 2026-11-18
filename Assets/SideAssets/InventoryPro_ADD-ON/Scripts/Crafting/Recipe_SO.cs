using UnityEngine;

namespace cowsins.Inventory
{
    /// <summary>
    /// Recipe_SO contains all the required information for a recipe.
    /// </summary>
    [System.Serializable]
    public class Recipe_SO : ScriptableObject
    {
        /// <summary>
        /// Ingredients are those items required to craft the result of a recipe.
        /// </summary>
        [System.Serializable]
        public class Ingredient
        {
            public Item_SO item;
            public int quantity;

            public Ingredient(Item_SO item, int quantity)
            {
                this.item = item;
                this.quantity = quantity;
            }
        }
        public Ingredient[] ingredients;

        public Item_SO result;

        [Range(1, 99)] public int resultAmount = 1;

        public float timeToCraft;

        public int minLevelRequired;
    }
}