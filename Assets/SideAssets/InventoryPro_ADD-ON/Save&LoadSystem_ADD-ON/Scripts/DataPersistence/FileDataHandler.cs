using System.Collections.Generic;
using System.IO;
using System;
using UnityEngine;
using Newtonsoft.Json;
using System.Security.Cryptography;
using System.Text;

namespace cowsins.SaveLoad
{
    // Helper class that handles Safe Json, Serialization, Encrypting & Decrypting Files.
    public class FileDataHandler
    {
        private string persistentDataDirPath = "";
        private string dataFileName = "";
        private const string metadataFileName = "meta.json";

        /// <summary>
        /// When enabled, files will be saved with AES encryption.
        /// When disabled, files are stored as plain readable JSON for easier debugging.
        /// </summary>
        public bool UseEncryption { get; set; } = true;

        // File Data Handler Constructor
        public FileDataHandler(string dataFileName, bool useEncryption = true)
        {
            this.persistentDataDirPath = Application.persistentDataPath;
            this.dataFileName = dataFileName;
            this.UseEncryption = useEncryption;
        }

        public void Save(GameData data, string profileId)
        {
            // We always need to save based on a Profile ID
            if (profileId == null) return;

            // The data is stored at the persistent data path, inside its corresponding profileID index, in a json file named after the specified dataFileName.
            string fullPath = Path.Combine(persistentDataDirPath, profileId, dataFileName);
            string tempPath = fullPath + ".tmp";
            string backupPath = fullPath + ".bak";

            try
            {
                string directoryPath = Path.GetDirectoryName(fullPath);
                if (!Directory.Exists(directoryPath)) Directory.CreateDirectory(directoryPath);

                JsonSerializerSettings settings = GetJsonSerializerSettings();
                string dataToStore = JsonConvert.SerializeObject(data, settings);

                if (UseEncryption)
                {
                    dataToStore = Encrypt(dataToStore);
                }

                // Write to a temporary file first to guarantee atomic write safety
                using (FileStream stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    using (StreamWriter writer = new StreamWriter(stream, Encoding.UTF8))
                    {
                        writer.Write(dataToStore);
                    }
                }

                // If a valid save file already exists, create a backup copy before replacing it
                if (File.Exists(fullPath))
                {
                    try
                    {
                        File.Copy(fullPath, backupPath, true);
                    }
                    catch (Exception backupEx)
                    {
                        Debug.LogWarning($"<color=yellow>[COWSINS]</color> Failed to create save backup file: {backupEx.Message}");
                    }

                    File.Delete(fullPath);
                }

                // Swap temporary file into place
                File.Move(tempPath, fullPath);

                // Save lightweight metadata header for UI or MainMenu reading
                SaveMetadata(data, profileId);
            }
            catch (Exception e)
            {
                // Clean up orphaned temp file if writing failed
                if (File.Exists(tempPath))
                {
                    try { File.Delete(tempPath); } catch { }
                }

                Debug.LogError("<color=red>[COWSINS]</color> Error occurred when trying to save data to file: " + fullPath + "\n" + e);
            }
        }

        public GameData Load(string profileId)
        {
            // We always need to load based on a Profile ID
            if (profileId == null) return null;

            string fullPath = Path.Combine(persistentDataDirPath, profileId, dataFileName);
            string backupPath = fullPath + ".bak";

            // If the primary file is missing but a backup exists, attempt recovery
            if (!File.Exists(fullPath) && File.Exists(backupPath))
            {
                Debug.LogWarning($"<color=yellow>[COWSINS]</color> Primary save file missing at {fullPath}. Restoring from backup {backupPath}...");
                try
                {
                    File.Copy(backupPath, fullPath, true);
                }
                catch (Exception restoreEx)
                {
                    Debug.LogError($"<color=red>[COWSINS]</color> Failed to restore from backup: {restoreEx.Message}");
                }
            }

            GameData loadedData = null;

            if (File.Exists(fullPath))
            {
                loadedData = LoadFromFile(fullPath);

                // If loading the primary file failed or produced corrupt data, try loading from backup
                if (loadedData == null && File.Exists(backupPath))
                {
                    Debug.LogWarning($"<color=yellow>[COWSINS]</color> Primary save file at {fullPath} failed to load. Attempting recovery from backup {backupPath}...");
                    loadedData = LoadFromFile(backupPath);
                }
            }

            return loadedData;
        }

        private GameData LoadFromFile(string filePath)
        {
            try
            {
                string dataToLoad = "";
                using (FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        dataToLoad = reader.ReadToEnd();
                    }
                }

                if (string.IsNullOrWhiteSpace(dataToLoad))
                {
                    Debug.LogWarning($"<color=yellow>[COWSINS]</color> Save file at {filePath} was empty.");
                    return null;
                }

                // Auto-detect encryption
                string trimmed = dataToLoad.TrimStart();
                bool isPlainJson = trimmed.StartsWith("{") || trimmed.StartsWith("[");

                if (!isPlainJson)
                {
                    dataToLoad = Decrypt(dataToLoad);
                }

                JsonSerializerSettings settings = GetJsonSerializerSettings();
                GameData loaded = JsonConvert.DeserializeObject<GameData>(dataToLoad, settings);
                return loaded;
            }
            catch (Exception e)
            {
                Debug.LogError("<color=red>[COWSINS]</color> Error occurred when trying to load data from file: " + filePath + "\n" + e);
                return null;
            }
        }

        // Deletes a Save Slot based on its profile ID
        public void Delete(string profileId)
        {
            if (profileId == null) return;

            // Gather the entire Path
            string fullPath = Path.Combine(persistentDataDirPath, profileId, dataFileName);
            try
            {
                // If the file exists, delete it
                if (File.Exists(fullPath))
                {
                    Directory.Delete(Path.GetDirectoryName(fullPath), true);
                }
                else
                {
                    Debug.LogWarning("Tried to delete profile data, but data was not found at path: " + fullPath);
                }
            }
            catch (Exception e)
            {
                Debug.LogError("Failed to delete profile data for profileId: "
                    + profileId + " at Path: " + fullPath + "\n" + e);
            }
        }

        public void SaveMetadata(GameData data, string profileId)
        {
            if (profileId == null || data == null) return;
            try
            {
                string metaPath = Path.Combine(persistentDataDirPath, profileId, metadataFileName);
                SaveSlotMetadata metadata = new SaveSlotMetadata(profileId, data);
                string metaJson = JsonConvert.SerializeObject(metadata, Formatting.Indented);
                File.WriteAllText(metaPath, metaJson, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"<color=yellow>[COWSINS]</color> Failed to save slot metadata: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads only the lightweight metadata for a profile
        /// </summary>
        public SaveSlotMetadata LoadMetadata(string profileId)
        {
            if (profileId == null) return null;

            string metaPath = Path.Combine(persistentDataDirPath, profileId, metadataFileName);
            if (File.Exists(metaPath))
            {
                try
                {
                    string json = File.ReadAllText(metaPath, Encoding.UTF8);
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        SaveSlotMetadata meta = JsonConvert.DeserializeObject<SaveSlotMetadata>(json);
                        if (meta != null) return meta;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"<color=yellow>[COWSINS]</color> Failed to read metadata at {metaPath}, falling back to full save: {ex.Message}");
                }
            }

            // If metadata is missing, load the full GameData and generate metadata for subsequent fast queries
            GameData fullData = Load(profileId);
            if (fullData != null)
            {
                SaveSlotMetadata metadata = new SaveSlotMetadata(profileId, fullData);
                SaveMetadata(fullData, profileId);
                return metadata;
            }

            return null;
        }

        /// <summary>
        /// Returns lightweight metadata for all profile directories without loading full save payloads.
        /// </summary>
        public Dictionary<string, SaveSlotMetadata> LoadAllProfilesMetadata()
        {
            Dictionary<string, SaveSlotMetadata> metadataDictionary = new Dictionary<string, SaveSlotMetadata>();

            if (!Directory.Exists(persistentDataDirPath))
            {
                return metadataDictionary;
            }

            IEnumerable<DirectoryInfo> dirInfos = new DirectoryInfo(persistentDataDirPath).EnumerateDirectories();
            foreach (DirectoryInfo dirInfo in dirInfos)
            {
                string profileId = dirInfo.Name;
                string fullPath = Path.Combine(persistentDataDirPath, profileId, dataFileName);
                string backupPath = fullPath + ".bak";
                string metaPath = Path.Combine(persistentDataDirPath, profileId, metadataFileName);

                if (!File.Exists(fullPath) && !File.Exists(backupPath) && !File.Exists(metaPath))
                {
                    continue;
                }

                SaveSlotMetadata metadata = LoadMetadata(profileId);
                if (metadata != null)
                {
                    metadataDictionary.Add(profileId, metadata);
                }
            }

            return metadataDictionary;
        }

        // Returns the most recently played Profile ID (That corresponds to a Save Slot) using lightweight metadata
        public string GetMostRecentlyUpdatedProfileId()
        {
            string mostRecentProfileId = null;
            long mostRecentTimestamp = long.MinValue;

            Dictionary<string, SaveSlotMetadata> metadataDictionary = LoadAllProfilesMetadata();

            foreach (KeyValuePair<string, SaveSlotMetadata> pair in metadataDictionary)
            {
                string profileId = pair.Key;
                SaveSlotMetadata meta = pair.Value;

                if (meta == null) continue;

                if (mostRecentProfileId == null || meta.lastUpdated > mostRecentTimestamp)
                {
                    mostRecentProfileId = profileId;
                    mostRecentTimestamp = meta.lastUpdated;
                }
            }
            return mostRecentProfileId;
        }

        public Dictionary<string, GameData> LoadAllProfiles()
        {
            Dictionary<string, GameData> profileDictionary = new Dictionary<string, GameData>();

            if (!Directory.Exists(persistentDataDirPath))
            {
                return profileDictionary;
            }

            IEnumerable<DirectoryInfo> dirInfos = new DirectoryInfo(persistentDataDirPath).EnumerateDirectories();
            foreach (DirectoryInfo dirInfo in dirInfos)
            {
                string profileId = dirInfo.Name;
                string fullPath = Path.Combine(persistentDataDirPath, profileId, dataFileName);
                string backupPath = fullPath + ".bak";

                if (!File.Exists(fullPath) && !File.Exists(backupPath))
                {
                    continue;
                }

                GameData profileData = Load(profileId);

                if (profileData != null)
                {
                    profileDictionary.Add(profileId, profileData);
                }
                else
                {
                    Debug.LogError("Tried to load profile but something went wrong. ProfileID: " + profileId);
                }
            }

            return profileDictionary;
        }

        // Default Json Serializer Settings
        private JsonSerializerSettings GetJsonSerializerSettings()
        {
            return new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Include,
                TypeNameHandling = TypeNameHandling.Auto,  // Enable polymorphic deserialization ( Allows inheritance )
                Formatting = Formatting.Indented,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,  // Avoid circular references
                SerializationBinder = new SafeSerializationBinder()
            };
        }

        // Returns an encrypted String based on the passed string
        private string Encrypt(string plainText)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = GetEncryptionKey();
                aes.IV = GetIVKey();

                using (MemoryStream memoryStream = new MemoryStream())
                using (CryptoStream cryptographyStream = new CryptoStream(memoryStream, aes.CreateEncryptor(), CryptoStreamMode.Write))
                {
                    using (StreamWriter writer = new StreamWriter(cryptographyStream))
                    {
                        writer.Write(plainText);
                    }
                    return Convert.ToBase64String(memoryStream.ToArray());
                }
            }
        }

        // Returns the passed string but decrypted
        private string Decrypt(string encryptedText)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = GetEncryptionKey();
                aes.IV = GetIVKey();

                using (MemoryStream memoryStream = new MemoryStream(Convert.FromBase64String(encryptedText)))
                using (CryptoStream cryptographyStream = new CryptoStream(memoryStream, aes.CreateDecryptor(), CryptoStreamMode.Read))
                using (StreamReader reader = new StreamReader(cryptographyStream))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        private byte[] GetEncryptionKey()
        {        
            // XOR Encrypted Key
            string encryptionKey = "G2I+bSlsPG89bjJpMGgxazZqKmM1YjNtL2wjby5uKGk=";
            encryptionKey = XORDecrypt(encryptionKey, 0x5A);
            return Encoding.UTF8.GetBytes(encryptionKey);
        }

        private byte[] GetIVKey()
        {
            // XOR Encrypted IV Key
            string ivKey = "AmsAaBlpDG4YbxRsF20LYg==";
            ivKey = XORDecrypt(ivKey, 0x5A);
            return Encoding.UTF8.GetBytes(ivKey);
        }

        private string XORDecrypt(string input, byte key)
        {
            byte[] bytes = Convert.FromBase64String(input);
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] ^= key;
            }
            return Encoding.UTF8.GetString(bytes);
        }

#if UNITY_EDITOR
        // For security reasons, do not move EncryptDecryptAESEditorOnly out of #if UNITY_EDITOR conditional statement
        // And do not try to execute it anywhere else.
        // Storing this public method inside #if UNITY_EDITOR allows to ensure that this code won't be put into production and will just be usable for Editor Purposes,
        // Ensuring cheaters cannot decrypt the data so easily.
        public string EncryptDecryptAESEditorOnly(string data)
        {
            if (string.IsNullOrEmpty(data)) return data;
            string trimmed = data.TrimStart();
            if (trimmed.StartsWith("{") || trimmed.StartsWith("["))
            {
                return data; // Plain JSON, no decryption needed
            }
            return Decrypt(data);
        }
#endif
    }
}