using cowsins;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectFPS.Integrations
{
    [DisallowMultipleComponent, RequireComponent(typeof(PlayerStats), typeof(PlayerControl), typeof(PlayerMovement))]
    public sealed class FPSPlayerCombat : MonoBehaviour, IPlayerDamageFilter
    {
        [SerializeField] private InputActionReference blockAction;
        [SerializeField, Range(0, 180)] private float blockHalfAngle = 60;
        [SerializeField, Range(0, 1)] private float damageReduction = 0.5f;
        private readonly object blockToken = new object();
        private readonly object stunToken = new object();
        private PlayerStats stats;
        private PlayerControl control;
        private PlayerMovement movement;
        private WeaponController weapons;
        private bool blocking;
        private float stunUntil;
        public bool IsStunned => isActiveAndEnabled && !stats.IsDead && Time.time < stunUntil;
        public bool IsBlocking => isActiveAndEnabled && blocking && !IsStunned && !stats.IsDead && !movement.IsDashing;
        public bool IsDodging => isActiveAndEnabled && !stats.IsDead && movement.IsDashing && movement.DamageProtectionWhileDashing;

        private void Awake()
        {
            stats = GetComponent<PlayerStats>();
            control = GetComponent<PlayerControl>();
            movement = GetComponent<PlayerMovement>();
            weapons = GetComponent<WeaponController>();
        }
        private void OnEnable()
        {
            stats.AddOnDieListener(ResetState);
            movement.Events.OnRespawn.AddListener(Respawned);
            movement.Events.OnDashStart.AddListener(DashStarted);
            if (blockAction != null && blockAction.action == null)
            { Debug.LogError("FPSPlayerCombat: Block Action의 실제 입력 액션이 누락되었습니다.", this); enabled = false; }
        }
        private void OnDisable()
        {
            stats.RemoveOnDieListener(ResetState);
            movement.Events.OnRespawn.RemoveListener(Respawned);
            movement.Events.OnDashStart.RemoveListener(DashStarted);
            ResetState();
        }
        private void Respawned(Vector3 position, Quaternion rotation, bool resetVelocity, bool resetVerticalLook) => ResetState();
        private void DashStarted() => SetBlocking(false);
        private void Update()
        {
            if (stats.IsDead) { ResetState(); return; }
            if (!IsStunned) control.RemoveRestriction(stunToken);
            if (movement.IsDashing || IsStunned || PauseMenu.isPaused) SetBlocking(false);
            else if (blockAction != null) SetBlocking(blockAction.action.IsPressed());
        }
        public void SetBlocking(bool value)
        {
            value &= isActiveAndEnabled && !stats.IsDead && !IsStunned && !movement.IsDashing &&
                !PauseMenu.isPaused && control.IsControllable;
            if (value && !blocking && !control.ActionsControllable) return;
            bool entering = value && !blocking;
            blocking = value;
            if (value)
            {
                control.AddRestriction(blockToken, PlayerControl.ControlRestriction.Actions | PlayerControl.ControlRestriction.Shooting);
                if (entering) weapons?.reloadBehaviour?.StopReload();
            }
            else control.RemoveRestriction(blockToken);
        }
        public void TriggerStun(float seconds)
        {
            if (!isActiveAndEnabled || stats.IsDead || seconds <= 0 || float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
            stunUntil = Time.time + seconds;
            SetBlocking(false);
            control.AddRestriction(stunToken, PlayerControl.ControlRestriction.Movement |
                PlayerControl.ControlRestriction.Actions | PlayerControl.ControlRestriction.Shooting);
            weapons?.reloadBehaviour?.StopReload();
            var states = GetComponent<PlayerStates>();
            if (states != null && states.CurrentState != null && states._States != null)
                states.ForceChangeState(states._States.Default());
            movement.grapplingHookBehaviour?.Exit();
        }
        public float FilterDamage(float amount, DamageContext context)
        {
            Vector3 direction = context.Attacker != null ? context.Attacker.position - transform.position : Vector3.zero;
            return FPSCombatRules.BlockDamage(amount, IsBlocking, movement.Orientation.Forward, direction,
                context.Kind, blockHalfAngle, damageReduction);
        }
        private void ResetState()
        {
            blocking = false; stunUntil = 0;
            control.RemoveRestriction(blockToken);
            control.RemoveRestriction(stunToken);
        }
    }
}
