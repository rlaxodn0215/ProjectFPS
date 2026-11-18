using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using cowsins;

namespace cowsins.SaveLoad
{
    public class PlayerSaveModule : ISaveModule
    {
        public string ModuleId => "Player";

        private PlayerMovement playerMovement;
        private WeaponController weaponController;
        private PlayerStats playerStats;
        private PlayerMultipliers playerMultipliers;
        private PlayerStates playerStates;
        private ExperienceService experienceManager;
        private CoinService coinManager;

        public PlayerMovement CurrentPlayerMovement => playerMovement;
        public WeaponController CurrentWeaponController => weaponController;
        public PlayerStats CurrentPlayerStats => playerStats;

        public void RefreshPlayerReferences()
        {
#pragma warning disable CS0618
            playerMovement = UnityEngine.Object.FindObjectOfType<PlayerMovement>();
#pragma warning restore CS0618
            if (playerMovement != null)
            {
                weaponController = playerMovement.GetComponent<WeaponController>();
                playerStats = playerMovement.GetComponent<PlayerStats>();
                playerMultipliers = playerMovement.GetComponent<PlayerMultipliers>();
                playerStates = playerMovement.GetComponent<PlayerStates>();

                var progressionManager = playerMovement.GetComponent<ProgressionManager>();
                experienceManager = progressionManager?.experienceService;
                coinManager = progressionManager?.coinService;
            }
            else
            {
                weaponController = null;
                playerStats = null;
                playerMultipliers = null;
                playerStates = null;
                experienceManager = null;
                coinManager = null;
            }
        }

        public void SaveData(GameData gameData, DataPersistence_SO settings)
        {
            if (settings == null || gameData == null) return;

            RefreshPlayerReferences();

            // If no Player is found, or the player is dead, avoid saving
            if (playerMovement == null || playerStats?.IsDead == true) return;

            string sceneName = SceneManager.GetActiveScene().name;
            bool isSceneExcluded = DataPersistenceManager.instance != null && DataPersistenceManager.instance.IsSceneExcludedFromSpecificData();

            var playerData = gameData.player ?? (gameData.player = new GameData.PlayerSaveData());

            // Player Transforms
            if (settings.SavePlayerTransforms && !isSceneExcluded)
            {
                var transformData = new GameData.SerializablePlayerTransforms
                {
                    playerPosition = new float[]
                    {
                        playerMovement.transform.position.x,
                        playerMovement.transform.position.y,
                        playerMovement.transform.position.z
                    },
                    playerRotation = new float[]
                    {
                        playerMovement.Orientation != null ? playerMovement.Orientation.Rotation.x : 0f,
                        playerMovement.Orientation != null ? playerMovement.Orientation.Rotation.y : 0f,
                        playerMovement.Orientation != null ? playerMovement.Orientation.Rotation.z : 0f,
                        playerMovement.Orientation != null ? playerMovement.Orientation.Rotation.w : 1f
                    },
                    isCrouching = playerMovement.IsCrouching
                };

                if (playerData.playerTransforms == null)
                {
                    playerData.playerTransforms = new System.Collections.Generic.Dictionary<string, GameData.SerializablePlayerTransforms>();
                }

                playerData.playerTransforms[sceneName] = transformData;
            }

            // Player Stats
            if (settings.SavePlayerStats && playerStats != null)
            {
                playerData.playerHealth = playerStats.Health;
                playerData.playerShield = playerStats.Shield;
                playerData.maxHealth = playerStats.MaxHealth;
                playerData.maxShield = playerStats.MaxShield;

                if (playerMultipliers != null)
                {
                    playerData.damageMultiplier = playerMultipliers.DamageMultiplier.Value;
                    playerData.healMultiplier = playerMultipliers.HealMultiplier.Value;
                }
            }

            // Experience & Level
            if (settings.SaveExperience && experienceManager != null)
            {
                playerData.totalExperience = experienceManager.TotalExperience;
                playerData.playerLevel = experienceManager.playerLevel;
            }

            // Currency
            if (settings.SaveCoins && coinManager != null)
            {
                playerData.coins = coinManager.coins;
            }

            // Hotbar (FPS Engine's Base Weapon Inventory)
            if (settings.SaveHotbar && weaponController != null && weaponController.Inventory != null)
            {
                playerData.currentWeaponInt = weaponController.CurrentWeaponIndex;

                int hotbarSize = weaponController.Inventory.Length;
                playerData.serializableHotbarData = new GameData.SerializableInventoryData[hotbarSize];

                for (int i = 0; i < hotbarSize; i++)
                {
                    var tempHotbarData = new GameData.SerializableInventoryData();
                    WeaponIdentification weaponId = weaponController.Inventory[i];

                    if (weaponId != null && weaponId.weapon != null)
                    {
                        tempHotbarData.SOPath = weaponId.weapon.name;
                        tempHotbarData.amount = 1;
                        tempHotbarData.bulletsLeftInMagazine = weaponId.bulletsLeftInMagazine;
                        tempHotbarData.totalBullets = weaponId.totalBullets;
                        tempHotbarData.currentDurability = 0f;

                        if (weaponId.AttachmentState != null)
                        {
                            tempHotbarData.barrel = weaponId.AttachmentState.GetCurrent(AttachmentType.Barrel)?.attachmentIdentifier.name;
                            tempHotbarData.scope = weaponId.AttachmentState.GetCurrent(AttachmentType.Scope)?.attachmentIdentifier.name;
                            tempHotbarData.stock = weaponId.AttachmentState.GetCurrent(AttachmentType.Stock)?.attachmentIdentifier.name;
                            tempHotbarData.grip = weaponId.AttachmentState.GetCurrent(AttachmentType.Grip)?.attachmentIdentifier.name;
                            tempHotbarData.magazine = weaponId.AttachmentState.GetCurrent(AttachmentType.Magazine)?.attachmentIdentifier.name;
                            tempHotbarData.flashlight = weaponId.AttachmentState.GetCurrent(AttachmentType.Flashlight)?.attachmentIdentifier.name;
                            tempHotbarData.laser = weaponId.AttachmentState.GetCurrent(AttachmentType.Laser)?.attachmentIdentifier.name;
                        }
                    }
                    else
                    {
                        tempHotbarData.SOPath = string.Empty;
                        tempHotbarData.amount = 0;
                        tempHotbarData.bulletsLeftInMagazine = 0;
                        tempHotbarData.totalBullets = 0;
                    }

                    playerData.serializableHotbarData[i] = tempHotbarData;
                }
            }
        }

        public void LoadData(GameData gameData, DataPersistence_SO settings)
        {
            if (settings == null || gameData == null || gameData.player == null) return;

            RefreshPlayerReferences();
            if (playerMovement == null) return;

            var playerData = gameData.player;
            string currentSceneName = SceneManager.GetActiveScene().name;

            // Player Transforms
            if (settings.SavePlayerTransforms && playerData.playerTransforms != null && playerData.playerTransforms.TryGetValue(currentSceneName, out var transformData))
            {
                if (transformData.playerPosition != null && transformData.playerPosition.Length >= 3 &&
                    transformData.playerRotation != null && transformData.playerRotation.Length >= 4)
                {
                    Vector3 position = new Vector3(transformData.playerPosition[0], transformData.playerPosition[1], transformData.playerPosition[2]);
                    Quaternion rotation = new Quaternion(transformData.playerRotation[0], transformData.playerRotation[1], transformData.playerRotation[2], transformData.playerRotation[3]);

                    playerMovement.TeleportPlayer(position, rotation, false, false);

                    if (transformData.isCrouching && playerStates != null)
                    {
                        playerStates.CurrentState.ExitState();
                        playerStates.CurrentState = playerStates._States.Crouch();
                        playerStates.CurrentState.EnterState();
                    }
                }
            }

            // Player Stats
            if (settings.SavePlayerStats && playerStats != null)
            {
                playerStats.OverrideHealth(playerData.playerHealth, playerData.maxHealth, playerData.playerShield, playerData.maxShield);

                if (playerMultipliers != null)
                {
                    playerMultipliers.DamageMultiplier.BaseValue = playerData.damageMultiplier;
                    playerMultipliers.HealMultiplier.BaseValue = playerData.healMultiplier;
                }

                playerStats.Events.OnHealthChanged?.Invoke(playerData.playerHealth, playerData.playerShield, false);
            }

            // Experience & Level
            if (settings.SaveExperience && experienceManager != null)
            {
                experienceManager.ResetExperience();
                experienceManager.AddExperience(playerData.totalExperience);
            }

            // Currency
            if (settings.SaveCoins && coinManager != null)
            {
                coinManager.ResetCoins();
                coinManager.AddCoins(playerData.coins, false);
            }

            // Hotbar
            if (settings.SaveHotbar && weaponController != null)
            {
                weaponController.CurrentWeaponIndex = playerData.currentWeaponInt;
                LoadHotbarWeaponData(playerData);
            }
        }

        private void LoadHotbarWeaponData(GameData.PlayerSaveData playerData)
        {
            if (playerData.serializableHotbarData == null || playerData.serializableHotbarData.Length == 0) return;

            for (int i = 0; i < playerData.serializableHotbarData.Length; i++)
            {
                var data = playerData.serializableHotbarData[i];
                string weaponAssetPath = data?.SOPath;

                if (string.IsNullOrEmpty(weaponAssetPath)) continue;

                Weapon_SO weaponSo = ItemRegistry.GetItemByName(weaponAssetPath) as Weapon_SO;
                if (weaponSo == null)
                {
                    Debug.LogError($"<color=red>[COWSINS]</color> Weapon asset '{weaponAssetPath}' not found.");
                    continue;
                }

                weaponController.InstantiateWeapon(weaponSo, i, data.bulletsLeftInMagazine, data.totalBullets);

                var attachmentPaths = new[] { data.barrel, data.scope, data.stock, data.grip, data.magazine, data.flashlight, data.laser };
                for (int j = 0; j < attachmentPaths.Length; j++)
                {
                    if (!string.IsNullOrEmpty(attachmentPaths[j]))
                    {
                        var attachment = ItemRegistry.GetItemByName(attachmentPaths[j]) as AttachmentIdentifier_SO;
                        if (attachment != null && weaponController.attachmentsSystem != null)
                        {
                            weaponController.attachmentsSystem.AssignNewAttachmentToWeapon(attachment, i);
                        }
                    }
                }
            }
        }
    }
}
