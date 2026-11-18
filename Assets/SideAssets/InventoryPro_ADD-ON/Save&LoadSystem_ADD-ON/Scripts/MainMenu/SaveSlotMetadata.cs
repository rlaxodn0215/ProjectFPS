using System;

namespace cowsins.SaveLoad
{
    /// <summary>
    /// Allows the Main Menu and UI to display slot status without loading or decrypting the entire world save payload.
    /// </summary>
    [Serializable]
    public class SaveSlotMetadata
    {
        public string profileId;
        public long lastUpdated;
        public string timeSaved;
        public string currentScene;
        public int playerLevel;
        public int coins;

        public SaveSlotMetadata()
        {
            profileId = string.Empty;
            timeSaved = string.Empty;
            currentScene = string.Empty;
            playerLevel = 0;
            coins = 0;
            lastUpdated = 0;
        }

        public SaveSlotMetadata(string profileId, GameData gameData)
        {
            this.profileId = profileId;
            if (gameData != null)
            {
                this.lastUpdated = gameData.lastUpdated;
                this.timeSaved = gameData.timeSaved;
                this.currentScene = gameData.currentScene;
                this.playerLevel = gameData.player != null ? gameData.player.playerLevel : 0;
                this.coins = gameData.player != null ? gameData.player.coins : 0;
            }
            else
            {
                this.timeSaved = DateTime.Now.ToString("dd MMMM, yyyy HH:mm");
                this.currentScene = string.Empty;
                this.playerLevel = 0;
                this.coins = 0;
                this.lastUpdated = DateTime.Now.ToBinary();
            }
        }
    }
}
