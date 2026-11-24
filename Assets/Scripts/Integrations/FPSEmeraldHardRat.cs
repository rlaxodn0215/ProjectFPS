using cowsins;
using EmeraldAI;
using UnityEngine;
using UnityEngine.AI;

namespace ProjectFPS
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EmeraldSystem))]
    public sealed class FPSEmeraldHardRat : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float dodgeChance = 0.3f;
        [SerializeField, Min(0f)] private float dodgeCooldown = 4f;
        [SerializeField] private float DodgeDistance = 1.25f;
        [SerializeField] private float DodgeDuration = 0.15f;

        private const float GroundClearance = 0.05f;
        private readonly RaycastHit[] sweepHits = new RaycastHit[16];

        private EmeraldSystem system;
        private EmeraldHealth health;
        private EmeraldDetection detection;
        private EmeraldAnimation animationState;
        private EmeraldCombat combat;
        private EmeraldMovement movement;
        private EmeraldSounds sounds;
        private NavMeshAgent agent;
        private BoxCollider[] hitboxes;
        private Transform ambushTarget;
        private Transform ambushCamera;
        private bool consideredAmbush;
        private bool ambushing;
        private float nextDodgeTime;
        private bool dodging;
        private Vector3 dodgeStart;
        private Vector3 dodgeDirection;
        private float dodgeElapsed;

        private void Awake()
        {
            system = GetComponent<EmeraldSystem>();
            health = GetComponent<EmeraldHealth>();
            detection = GetComponent<EmeraldDetection>();
            animationState = GetComponent<EmeraldAnimation>();
            combat = GetComponent<EmeraldCombat>();
            movement = GetComponent<EmeraldMovement>();
            sounds = GetComponent<EmeraldSounds>();
            agent = GetComponent<NavMeshAgent>();
            hitboxes = GetComponentsInChildren<BoxCollider>();
        }

        private void OnEnable()
        {
            detection.OnEnemyTargetDetected += BeginApproach;
            animationState.OnStartAttackAnimation += EndAmbush;
            combat.OnExitCombat += EndEncounter;
            health.OnDamageResolved += OnDamageResolved;
            health.OnDeath += ResetLife;
            health.OnLifeStarted += ResetLife;
        }

        private void OnDisable()
        {
            detection.OnEnemyTargetDetected -= BeginApproach;
            animationState.OnStartAttackAnimation -= EndAmbush;
            combat.OnExitCombat -= EndEncounter;
            health.OnDamageResolved -= OnDamageResolved;
            health.OnDeath -= ResetLife;
            health.OnLifeStarted -= ResetLife;
            ResetLife();
        }

        private void BeginApproach()
        {
            if (consideredAmbush) return;
            consideredAmbush = true;
            ambushTarget = system.CombatTarget;
            if ((!TryGetComponent<FPSEmeraldRatAlert>(out var alert) || !alert.IsAlerting) &&
                ambushTarget && ambushTarget.TryGetComponent<FPSEmeraldPlayerAdapter>(out _) &&
                ambushTarget.TryGetComponent<WeaponController>(out var weapons) && weapons.MainCamera)
            {
                ambushCamera = weapons.MainCamera.transform;
                if (CanContinueAmbush())
                {
                    ambushing = true;
                    movement.CurrentMovementState = EmeraldMovement.MovementStates.Walk;
                    return;
                }
            }

            sounds.PlayWarningSound();
        }

        private void LateUpdate()
        {
            if (ambushing)
            {
                if (!CanContinueAmbush()) AbortAmbush();
                return;
            }

            if (dodging) AdvanceDodge();
            else TryStartDodge();
        }

        private bool CanContinueAmbush()
        {
            if (!ambushTarget || !ambushCamera || system.CombatTarget != ambushTarget ||
                !agent.isActiveAndEnabled || !agent.isOnNavMesh) return false;

            var forward = Vector3.ProjectOnPlane(ambushCamera.forward, Vector3.up);
            var toRat = Vector3.ProjectOnPlane(transform.position - ambushCamera.position, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.ProjectOnPlane(ambushTarget.forward, Vector3.up);
            if (Vector3.Dot(forward.normalized, toRat.normalized) > -0.5f) return false;

            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            return NavMesh.SamplePosition(ambushTarget.position, out var target, 0.8f, filter) &&
                !NavMesh.Raycast(agent.nextPosition, target.position, out _, filter);
        }

        private void OnDamageResolved(int damage, Transform attacker)
        {
            if (damage <= 0 || !ambushing) return;
            if (health.CurrentHealth > 0) AbortAmbush();
            else EndAmbush();
        }

        private void AbortAmbush()
        {
            EndAmbush();
            if (combat.CombatState && system.CombatTarget) sounds.PlayWarningSound();
        }

        private void EndAmbush()
        {
            if (ambushing) movement.CurrentMovementState = EmeraldMovement.MovementStates.Run;
            ambushing = false;
            ambushTarget = null;
            ambushCamera = null;
        }

        private void EndEncounter()
        {
            FinishDodge();
            EndAmbush();
            consideredAmbush = false;
        }

        private void ResetLife()
        {
            EndEncounter();
            nextDodgeTime = 0f;
            if (system && system.AIAnimator && system.AIAnimator.isActiveAndEnabled)
                system.AIAnimator.ResetTrigger("Dodge Triggered");
        }

        private void TryStartDodge()
        {
            var animator = system.AIAnimator;
            var target = system.CombatTarget;
            if (Time.time < nextDodgeTime || !combat.CombatState || !target ||
                !target.TryGetComponent<FPSEmeraldPlayerAdapter>(out _) || detection.TargetObstructed ||
                movement.DefaultMovementPaused || !agent.isActiveAndEnabled || !agent.isOnNavMesh ||
                agent.isOnOffMeshLink || !agent.updatePosition || agent.isStopped || agent.velocity.sqrMagnitude < 0.01f ||
                !animator || !animator.isActiveAndEnabled || !animator.runtimeAnimatorController ||
                !animationState.IsMoving || animationState.IsDead || animationState.IsAttacking ||
                animationState.AttackTriggered || animationState.IsDodging || animationState.InternalDodge ||
                animationState.IsStunned || animator.GetBool("Attack") || animator.GetBool("Stunned Active")) return;

            if (!IsAimedAt(target)) return;

            nextDodgeTime = Time.time + dodgeCooldown;
            if (dodgeChance <= 0f || (dodgeChance < 1f && Random.value >= dodgeChance)) return;

            var right = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
            if (right == Vector3.zero) return;
            int side = Random.value < 0.5f ? -1 : 1;
            dodgeStart = agent.nextPosition;
            if (!TryDodgeStep(dodgeStart, dodgeStart + right * (side * DodgeDistance), out _))
            {
                side = -side;
                if (!TryDodgeStep(dodgeStart, dodgeStart + right * (side * DodgeDistance), out _)) return;
            }

            dodgeDirection = right * side;
            dodgeElapsed = 0f;
            dodging = true;
            movement.DefaultMovementPaused = true;
            agent.ResetPath();
            movement.SetActionDirection();
            animator.SetInteger("Dodge Direction", side < 0 ? 0 : 2);
            animator.SetBool("Walk Backwards", false);
            animator.SetTrigger("Dodge Triggered");
        }

        private bool IsAimedAt(Transform target)
        {
            if (!target.TryGetComponent<WeaponController>(out var weapons) || !weapons.Weapon ||
                !weapons.MainCamera) return false;

            var cameraTransform = weapons.MainCamera.transform;
            if (!Physics.Raycast(cameraTransform.position, cameraTransform.forward, out var hit,
                weapons.Weapon.bulletRange, weapons.settings.hitLayer)) return false;

            foreach (var hitbox in hitboxes)
            {
                if (hit.collider == hitbox) return true;
            }
            return false;
        }

        private void AdvanceDodge()
        {
            if (!combat.CombatState || !system.CombatTarget || animationState.IsDead ||
                animationState.IsStunned || animationState.IsAttacking || !agent.isActiveAndEnabled ||
                !agent.isOnNavMesh || system.AIAnimator.GetBool("Stunned Active"))
            {
                FinishDodge();
                return;
            }

            dodgeElapsed += Time.deltaTime;
            var wanted = dodgeStart + dodgeDirection * (DodgeDistance * Mathf.Clamp01(dodgeElapsed / DodgeDuration));
            var from = agent.nextPosition;
            if (!TryDodgeStep(from, wanted, out var destination))
            {
                FinishDodge();
                return;
            }

            agent.Move(destination - from);
            if (dodgeElapsed >= DodgeDuration) FinishDodge();
        }

        private bool TryDodgeStep(Vector3 from, Vector3 wanted, out Vector3 destination)
        {
            destination = from;
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            wanted.y = from.y;
            if (!NavMesh.SamplePosition(wanted, out var hit, 0.8f, filter) ||
                Vector3.ProjectOnPlane(hit.position - wanted, Vector3.up).sqrMagnitude > 0.0025f ||
                NavMesh.Raycast(from, hit.position, out _, filter)) return false;

            var offset = hit.position - from;
            if (offset.sqrMagnitude > 0.000001f)
            {
                foreach (var box in hitboxes)
                {
                    if (!box || !box.enabled) continue;
                    var boxTransform = box.transform;
                    var scale = boxTransform.lossyScale;
                    var half = Vector3.Scale(box.size * 0.5f, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
                    var center = boxTransform.TransformPoint(box.center) + (from - transform.position) + Vector3.up * GroundClearance;
                    int count = Physics.BoxCastNonAlloc(center, half, offset.normalized, sweepHits,
                        boxTransform.rotation, offset.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                    if (count == sweepHits.Length) return false;
                    for (int i = 0; i < count; i++)
                    {
                        var blocker = sweepHits[i].collider;
                        if (blocker && !blocker.transform.IsChildOf(transform) && sweepHits[i].normal.y < 0.7071f)
                            return false;
                    }
                }
            }

            destination = hit.position;
            return true;
        }

        private void FinishDodge()
        {
            if (!dodging) return;
            dodging = false;
            movement.DefaultMovementPaused = false;
            if (system.AIAnimator && system.AIAnimator.isActiveAndEnabled)
                system.AIAnimator.ResetTrigger("Dodge Triggered");
        }
    }
}
