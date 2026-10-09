using EmeraldAI;
using UnityEngine;
using UnityEngine.AI;

namespace ProjectFPS.Integrations
{
    [DefaultExecutionOrder(100), DisallowMultipleComponent, RequireComponent(typeof(EmeraldSystem))]
    public sealed class FPSEmeraldHearing : MonoBehaviour
    {
        [SerializeField] private LayerMask obstructionLayers;
        [SerializeField, Range(0, 1)] private float obstructionMultiplier = 0.5f;
        [SerializeField, Min(0)] private float investigationSeconds = 5;
        [Tooltip("조사 위치로 이동하는 최대 시간입니다.")]
        [SerializeField, Min(0.1f)] private float travelTimeout = 15;
        private EmeraldSystem system;
        private EmeraldHealth health;
        private bool investigating;
        private bool waiting;
        private float deadline;
        private bool originalPaused;
        private bool originalStopped;
        private Vector3 originalDestination;
        private bool originalHadPath;
        public Vector3 LastHeardPosition { get; private set; }
        public bool IsInvestigating => investigating;

        private void Awake() { system = GetComponent<EmeraldSystem>(); health = GetComponent<EmeraldHealth>(); }
        private void OnEnable() { FPSNoiseEmitter.Emitted += Hear; health.OnDeath += Died; }
        private void OnDisable() { FPSNoiseEmitter.Emitted -= Hear; health.OnDeath -= Died; Finish(true); }
        private void Died() => Finish(false);
        private void Start()
        {
            if (system.m_NavMeshAgent == null || !system.m_NavMeshAgent.enabled || !system.m_NavMeshAgent.isOnNavMesh)
            { Debug.LogError("FPSEmeraldHearing: 활성 NavMeshAgent와 베이크된 NavMesh가 필요합니다.", this); enabled = false; }
        }
        private void Hear(FPSNoise noise)
        {
            if (system.CombatComponent == null || system.AnimationComponent == null || system.AnimationComponent.IsDead ||
                system.CombatComponent.CombatState || system.TargetToFollow != null || noise.Source == null ||
                noise.Source.GetComponent<cowsins.PlayerStats>() == null ||
                system.DetectionComponent.GetTargetFactionRelation(noise.Source) != "Enemy") return;
            var agent = system.m_NavMeshAgent;
            if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;
            if (!investigating && system.MovementComponent.DefaultMovementPaused) return;
            bool blocked = Physics.Linecast(transform.position + Vector3.up, noise.Position + Vector3.up,
                obstructionLayers, QueryTriggerInteraction.Ignore);
            if (!FPSCombatRules.CanHear(Vector3.Distance(transform.position, noise.Position), noise.Radius, blocked, obstructionMultiplier)) return;
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            if (!NavMesh.SamplePosition(noise.Position, out var hit, 2f, filter)) return;
            var path = new NavMeshPath();
            if (!agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete) return;
            if (!investigating)
            {
                originalPaused = system.MovementComponent.DefaultMovementPaused;
                originalStopped = agent.isStopped;
                originalHadPath = agent.hasPath;
                originalDestination = originalHadPath ? agent.destination : transform.position;
            }
            LastHeardPosition = noise.Position;
            investigating = true; waiting = false;
            deadline = Time.time + travelTimeout;
            system.MovementComponent.DefaultMovementPaused = true;
            agent.isStopped = false;
            agent.SetPath(path);
        }
        private void Update()
        {
            if (!investigating) return;
            if (system.AnimationComponent.IsDead || system.CombatComponent.CombatState) { Finish(false); return; }
            var agent = system.m_NavMeshAgent;
            if (!agent.enabled || !agent.isOnNavMesh) { Finish(false); return; }
            if (!waiting && !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.2f)
            { waiting = true; deadline = Time.time + investigationSeconds; agent.isStopped = true; }
            if (Time.time >= deadline) Finish(true);
        }
        private void Finish(bool restorePath)
        {
            if (!investigating) return;
            investigating = false;
            if (system.MovementComponent != null) system.MovementComponent.DefaultMovementPaused = originalPaused;
            var agent = system.m_NavMeshAgent;
            if (agent == null || !agent.enabled || !agent.isOnNavMesh || system.AnimationComponent.IsDead) return;
            agent.isStopped = originalStopped;
            if (!restorePath || system.CombatComponent.CombatState) return;
            if (originalHadPath) agent.SetDestination(originalDestination);
            else agent.ResetPath();
        }
    }
}
