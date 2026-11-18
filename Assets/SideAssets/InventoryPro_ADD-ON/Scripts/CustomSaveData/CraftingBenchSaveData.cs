using System.Collections.Generic;
using cowsins.SaveLoad;
using UnityEngine.SceneManagement;

namespace cowsins.Inventory
{
    public partial class CraftingBench : TradeHub
    {
        // Stores important Crafting Process Data to be Serialized
        public class CraftingProcessSaveData
        {
            public int remainingAmountToCraft;
            public int totalCrafted;
            public float craftingProgress;
            public bool isCrafting;
        }
#if SAVE_LOAD_ADD_ON
        // craftingProcesses(List<CraftingProcess>) from CraftingBench cannot be directly serialized, so we´ll store serializable data of each CraftingProcess instead
        // Because of it, we need to create a new class that inherits from CustomSaveData. This class will store our customized data instead.
        [System.Serializable]
        public class CraftingBenchSaveData : CustomSaveData
        {
            public List<CraftingProcessSaveData> craftingProcessData = new List<CraftingProcessSaveData>();
        }

        // Base SaveFields method gathers all variables within the class that implement the [SaveField]
        // In this case, no variable implements it in CraftingBench, so we´ll be processing and saving the craftingProcesses manually.
        public override CustomSaveData SaveFields()
        {
            List<CraftingProcessSaveData> craftingProcessData = new List<CraftingProcessSaveData>();

            // For each Crafting Process, store the required Serializable data
            foreach (var craftingProcess in craftingProcesses)
            {
                craftingProcessData.Add(new CraftingProcessSaveData
                {
                    remainingAmountToCraft = craftingProcess.remainingAmountToCraft,
                    totalCrafted = craftingProcess.totalCrafted,
                    craftingProgress = craftingProcess.craftingProgress,
                    isCrafting = craftingProcess.isCrafting
                });
            }

            return new CraftingBenchSaveData
            {
                craftingProcessData = craftingProcessData,
                // Scene Name is also very important and must be saved.
                SceneName = SceneManager.GetActiveScene().name
            };
        }


        public override void LoadFields(object data)
        {
            if (data is CraftingBenchSaveData shopData)
            {
                for (int i = 0; i < craftingProcesses.Count; i++)
                {
                    var processData = shopData.craftingProcessData[i];
                    CraftingProcess craftingProcess = craftingProcesses[i];

                    // Update the crafting process data with the saved data
                    craftingProcess.remainingAmountToCraft = processData.remainingAmountToCraft;
                    craftingProcess.totalCrafted = processData.totalCrafted;
                    craftingProcess.craftingProgress = processData.craftingProgress;
                    craftingProcess.isCrafting = processData.isCrafting;

                    // If the crafting process is still in progress, restart the coroutine
                    if (craftingProcess.isCrafting)
                    {
                        craftingProcess.craftingCoroutine = StartCoroutine(CraftingCoroutine(craftingProcess));
                    }

                    craftingProcesses[i] = craftingProcess;
                }
            }
        }
#endif
    }
}