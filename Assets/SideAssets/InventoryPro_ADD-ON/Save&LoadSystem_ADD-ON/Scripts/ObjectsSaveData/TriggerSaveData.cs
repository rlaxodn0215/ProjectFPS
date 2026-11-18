using System.Collections.Generic;
using cowsins.SaveLoad;

namespace cowsins
{
    public partial class Trigger : Identifiable
    {

#if SAVE_LOAD_ADD_ON
        protected void SaveTrigger()
        {
            if (GameDataManager.instance == null) return; 

            if (GameDataManager.instance.instantiatedObjects.ContainsKey(this.uniqueID))
            {
                // If it was spawned, remove the item from instantiatedObjects ( It´s not an item that should be instantiated anymore )
                GameDataManager.instance.instantiatedObjects.Remove(this.uniqueID);
            }
            else
            {
                // Add to triggers data in GameDataManager
                if (GameDataManager.instance.triggersData == null)
                {
                    GameDataManager.instance.triggersData = new Dictionary<string, CustomSaveData>();
                }
                GameDataManager.instance.triggersData[this.uniqueID] = SaveFields();
            }
        }
#endif
    }

}