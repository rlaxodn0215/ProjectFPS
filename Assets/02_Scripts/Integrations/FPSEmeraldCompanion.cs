using cowsins;
using EmeraldAI;
using UnityEngine;
using UnityEngine.AI;

namespace ProjectFPS.Integrations
{
    public enum FPSAllyCommand { Follow, GuardHere, MoveAndGuard, Attack }
    public enum FPSCommandFailure { None, Unavailable, NotOwned, InvalidTarget, Unreachable, PlayerRestricted }
    public readonly struct FPSCommandResult
    {
        public readonly FPSEmeraldCompanion Companion;
        public readonly FPSCommandFailure Failure;
        public bool Success => Failure == FPSCommandFailure.None;
        public FPSCommandResult(FPSEmeraldCompanion companion, FPSCommandFailure failure) { Companion = companion; Failure = failure; }
    }

    [DefaultExecutionOrder(100), DisallowMultipleComponent, RequireComponent(typeof(EmeraldSystem))]
    public sealed class FPSEmeraldCompanion : MonoBehaviour
    {
        [SerializeField] private PlayerStats owner;
        public PlayerStats Owner => owner;
        private EmeraldSystem system;
        private bool guarding;
        private Vector3 guardPosition;
        private Transform commandedTarget;
        private bool wasAttacking;
        private bool previousInfiniteChase;
        public bool IsAvailable => isActiveAndEnabled && system.AnimationComponent != null && !system.AnimationComponent.IsDead &&
            !system.AnimationComponent.IsStunned && system.AIAnimator != null && !system.AIAnimator.GetBool("Stunned Active") &&
            system.HealthComponent != null && system.HealthComponent.CurrentHealth > 0 && system.m_NavMeshAgent != null &&
            system.m_NavMeshAgent.enabled && system.m_NavMeshAgent.isOnNavMesh;

        private void Awake() => system = GetComponent<EmeraldSystem>();
        private void Start()
        {
            if (owner == null || !IsAvailable)
            { Debug.LogError("FPSEmeraldCompanion: 소유 플레이어와 활성 NavMeshAgent를 지정하세요.", this); enabled = false; return; }
            Follow();
            EmeraldAPI.Behaviors.ChangeBehavior(system, EmeraldBehaviors.BehaviorTypes.Aggressive);
        }
        public FPSCommandResult Execute(PlayerStats caller, FPSAllyCommand command, Vector3 point = default, Transform target = null)
        {
            if (!IsAvailable) return Result(FPSCommandFailure.Unavailable);
            if (caller == null || caller != owner) return Result(FPSCommandFailure.NotOwned);
            var control = caller.GetComponent<PlayerControl>();
            if (caller.IsDead || PauseMenu.isPaused || control == null || !control.IsControllable || !control.ActionsControllable)
                return Result(FPSCommandFailure.PlayerRestricted);
            if (command == FPSAllyCommand.Attack)
            {
                var enemy = target != null ? target.GetComponentInParent<EmeraldSystem>() : null;
                if (enemy == null || enemy == system || enemy.AnimationComponent.IsDead || enemy.HealthComponent.CurrentHealth <= 0 ||
                    system.DetectionComponent.GetTargetFactionRelation(enemy.transform) != "Enemy") return Result(FPSCommandFailure.InvalidTarget);
                if (!TryPath(enemy.transform.position, out _)) return Result(FPSCommandFailure.Unreachable);
                if (!wasAttacking) previousInfiniteChase = system.BehaviorsComponent.InfititeChase;
                wasAttacking = true;
                commandedTarget = enemy.transform;
                EmeraldAPI.Movement.ResumeFollowing(system);
                EmeraldAPI.Combat.OverrideCombatTarget(system, commandedTarget);
                return Result(FPSCommandFailure.None);
            }
            if (command == FPSAllyCommand.Follow)
            {
                StopCommandedAttack(); guarding = false; Follow();
                return Result(FPSCommandFailure.None);
            }
            if (command != FPSAllyCommand.GuardHere && command != FPSAllyCommand.MoveAndGuard) return Result(FPSCommandFailure.InvalidTarget);
            point = command == FPSAllyCommand.GuardHere ? transform.position : point;
            if (!TryPath(point, out var destination)) return Result(FPSCommandFailure.Unreachable);
            StopCommandedAttack(); guarding = true; guardPosition = destination;
            ApplyGuard();
            return Result(FPSCommandFailure.None);
        }
        private FPSCommandResult Result(FPSCommandFailure failure) => new FPSCommandResult(this, failure);
        private bool TryPath(Vector3 point, out Vector3 destination)
        {
            destination = point;
            if (!IsFinite(point)) return false;
            var agent = system.m_NavMeshAgent;
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            if (!NavMesh.SamplePosition(point, out var hit, 2f, filter)) return false;
            destination = hit.position;
            var path = new NavMeshPath();
            return agent.CalculatePath(destination, path) && path.status == NavMeshPathStatus.PathComplete;
        }
        private static bool IsFinite(Vector3 point) => !float.IsNaN(point.x) && !float.IsNaN(point.y) && !float.IsNaN(point.z) &&
            !float.IsInfinity(point.x) && !float.IsInfinity(point.y) && !float.IsInfinity(point.z);
        private void Follow()
        {
            if (system.TargetToFollow != owner.transform) EmeraldAPI.Detection.SetTargetToFollow(system, owner.transform, false);
            EmeraldAPI.Movement.StopCompanionGuardPosition(system);
            EmeraldAPI.Movement.ResumeFollowing(system);
        }
        private void ApplyGuard()
        {
            if (system.TargetToFollow != owner.transform) EmeraldAPI.Detection.SetTargetToFollow(system, owner.transform, false);
            system.m_NavMeshAgent.isStopped = false;
            EmeraldAPI.Movement.StartCompanionGuardPosition(system, guardPosition);
        }
        private void Update()
        {
            if (!IsAvailable || owner == null) return;
            if (wasAttacking)
            {
                var health = commandedTarget != null ? commandedTarget.GetComponent<EmeraldHealth>() : null;
                if (health != null && health.CurrentHealth > 0 && commandedTarget.gameObject.activeInHierarchy &&
                    system.CombatTarget == commandedTarget && system.DetectionComponent.GetTargetFactionRelation(commandedTarget) == "Enemy") return;
                StopCommandedAttack();
                if (guarding) ApplyGuard(); else Follow();
            }
            else if (guarding && !system.CombatComponent.CombatState && !system.m_NavMeshAgent.pathPending &&
                Vector3.Distance(transform.position, guardPosition) > system.m_NavMeshAgent.stoppingDistance + 0.5f) ApplyGuard();
        }
        private void StopCommandedAttack()
        {
            if (!wasAttacking) return;
            wasAttacking = false; commandedTarget = null;
            system.BehaviorsComponent.InfititeChase = previousInfiniteChase;
            system.CombatComponent.ExitCombat();
        }
        private void OnDisable()
        {
            if (wasAttacking && system.BehaviorsComponent != null) system.BehaviorsComponent.InfititeChase = previousInfiniteChase;
            wasAttacking = false; commandedTarget = null;
        }
    }
}
