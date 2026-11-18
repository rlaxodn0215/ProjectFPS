using UnityEngine;

namespace cowsins.SaveLoad
{
    public class ToggleAutoSave : Trigger
    {
        public override void TriggerEnter(Collider other)
        {
            // If Data Persistence Manager is not available, return and show a toast message
            if (DataPersistenceManager.instance == null)
            {
                ToastManager.Instance?.ShowToast(ToastManager.Instance?.DataPersistenceManagerNotAvailableMsg);
                return;
            }

            // Toggle Auto Save
            if (DataPersistenceManager.instance.IsAutoSaveEnabled())
            {
                ToastManager.Instance?.ShowToast(ToastManager.Instance?.DisableAutoSaveMsg);
                DataPersistenceManager.instance.DisableAutoSave();
            }
            else
            {
                ToastManager.Instance?.ShowToast(ToastManager.Instance?.EnableAutoSaveMsg);
                DataPersistenceManager.instance.EnableAutoSave();
            }
        }
    }
}