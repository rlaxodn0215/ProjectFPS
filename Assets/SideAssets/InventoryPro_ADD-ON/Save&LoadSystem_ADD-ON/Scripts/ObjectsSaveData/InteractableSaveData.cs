using cowsins.SaveLoad;
using System.Collections.Generic;

namespace cowsins
{
    public partial class Interactable : Identifiable
    {

#if SAVE_LOAD_ADD_ON
        private void OnEnable()
        {
            interactableEvents.OnInteract.AddListener(StoreData);
        }

        private void OnDisable()
        {
            interactableEvents.OnInteract.RemoveAllListeners();
        }

        public override void StoreData()
        {
            if (GameDataManager.instance == null) return;

            // Check if the item exists in instantiatedObjects ( Therefore, was instantiated previously )
            if (GameDataManager.instance.instantiatedObjects.ContainsKey(this.uniqueID))
            {
                // Remove the item from instantiatedObjects, we don´t need to instantiate it again
                GameDataManager.instance.instantiatedObjects.Remove(this.uniqueID);
            }
            else
            {
                // Proceed to save interaction if not found in instantiatedObjects
                if (GameDataManager.instance.interactablesData == null)
                {
                    GameDataManager.instance.interactablesData = new Dictionary<string, CustomSaveData>();
                }
                GameDataManager.instance.interactablesData[this.uniqueID] = SaveFields();
            }
        }
#endif

    }
}
