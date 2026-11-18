using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace cowsins.Inventory
{
    // Used by Trade Button. It allows simple Color highlight variations
    // Inherits from Button
    public class HighlightInteractions : Button, ISelectHandler, IDeselectHandler
    {
        [HideInInspector] public TradeButton tradeButton;

        private Color normalColor;
        private Color highlighColor;

        protected override void Awake()
        {
            // Gather normalColor so we can reset the color if needed
            // Gathers colors from Button´s
            normalColor = this.colors.normalColor;
            highlighColor = this.colors.selectedColor;
            base.Awake();
        }

        public override void OnSelect(BaseEventData eventData)
        {
            tradeButton.PointerEnter();
            ColorBlock colors = this.colors;
            colors.normalColor = highlighColor;
            this.colors = colors;
        }

        public override void OnDeselect(BaseEventData eventData)
        {
            tradeButton.PointerExit();
            ColorBlock colors = this.colors;
            colors.normalColor = normalColor;
            this.colors = colors;
        }
    }
}