using UnityEngine;
using System.Collections.Generic;
using System.Collections;

namespace cowsins.Inventory
{
    /// <summary>
    /// Crafting Bench allows the Player to "Trade" Resources (Items) for other Items.
    /// </summary>
    public partial class CraftingBench : TradeHub
    {
        [SerializeField, Title("CRAFTING BENCH", upMargin = 10), Tooltip("UI Associated to this Trade HUB. It will display all Recipes.")] private CraftingUI craftingUIPrefab;
        [SerializeField, Tooltip("Contains all the Recipes that can be crafted for this specific Crafting Bench.")] private Recipe_SO[] availableRecipes;
        [SerializeField] private AudioClip startCraftingSFX;

        // GETTERS
        public AudioClip StartCraftingSFX => startCraftingSFX;

        private CraftingUI craftUIInstance;
        public CraftingUI CraftUIInstance => craftUIInstance;

        private List<CraftingProcess> craftingProcesses = new List<CraftingProcess>();

        public List<CraftingProcess> CraftingProcesses => craftingProcesses;

        // When the game starts, populate the Crafting processes based on the available Recipes.
        private void Awake()
        {
            foreach (var recipe in availableRecipes)
            {
                CraftingProcess craftingProcess = new CraftingProcess(recipe, 0);
                craftingProcesses.Add(craftingProcess);
            }
        }

        // This method is called upon interaction with the Crafting Bench
        // Initialize UI
        public override void OpenHub()
        {
            base.OpenHub();
            craftUIInstance = Instantiate(craftingUIPrefab);
            craftUIInstance.InitializeUI(availableRecipes, this, CloseHub);
        }

        // This method is called when closing the Crafting Bench
        // Destroy UI
        public override void CloseHub()
        {
            base.CloseHub();
            if (craftUIInstance != null)
                Destroy(craftUIInstance.gameObject);
        }

        public void StartCraftingProcess(Recipe_SO recipe, int amountToCraft)
        {
            // Find the crafting process for the given recipe
            CraftingProcess craftingProcess = craftingProcesses.Find(cp => cp.recipe == recipe);

            if (craftingProcess != null)
            {
                craftingProcess.remainingAmountToCraft += amountToCraft;

                // If the process is not currently crafting, start it
                if (!craftingProcess.isCrafting)
                {
                    craftingProcess.isCrafting = true;
                    craftingProcess.craftingCoroutine = StartCoroutine(CraftingCoroutine(craftingProcess));
                }
#if SAVE_LOAD_ADD_ON
                StoreData();
#endif
            }
        }

        private IEnumerator CraftingCoroutine(CraftingProcess craftingProcess)
        {
            float timeToCraft = craftingProcess.recipe.timeToCraft > 0 ? craftingProcess.recipe.timeToCraft : 0.2f;
            float elapsedTime = craftingProcess.craftingProgress * timeToCraft;
            float lastSavedProgress = 0f;
            float progressSaveThreshold = 0.1f;
            bool isFirst = true;

            // Keep looping if there are items that still need to be crafted
            while (craftingProcess.remainingAmountToCraft > 0)
            {
                if (!isFirst) elapsedTime = 0f;

                while (elapsedTime < timeToCraft)
                {
                    elapsedTime += Time.deltaTime;
                    craftingProcess.craftingProgress = Mathf.Clamp01(elapsedTime / timeToCraft);

                    // Update the UI progress bar dynamically
                    craftUIInstance?.UpdateCraftingProgress(craftingProcess, craftingProcess.craftingProgress);

                    if (craftingProcess.craftingProgress - lastSavedProgress >= progressSaveThreshold)
                    {
#if SAVE_LOAD_ADD_ON
                        StoreData();
#endif
                        lastSavedProgress = craftingProcess.craftingProgress;
                    }

                    yield return null;
                }

                isFirst = false;

                craftingProcess.totalCrafted++;
                craftingProcess.remainingAmountToCraft--;
                // Reset progress to start over again
                craftingProcess.craftingProgress = 0f;

                // Reflect changes in the UI
                craftUIInstance?.UpdateButtonProgress(craftingProcess, 0f);

                // Save this interaction to the GameDataManager
#if SAVE_LOAD_ADD_ON
                StoreData();
#endif
            }

            craftingProcess.isCrafting = false;
#if SAVE_LOAD_ADD_ON
            StoreData();
#endif
        }
    }
}