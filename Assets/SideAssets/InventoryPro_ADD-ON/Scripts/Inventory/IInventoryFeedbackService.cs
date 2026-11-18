using UnityEngine;

namespace cowsins.Inventory
{
    public interface IInventoryFeedbackService
    {
        void PlaySound(AudioClip clip);
        void ShowToast(string message);
        void OnItemPlaced();
        void OnItemRotated();
        void OnItemDeleted(string itemName);
        void OnItemBroken(string itemName);
        void ShowAttachmentAddedToast(string attachmentName, string weaponName);
    }
}
