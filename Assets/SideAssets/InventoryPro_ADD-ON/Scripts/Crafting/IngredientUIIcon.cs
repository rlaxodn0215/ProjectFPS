using UnityEngine;
using UnityEngine.UI;
using TMPro; 

namespace cowsins.Inventory
{
    /// <summary>
    /// This Icon helps to visually indicate the required ingredients for a recipe to be crafted. It displays the item and the required Amount.
    /// </summary>
    public class IngredientUIIcon : MonoBehaviour
    {
        [SerializeField, Tooltip("Image that displays the Icon of the Ingredient.")] private Image icon;
        [SerializeField, Tooltip("Text that displays the Required Amount for this ingredient.")] private TextMeshProUGUI quantityText;
        [SerializeField] private Color enoughIngredientsColor = Color.white;
        [SerializeField] private Color insufficientIngredientsColor = Color.red;

        public void SetIngredient(Item_SO item, int quantity, bool hasEnough)
        {
            icon.sprite = item.icon;
            quantityText.text = $"x{quantity}";
            Color color = GatherColor(hasEnough);
            icon.color = color;
            quantityText.color = color;
        }

        private Color GatherColor(bool hasEnough)
        {
            return hasEnough ? enoughIngredientsColor : insufficientIngredientsColor;
        }
    }
}