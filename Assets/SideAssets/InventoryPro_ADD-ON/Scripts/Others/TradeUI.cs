using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace cowsins.Inventory
{
    /// <summary>
    /// Shop UI & Crafting UI are TradeUIs, they both inherit from Trade UI as they share similarities to avoid extra code.
    /// </summary>
    public class TradeUI : MonoBehaviour
    {
        [SerializeField, Title("TRADE UI"), Tooltip("On pressing this button, trade UI will be closed. This action is assigned automatically and requires no extra set-up.")] 
        protected Button closeButton;
        [SerializeField, Tooltip("All buttons will be spawned under buttonsParent, generally as a Grid or Horizontal/Vertical Layout")] protected Transform buttonsParent;
        [SerializeField, Tooltip("Object that contains iconImage & nameText. This object is enabled on hovering a valid button.")] protected GameObject selectedItemInformation;
        [SerializeField, Tooltip("Displays Icon for the highlighed Item")] protected Image iconImage;
        [SerializeField, Tooltip("Displays Name for the highlighed Item")] protected TextMeshProUGUI nameText;
        [SerializeField, Tooltip("Displays Current Currency")] protected TextMeshProUGUI coinsText;

        protected TradeButton tradeButton;

        // Individually Initialize all Buttons in Trade UI.
        protected void InitializeButtons<T>(T[] items, Func<T, bool> isValidItem, Action<T, TradeButton> initializeButton, TradeButton tradeButton, ProgressionManager pm)
        {
            bool isFirstButton = true;
            this.tradeButton = tradeButton;

            foreach (T item in items)
            {
                if (!isValidItem(item)) continue;

                TradeButton btn = Instantiate(this.tradeButton, buttonsParent);
                if (btn.transform is RectTransform rt)
                    rt.sizeDelta = new Vector2(300, 300);
                initializeButton(item, btn);

                if (isFirstButton)
                {
                    var buttonComponent = btn.ActionButton;
                    if (buttonComponent != null && EventSystem.current && Gamepad.current != null)
                    {
                        EventSystem.current.firstSelectedGameObject = buttonComponent.gameObject;
                        EventSystem.current.SetSelectedGameObject(buttonComponent.gameObject);
                    }
                    isFirstButton = false;
                }
            }

            // Coins Text is also initialized at the end, based on the amount of coins the player has.
            // This is updated each time the player interacts with the Trade UI.
            if (coinsText) coinsText.text = pm.coinService.coins.ToString();
        }
    }
}
