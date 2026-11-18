using System.Collections;
using UnityEngine;
using UnityEngine.UI; 

namespace cowsins.Inventory
{
    public class CraftButton : TradeButton
    {
        [SerializeField, Title("CRAFT BUTTON", upMargin = 10)] private string craftCallToAction = "Craft", collectCallToAction = "Collect";
        [SerializeField, Tooltip("Fills up to display the current progress of a Crafting Process.")] private Image craftingProgressIndicator;

        // GETTERS
        private Recipe_SO recipeSO;
        public Recipe_SO RecipeSO => recipeSO;

        // INTERNAL USE
        private Color craftingProgressIndicatorStartColor;
        private CraftingUI craftingUI;
        private bool hasEnoughIngredients;
        private int totalCrafted = 0;

        /// <summary>
        /// Initializes a Craft Button, so it can craft the specified Recipe.
        /// </summary>
        /// <param name="craftingUI"></param>
        /// <param name="recipeSO"></param>
        /// <param name="craftingBench"></param>
        /// <param name="enoughIngredients"></param>
        /// <param name="craftingProcess"></param>
        public void Initialize(CraftingUI craftingUI, Recipe_SO recipeSO, TradeHub craftingBench, bool enoughIngredients, CraftingProcess craftingProcess)
        {
            this.craftingUI = craftingUI;
            this.tradeHub = craftingBench;
            this.recipeSO = recipeSO;
            // Whether the player has enough ingredients to craft this recipe or not is calculated on interacting with the Crafting Bench, and it�s then passed to each button
            this.hasEnoughIngredients = enoughIngredients;
            this.actionButton.tradeButton = this;

            // UPDATE UI to reflect the current Recipe visually
            iconImage.sprite = recipeSO.result.icon;
            nameText.text = recipeSO.result._name;

            // Check if the item is locked based on level requirements and its availability
            HandleLockedItem(true, recipeSO.minLevelRequired);

            // Update Progress Visuals initially
            craftingProgressIndicatorStartColor = craftingProgressIndicator.color;
            UpdateProgressIndicatorFill(craftingProcess.craftingProgress);

            // Handle Current Amount and Items to be Crafted
            this.totalCrafted = craftingProcess.totalCrafted;
            this.currentAmount = craftingProcess.remainingAmountToCraft;
            UpdateCurrentAmount(currentAmount == 0 ? 1 : currentAmount);

            callToActionText.text =
                IsCraftingOrWaitingForCollect(craftingProcess) ? $"{collectCallToAction} x{totalCrafted}"
                : (enoughIngredients ? craftCallToAction : $"<color=#{ColorUtility.ToHtmlStringRGB(Color.red)}> {craftCallToAction}</color>");
        }

        // This methos is called when the Craft/Collect button is clicked.
        protected override void HandleActionClick()
        {
            if (!IsCraftingOrWaitingForCollect(craftingUI.GetCraftingProcess(recipeSO)))
            {
                // If it is not crafting and there are no items to collect, and the player has enough ingredients: 
                if (hasEnoughIngredients)
                {
                    // Handle Trade Decline
                    if (!hasEnoughIngredients || lockedContainer.activeSelf)
                    {
                        tradeHub.Events.onDeclinedTrade?.Invoke();
                        return;
                    }

                    StartCrafting();
                }
                else
                    SoundManager.Instance.PlaySound(tradeHub.ForbiddenTradeSFX, 0, 0, false);
            }
            else CollectCraftedItem(); // It�s collectable
        }

        public override void PointerEnter()
        {
            craftingUI.UpdateSelectedItemUI(recipeSO, currentAmount);
            base.PointerEnter();
        }
        public override void PointerExit()
        {
            craftingUI.UpdateSelectedItemUI(null, 1);
            base.PointerExit();

        }

        // Called when the player increases or decreases the amount desired to be crafted.
        protected override void UpdateCurrentAmount(int currentAmount)
        {
            if (craftingUI.IsCrafting(recipeSO))
            {
                amountInputField.text = this.currentAmount.ToString();
                ToggleAmountHandlingButtons(false);
                return;
            }

            this.currentAmount = currentAmount;
            amountInputField.text = this.currentAmount.ToString();

            if (totalCrafted <= 0)
            {
                ToggleAmountHandlingButtons(true);

                if (currentAmount <= 1)
                {
                    currentAmount = 1;
                    previousAmountButton.gameObject.SetActive(false);
                }
                else previousAmountButton.gameObject.SetActive(true);
            }

            UpdateActionText();
            craftingUI.UpdateIngredientDisplay(recipeSO, currentAmount);
        }

        // Override Amount UI 
        public void SetCurrentAmountText() => amountInputField.text = this.currentAmount.ToString();

        // Called when the player increases the amount desired to be crafted.
        protected override void HandleNextAmountClick() => UpdateCurrentAmount(currentAmount + 1);

        public void StartCrafting()
        {
            // If there is less than 1 item to be crafted, then there is nothing to craft. Stop here.
            if (currentAmount < 1) return;
            
            // Update Button UI
            callToActionText.text = $"{collectCallToAction} x{totalCrafted}";
            craftingUI.CraftItem(recipeSO, currentAmount);
            ToggleAmountHandlingButtons(false);

            // Start Crafting
            StartCoroutine(WaitForItemCraft());

            SoundManager.Instance.PlaySound(((CraftingBench)tradeHub).StartCraftingSFX, 0, 0, false);
        }

        private IEnumerator WaitForItemCraft()
        {
            // Keep looping until there are no more items to craft
            while (currentAmount > 0)
            {
                float elapsedTime = 0f;
                // Added extra .2f seconds for those Items whose timeToCraft equals 0 for feedback improvements
                float timeToWait = recipeSO.timeToCraft > 0 ? recipeSO.timeToCraft : 0.2f;

                // Update UI to indicate the current progress of the Crafting Process
                UpdateProgressIndicator(0.7f);

                // Wait until the crafting loop/process for this item is finished
                while (elapsedTime < timeToWait)
                {
                    elapsedTime += Time.deltaTime;
                    yield return null;
                }

                // Finished this Crafting Process
                totalCrafted++;
                currentAmount--;
                UpdateCollectText(totalCrafted);
                FinishCrafting();
            }

            // Ensure no more items are left to be crafted.
            currentAmount = 0;
            amountInputField.text = "0";
        }


        public void FinishCrafting()
        {
            amountInputField.text = this.currentAmount.ToString();
            callToActionText.text = $"{collectCallToAction} x{totalCrafted}";
        }

        private void CollectCraftedItem()
        {
            // Ensure there are items to be collected before collecting.
            totalCrafted = craftingUI.GetCraftingProcess(recipeSO).totalCrafted;
            if (totalCrafted <= 0) return;

            // Returns whether the items could be added and the amount that could not be added ( if applicable )
            (bool success, int amount) = AddItemToInventory(recipeSO.result, totalCrafted * recipeSO.resultAmount);

            // If not every item was added to the inventory, there was not enough space in the Inventory
            if (!success && amount > 0 && ToastManager.Instance.ShowToastOnInsufficientSpace)
            {
                ToastManager.Instance.ShowToast(ToastManager.Instance.InventoryIsFullMsg);
            }

            // if all items were added to the inventory: 
            if (ToastManager.Instance.ShowToastOnTrade && success)
            {
                string message = ToastManager.Instance.ShowAmountTraded ? $"x{totalCrafted} " : "";
                message += $"{recipeSO.result._name} {ToastManager.Instance.CollectedMsg}";
                ToastManager.Instance.ShowToast(message);
            }
            totalCrafted = amount;

            // Take into account that we can collect while crafting, so we need to check if we are crafting or not before collecting.
            if (!craftingUI.IsCrafting(recipeSO))
            {
                if (totalCrafted <= 0 && success)
                {
                    UpdateProgressIndicator(0);
                    UpdateActionText();
                    currentAmount = 1;
                    amountInputField.text = this.currentAmount.ToString();
                }
                else callToActionText.text = $"{collectCallToAction} x{amount}";

                ToggleAmountHandlingButtons(true);
            }
            else
            {
                callToActionText.text = $"{collectCallToAction} x{amount}";
            }

            // UI UPDATES + SFX
            craftingUI.UpdateCraftingProcess(recipeSO, totalCrafted);

            if (success)
            {
                SoundManager.Instance.PlaySound(tradeHub.SuccessfulTradeSFX, 0, 0, false);
                craftingUI.RefreshRecipeButtons();
            }
            else SoundManager.Instance.PlaySound(tradeHub.ForbiddenTradeSFX, 0, 0, false);
        }
        
        // Enable or disable the Buttons that the player can click to increase or decrease amount to be crafted.
        // This is generally called when started crafting, to avoid the player modifying the amount of items to craft without paying the ingredients.
        private void ToggleAmountHandlingButtons(bool value)
        {
            nextAmountButton.gameObject.SetActive(value);
            previousAmountButton.gameObject.SetActive(false);
            amountInputField.interactable = value;
        }

        private void UpdateActionText()
        {
            hasEnoughIngredients = InventoryProManager.instance.HasEnoughIngredients(recipeSO, currentAmount);

            callToActionText.text = hasEnoughIngredients ?
                craftCallToAction :
                $"<color=#{ColorUtility.ToHtmlStringRGB(Color.red)}> {craftCallToAction}</color>";
        }

        private void UpdateProgressIndicator(float alpha)
        {
            craftingProgressIndicator.color = new Color(
                    craftingProgressIndicatorStartColor.r,
                    craftingProgressIndicatorStartColor.g,
                    craftingProgressIndicatorStartColor.b,
                    alpha);
        }

        public void UpdateProgressIndicatorFill(float progress) => craftingProgressIndicator.fillAmount = progress;

        public void UpdateCollectText(int craftedAmount)
        {
            if (craftedAmount > 0) callToActionText.text = $"{collectCallToAction} x{craftedAmount}";
            else callToActionText.text = craftCallToAction;
        }

        private bool IsCraftingOrWaitingForCollect(CraftingProcess craftingProcess) { return craftingProcess.totalCrafted > 0 || craftingProcess.isCrafting; }
    }
}