using System.Collections.Generic;
using EmeraldAI;
using UnityEngine;

namespace ProjectFPS.Integrations
{
    [DefaultExecutionOrder(100), DisallowMultipleComponent, RequireComponent(typeof(EmeraldSystem))]
    public sealed class FPSEmeraldCombatActions : MonoBehaviour
    {
        [SerializeField, Range(0, 1)] private float blockChance = 0.4f;
        [SerializeField, Range(0, 100)] private int blockReduction = 50;
        [SerializeField, Range(1, 3)] private float blockLength = 1;
        [SerializeField, Min(0.25f)] private float blockCooldown = 3;
        [SerializeField, Range(0, 1)] private float dodgeChance = 0.25f;
        [SerializeField, Range(0, 100)] private int dodgeReduction = 100;
        [SerializeField, Min(0.25f)] private float dodgeCooldown = 4;
        [SerializeField, Range(0, 360)] private float detectionAngle = 120;
        [SerializeField] private LayerMask projectileLayers = 1 << 2;
        [SerializeField] private LayerMask obstructionLayers;
        private EmeraldSystem system;
        private bool shotPending;
        private readonly List<ActionsClass> installed = new List<ActionsClass>();
        private void Awake() => system = GetComponent<EmeraldSystem>();
        private void OnEnable() { FPSNoiseEmitter.Emitted += Shot; foreach (var state in installed) state.Enabled = true; }
        private void OnDisable()
        {
            FPSNoiseEmitter.Emitted -= Shot; shotPending = false;
            foreach (var state in installed)
            {
                if (state.IsActive && system != null && system.AnimationComponent != null) state.emeraldAction.CancelAction(system, state);
                state.Enabled = false;
            }
        }
        private void Start()
        {
            var profile = system.AnimationComponent != null ? system.AnimationComponent.m_AnimationProfile : null;
            if (profile == null || system.AIAnimator == null || system.AIAnimator.runtimeAnimatorController == null)
            { Debug.LogError("FPSEmeraldCombatActions: Animation Profile과 생성된 Controller가 필요합니다.", this); enabled = false; return; }
            var combat = system.CombatComponent;
            if (combat.WeaponTypeAmount == EmeraldCombat.WeaponTypeAmounts.Two || combat.StartingWeaponType == EmeraldCombat.WeaponTypes.Type1)
                Install(combat.Type1CombatActions, profile.Type1Animations);
            if (combat.WeaponTypeAmount == EmeraldCombat.WeaponTypeAmounts.Two || combat.StartingWeaponType == EmeraldCombat.WeaponTypes.Type2)
                Install(combat.Type2CombatActions, profile.Type2Animations);
        }
        private void Install(List<ActionsClass> actions, AnimationParentClass animations)
        {
            if (animations == null) return;
            bool canBlock = animations.BlockIdle?.AnimationClip != null;
            bool canDodge = animations.DodgeLeft?.AnimationClip != null && animations.DodgeRight?.AnimationClip != null && animations.DodgeBack?.AnimationClip != null;
            foreach (var state in actions)
            {
                if (state.emeraldAction is BlockAction && !canBlock || state.emeraldAction is DodgeAction && !canDodge)
                { state.Enabled = false; Debug.LogWarning("FPSEmeraldCombatActions: 클립이 누락된 기존 전투 행동을 비활성화합니다.", this); }
            }
            var ready = AnimationStateTypes.Idling | AnimationStateTypes.Moving | AnimationStateTypes.Strafing |
                AnimationStateTypes.BackingUp | AnimationStateTypes.TurningLeft | AnimationStateTypes.TurningRight;
            if (!actions.Exists(x => x.emeraldAction is BlockAction))
            {
                if (!canBlock) Debug.LogWarning("FPSEmeraldCombatActions: BlockIdle 누락으로 방어를 설치하지 않습니다.", this);
                else
                {
                    var action = ScriptableObject.CreateInstance<BlockAction>();
                    action.OddsToBlock = blockChance; action.MitigationAmount = blockReduction; action.BlockLength = blockLength;
                    action.CooldownLength = blockCooldown; action.MaxBlockAngle = detectionAngle; action.ProjectileLayers = projectileLayers;
                    Configure(action, ready); Add(actions, action);
                }
            }
            if (!actions.Exists(x => x.emeraldAction is DodgeAction))
            {
                if (!canDodge) Debug.LogWarning("FPSEmeraldCombatActions: 회피 세 방향 클립 누락으로 회피를 설치하지 않습니다.", this);
                else
                {
                    var action = ScriptableObject.CreateInstance<DodgeAction>();
                    action.OddsToDodge = dodgeChance; action.MitigationAmount = dodgeReduction; action.CooldownLength = dodgeCooldown;
                    action.MaxDodgeAngle = detectionAngle; action.ProjectileLayers = projectileLayers;
                    Configure(action, ready); Add(actions, action);
                }
            }
        }
        private static void Configure(EmeraldAction action, AnimationStateTypes ready)
        {
            action.EnterConditions = action.CooldownConditions = ready;
            action.ExitConditions = AnimationStateTypes.GettingHit | AnimationStateTypes.Stunned | AnimationStateTypes.Dead;
        }
        private void Add(List<ActionsClass> list, EmeraldAction action)
        {
            var state = new ActionsClass { emeraldAction = action, CooldownLengthTimer = action.CooldownLength };
            list.Add(state); installed.Add(state);
        }
        private void Shot(FPSNoise noise)
        {
            if (noise.Kind == FPSNoiseKind.Gunshot && noise.Weapon != null && (int)noise.Weapon.shootStyle == 0 &&
                system.CombatComponent != null && system.CombatComponent.CombatState && noise.Source == system.CombatTarget) shotPending = true;
        }
        private void LateUpdate()
        {
            if (!shotPending) return;
            shotPending = false;
            var animation = system.AnimationComponent;
            var target = system.CombatTarget;
            if (target == null || !system.CombatComponent.CombatState || animation.IsDead || animation.IsStunned ||
                system.AIAnimator.GetBool("Stunned Active") || !system.DetectionComponent.GetVisibleTargets().Exists(x => x == target || x.IsChildOf(target)) ||
                Vector3.Angle(transform.forward, target.position - transform.position) > detectionAngle * 0.5f ||
                Physics.Linecast(transform.position + Vector3.up, target.position + Vector3.up, obstructionLayers, QueryTriggerInteraction.Ignore)) return;
            // Runs after synchronous hitscan/pellet damage; prepares for a future shot.
            var actions = system.CombatComponent.CurrentWeaponType == EmeraldCombat.WeaponTypes.Type1 ?
                system.CombatComponent.Type1CombatActions : system.CombatComponent.Type2CombatActions;
            foreach (var state in actions)
            {
                if (state.emeraldAction is BlockAction block && block.TryReactToShot(system, state)) break;
                if (state.emeraldAction is DodgeAction dodge && dodge.TryReactToShot(system, state)) break;
            }
        }
        private void OnDestroy()
        {
            foreach (var state in installed)
            {
                if (system != null && system.CombatComponent != null)
                {
                    system.CombatComponent.Type1CombatActions.Remove(state);
                    system.CombatComponent.Type2CombatActions.Remove(state);
                }
                Destroy(state.emeraldAction);
            }
        }
    }
}
