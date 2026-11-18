using UnityEngine;
using UnityEngine.SceneManagement;

namespace cowsins.SaveLoad
{
    public class LoadTrigger : Trigger
    {
        // Load on Trigger
        public override void TriggerEnter(Collider other)
        {
            if (DataPersistenceManager.instance == null)
            {
                ToastManager.Instance?.ShowToast(ToastManager.Instance?.DataPersistenceManagerNotAvailableMsg);
                Debug.Log("<color=red>[COWSINS]</color> Data Persistence Manager Not Found! To Save & Load your game, " +
                    "load the scene from the MainMenu or any other scene that includes DataPersistenceManager.");
                return;
            }

            if (other.gameObject.CompareTag("Player"))
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
                ToastManager.Instance?.ShowToast(ToastManager.Instance?.GameLoaded);
                Debug.Log("<color=green>[COWSINS]</color> Game successfully loaded!");
            }
        }
    }
}