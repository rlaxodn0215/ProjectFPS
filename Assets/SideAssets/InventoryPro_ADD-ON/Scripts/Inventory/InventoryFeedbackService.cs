using UnityEngine;

namespace cowsins.Inventory
{
    public class InventoryFeedbackService : IInventoryFeedbackService
    {
        private readonly InventoryProManager manager;

        public InventoryFeedbackService(InventoryProManager manager) => this.manager = manager;

        public void PlaySound(AudioClip clip)
        {
            if (clip != null && SoundManager.Instance != null) SoundManager.Instance.PlaySound(clip, 0, 0, false);
        }

        public void ShowToast(string message)
        {
            if (!string.IsNullOrEmpty(message) && ToastManager.Instance != null) ToastManager.Instance.ShowToast(message);
        }

        public void OnItemPlaced()
        {
            if (manager != null && manager.PlaceItemSFX != null) PlaySound(manager.PlaceItemSFX);
            manager?.Events?.onPlaceItem?.Invoke();
        }

        public void OnItemRotated()
        {
            if (manager != null && manager.RotateItemSFX != null) PlaySound(manager.RotateItemSFX);
            manager?.Events?.onRotateItem?.Invoke();
        }

        public void OnItemDeleted(string itemName)
        {
            if (ToastManager.Instance != null) ShowToast($"{itemName} {ToastManager.Instance.ItemDeleted}");
            manager?.Events?.onItemDeleted?.Invoke(itemName);
        }

        public void OnItemBroken(string itemName)
        {
            bool showToast = manager != null ? manager.ShowToastOnItemBreak : (InventoryProManager.instance != null && InventoryProManager.instance.ShowToastOnItemBreak);
            if (showToast && ToastManager.Instance != null)
            {
                string brokeSuffix = !string.IsNullOrEmpty(ToastManager.Instance.ItemBroke) ? ToastManager.Instance.ItemBroke : "broke";
                ShowToast($"{itemName} {brokeSuffix}");
            }
            manager?.Events?.onItemBroken?.Invoke(itemName);
        }

        public void ShowAttachmentAddedToast(string attachmentName, string weaponName)
        {
            if (ToastManager.Instance != null) ShowToast($"{attachmentName} {ToastManager.Instance.AttachmentSuccessfullyAddedMsg} {weaponName}");
        }
    }
}
