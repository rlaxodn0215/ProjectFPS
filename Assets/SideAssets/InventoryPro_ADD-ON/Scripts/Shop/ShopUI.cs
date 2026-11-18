using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace cowsins.Inventory
{
    public class ShopUI : TradeUI
    {
        [SerializeField, Tooltip("Buttons to instantiate in the Shop.")] private ShopButton shopButton;
        [SerializeField, Tooltip("When purchasing Weapons, slidersContainer is enabled to display Weapons Statistics.")] private GameObject slidersContainer;
        [SerializeField, Tooltip("Contained in slidersContainer, when purchasing weapons each slider gets filled to represent their respective statistics.")] 
        private Slider damageSlider, fireRateSlider, magazineSizeSlider, speedModifierSlider;
        private Shop shop;
        private readonly List<ShopButton> buttonList = new List<ShopButton>();
        private Action closeAction;

        public void InitializeUI(ShopItemData[] shopItems, TradeHub shop, Action closeShop)
        {
            this.shop = (Shop)shop;
            this.closeAction = closeShop;

            buttonList.Clear();
            InitializeButtons(shopItems,
                item => item.item != null,
                (item, btn) => {
                    var sb = (ShopButton)btn;
                    sb.Initialize(this, item, shop, item.amountAvailable > 0);
                    buttonList.Add(sb);
                },
                shopButton,
                shop.progressionManager
            );

            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(HandleCloseClicked);
                closeButton.onClick.AddListener(HandleCloseClicked);
            }
        }

        private void OnDestroy()
        {
            if (closeButton != null) closeButton.onClick.RemoveListener(HandleCloseClicked);
        }

        private void HandleCloseClicked() => closeAction?.Invoke();

        /// <summary>
        /// Highlights the Item and displays its information in the shop.
        /// </summary>
        /// <param name="item"></param>
        public void UpdateHighlightedItemUI(Item_SO item)
        {
            if(item == null)
            {
                slidersContainer.SetActive(false);
                iconImage.gameObject.SetActive(false);
                nameText.gameObject.SetActive(false);
                return;
            }
           
            iconImage.gameObject.SetActive(true);
            nameText.gameObject.SetActive(true);

            // Only show the SLiders Container if the item to purchase is a Weapon, then fill the sliders accordingly.
            if (item is Weapon_SO weapon)
            {
                slidersContainer.SetActive(true);
                damageSlider.value = weapon.shootStyle == ShootStyle.Melee ? weapon.damagePerHit : weapon.damagePerBullet;
                fireRateSlider.value = weapon.fireRate;
                magazineSizeSlider.value = weapon.shootStyle == ShootStyle.Melee ? 1 : weapon.magazineSize;
                speedModifierSlider.value = weapon.applyWeight ? 1 / weapon.weightMultiplier : 1;
            }
            else
            {
                slidersContainer.SetActive(false);
            }

            iconImage.sprite = item?.icon;
            nameText.text = item._name;
        }

        /// <summary>
        /// Re-initializes all shop items. This is generally called after a purchase has occured so the shop displays if the Player has enough resources or not after the purchase
        /// </summary>
        /// <param name="purchasedShopButton"></param>
        public void RefreshShopItems(ShopButton purchasedShopButton)
        {
            coinsText.text = shop.progressionManager.coinService.coins.ToString();
            for (int i = 0; i < buttonList.Count; i++)
            {
                ShopButton shopButton = buttonList[i];
                if (shopButton == null) continue;

                if (shopButton == purchasedShopButton && shop.decreaseAvailableAmountOnPurchase)
                {
                    shopButton.ShopItemData.amountAvailable -= 1;
                }
                shopButton.Initialize(this, shopButton.ShopItemData, purchasedShopButton._TradeHub, shopButton.ShopItemData.amountAvailable > 0);
            }
        }
    }
}

