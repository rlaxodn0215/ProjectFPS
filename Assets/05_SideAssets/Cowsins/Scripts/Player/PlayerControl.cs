using UnityEngine;
using System;
using System.Collections.Generic;

namespace cowsins
{
    // This class controls the controllable state of the player.
    // If non-controllable ( controllable == false ), the player won´t be able to move or perform any action.
    // GrantControl, LoseControl, ToggleControl and CheckIfCanGrantControl methods can be accessed to handle the player control.
    public class PlayerControl : MonoBehaviour, IPlayerControlProvider
    {
        public bool IsControllable => controllable;
        public bool IsMovementControllable => movementControllable && (restrictions & ControlRestriction.Movement) == 0;
        public bool CameraControllable => cameraControllable && !IsRestricted(ControlRestriction.Camera);
        public bool ActionsControllable => actionsControllable && (restrictions & ControlRestriction.Actions) == 0;
        public bool ShootingControllable => shootingControllable && (restrictions & ControlRestriction.Shooting) == 0;

        [Flags]
        public enum ControlRestriction { None = 0, Movement = 1, Camera = 2, Actions = 4, Shooting = 8 }
        private readonly Dictionary<object, ControlRestriction> restrictionTokens = new();
        private ControlRestriction restrictions;
        // Invalidates delayed attacks even if a restriction ends before their hit delay.
        internal uint AttackRevision { get; private set; }
        internal bool HasMovementRestriction => (restrictions & ControlRestriction.Movement) != 0;
        public bool IsRestricted(ControlRestriction flags) => (restrictions & flags) != 0;

        public void AddRestriction(object token, ControlRestriction flags)
        {
            if (token == null) throw new ArgumentNullException(nameof(token));
            if (restrictionTokens.TryGetValue(token, out var previous) && previous == flags) return;
            restrictionTokens[token] = flags;
            if ((flags & (ControlRestriction.Actions | ControlRestriction.Shooting)) != 0) AttackRevision++;
            RefreshRestrictions();
        }

        public void RemoveRestriction(object token)
        {
            if (token != null && restrictionTokens.Remove(token)) RefreshRestrictions();
        }

        private void RefreshRestrictions()
        {
            restrictions = ControlRestriction.None;
            foreach (var flags in restrictionTokens.Values) restrictions |= flags;
        }

        private bool controllable = true;
        private bool movementControllable = true;
        private bool cameraControllable = true;
        private bool actionsControllable = true;
        private bool shootingControllable = true;

        // Reference to PlayerStats.cs ( IPlayerStatsProvider is implemented in PlayerStats.cs )
        private IPlayerStatsProvider playerStatusProvider; 

        private void Awake()
        {
            playerStatusProvider = GetComponent<IPlayerStatsProvider>();
            GrantControl();
        }

        /***************************************** GLOBAL/CORE CONTROL *************************************************/

        /// <summary>
        /// Forces the player to be controlled. CheckIfCanGrantControl() method is recommended instead.
        /// </summary>
        public void GrantCameraControl() => cameraControllable = true;
        public void LoseCameraControl() => cameraControllable = false;

        public void GrantControl()
        {
            controllable = true;
            GrantMovementControl();
            GrantCameraControl();
            GrantActionsControl();
            GrantShootingControl();
        } 
            

        /// <summary>
        /// Disallows the player to be controlled.
        /// </summary>
        public void LoseControl()
        {
            controllable = false;
            LoseMovementControl();
            LoseCameraControl();
            LoseActionsControl();
            LoseShootingControl();
        }

        /// <summary>
        /// Toggles the controllable state of the player.
        /// </summary>
        public void ToggleControl() { controllable = !controllable; if (!controllable) AttackRevision++; }

        /// <summary>
        /// Checks if the game is paused or the player is dead before allowing the player to be controlled.
        /// </summary>
        public void CheckIfCanGrantControl()
        {
            if (PauseMenu.isPaused || playerStatusProvider?.IsDead == true) return;
            GrantControl();
        }


        /***************************************** MOVEMENT CONTROL *************************************************/
        /// <summary>
        /// Allows movement. If Global Control is disabled, this will have no effect.
        /// </summary>
        public void GrantMovementControl() => movementControllable = true;

        /// <summary>
        /// Disallows the player movement. This is independent to the Global Control System
        /// </summary>
        public void LoseMovementControl() => movementControllable = false;

        /// <summary>
        /// Toggles the movement controllable state of the player.
        /// </summary>
        public void ToggleMovementControl() => movementControllable = !movementControllable;

        /***************************************** ACTIONS CONTROL *************************************************/
        
        /// <summary>
        /// Allows actions (shooting, reloading, interacting, switching weapons)
        /// </summary>
        public void GrantActionsControl() => actionsControllable = true;

        /// <summary>
        /// Disallows player actions.
        /// </summary>
        public void LoseActionsControl() { actionsControllable = false; AttackRevision++; }

        /// <summary>
        /// Toggles the actions controllable state of the player.
        /// </summary>
        public void ToggleActionsControl() { actionsControllable = !actionsControllable; if (!actionsControllable) AttackRevision++; }

        /***************************************** SHOOTING CONTROL *************************************************/
        
        /// <summary>
        /// Allows shooting.
        /// </summary>
        public void GrantShootingControl() => shootingControllable = true;

        /// <summary>
        /// Disallows shooting.
        /// </summary>
        public void LoseShootingControl() { shootingControllable = false; AttackRevision++; }

        /// <summary>
        /// Toggles the shooting controllable state of the player.
        /// </summary>
        public void ToggleShootingControl() { shootingControllable = !shootingControllable; if (!shootingControllable) AttackRevision++; }
    }
}
