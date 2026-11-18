using System.Collections.Generic;
using UnityEngine.SceneManagement;
using cowsins.SaveLoad; 

namespace cowsins.Inventory
{
    public partial class Chest : Interactable
    {
#if SAVE_LOAD_ADD_ON
        // currentSlotData(SlotData[]) from Chest cannot be directly serialized, so we will store serializable data of each SlotData instead
        // Because of it, we need to create a new class that inherits from CustomSaveData. This class will store our customized data instead.
        [System.Serializable]
        public class ChestSaveData : CustomSaveData
        {
            public GameData.SerializableInventoryData[] serializedChestData;
        }

        // Base SaveFields method gathers all variables within the class that implement the [SaveField]
        // In this case, no variable implements it in Chest, so we will be processing and saving the slot datas manually.
        public override CustomSaveData SaveFields()
        {
            List<GameData.SerializableInventoryData> tempSerializedChestData = new List<GameData.SerializableInventoryData>(0);

            for (int i = 0; i < initialInventoryGridData.rows * initialInventoryGridData.columns; i++)
            {
                var data = (currentChestData != null && currentChestData.Length > i) ? currentChestData[i] : null;
                tempSerializedChestData.Add(GameDataManager.SerializeSlotData(data));
            }

            return new ChestSaveData
            {
                serializedChestData = tempSerializedChestData.ToArray(),
                // Scene Name is also very important and must be saved.
                SceneName = SceneManager.GetActiveScene().name
            };
        }

        public override void LoadFields(object data)
        {
            if (data is ChestSaveData saveData)
            {
                SlotData[] tempData = new SlotData[saveData.serializedChestData.Length];
                for (int i = 0; i < saveData.serializedChestData.Length; i++)
                {
                    tempData[i] = InventoryProGeneratorBehaviour.DeserializeSlotData(saveData.serializedChestData[i]);
                }
                currentChestData = tempData;
                base.LoadFields(data);
            }

        }
#endif
    }
}