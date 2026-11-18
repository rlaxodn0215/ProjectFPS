using UnityEngine;
using UnityEngine.SceneManagement;

namespace cowsins.SaveLoad
{
    public class SaveLoadButton : MonoBehaviour
    {
        /// <summary>
        /// Saves the game if Data Persistence is available
        /// </summary>
        public void SaveGame()
        {
            if (DataPersistenceManager.instance == null)
            {
                ToastManager.Instance?.ShowToast(ToastManager.Instance?.DataPersistenceManagerNotAvailableMsg);
                Debug.Log("<color=red>[COWSINS]</color> Data Persistence Manager Not Found! To Save & Load your game, " +
                    "load the scene from the MainMenu or any other scene that includes DataPersistenceManager.");
                return;
            }

            DataPersistenceManager.instance.SaveGame();
            Debug.Log("<color=green>[COWSINS]</color> Game successfully saved!");
        }

        /// <summary>
        /// Loads the game if Data Persistence is available
        /// </summary>
        public void LoadGame()
        {
            if (DataPersistenceManager.instance == null)
            {
                ToastManager.Instance?.ShowToast(ToastManager.Instance?.DataPersistenceManagerNotAvailableMsg);
                Debug.Log("<color=red>[COWSINS]</color> Data Persistence Manager Not Found! To Save & Load your game, " +
                    "load the scene from the MainMenu or any other scene that includes DataPersistenceManager.");
                return;
            }

            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            Debug.Log("<color=green>[COWSINS]</color> Game successfully loaded!");
        }
    }
}