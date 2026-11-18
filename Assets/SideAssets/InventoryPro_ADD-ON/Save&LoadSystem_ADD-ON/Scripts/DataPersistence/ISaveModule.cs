namespace cowsins.SaveLoad
{
    public interface ISaveModule
    {
        /// <summary>
        /// Unique identifier for this save module
        /// </summary>
        string ModuleId { get; }

        /// <summary>
        /// Called when the game is being saved.
        /// </summary>
        void SaveData(GameData gameData, DataPersistence_SO settings);

        /// <summary>
        /// Called when the game is being loaded.
        /// </summary>
        void LoadData(GameData gameData, DataPersistence_SO settings);
    }
}
