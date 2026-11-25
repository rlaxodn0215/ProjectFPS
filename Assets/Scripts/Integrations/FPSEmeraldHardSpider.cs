using cowsins;
using EmeraldAI;
using EmeraldAI.Utility;
using UnityEngine;

namespace ProjectFPS
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EmeraldSystem), typeof(DarknessSpiderSurfaceMovement))]
    public sealed class FPSEmeraldHardSpider : MonoBehaviour
    {
        private enum Attack { Bite, Combo, CrawlBite, Jump, Web, CrawlWeb, AmbushJump, NormalWeb }

        [SerializeField] private Transform webOrigin;

        private const float DodgeDistance = 1.25f;
        private const float DodgeDuration = 0.15f;
        private readonly RaycastHit[] sightHits = new RaycastHit[32];
        private EmeraldSystem system;
        private EmeraldHealth health;
        private EmeraldDetection detection;
        private EmeraldAnimation animationState;
        private EmeraldCombat combat;
        private DarknessSpiderSurfaceMovement movement;
        private Transform encounterTarget;
        private Transform attackTarget;
        private Transform ambushCamera;
        private Attack attack;
        private DarknessSpiderAbility activeAbility;
        private int remainingHits;
        private bool ambushing;
        private bool attackEntered;
        private float attackRequested;
        private float nextAttack;
        private float nextCombo;
        private float nextCrawl;
        private float nextWeb;
        private float nextDodge;
        private float dodgeRemaining;
        private Vector3 dodgeDirection;
        private Vector3 home;

        private void Awake()
        {
            system = GetComponent<EmeraldSystem>();
            health = GetComponent<EmeraldHealth>();
            detection = GetComponent<EmeraldDetection>();
            animationState = GetComponent<EmeraldAnimation>();
            combat = GetComponent<EmeraldCombat>();
            movement = GetComponent<DarknessSpiderSurfaceMovement>();
        }

        private void OnEnable()
        {
            home = transform.position;
            health.OnDamageResolved += Damaged;
            health.OnDeath += Died;
            health.OnLifeStarted += ResetLife;
            combat.OnExitCombat += EndEncounter;
            animationState.OnStartAttackAnimation += AttackStarted;
            animationState.OnEndAttackAnimation += EndAttack;
        }

        private void OnDisable()
        {
            health.OnDamageResolved -= Damaged;
            health.OnDeath -= Died;
            health.OnLifeStarted -= ResetLife;
            combat.OnExitCombat -= EndEncounter;
            animationState.OnStartAttackAnimation -= AttackStarted;
            animationState.OnEndAttackAnimation -= EndAttack;
            ResetLife();
        }

        internal void Tick()
        {
            Transform target = combat.CombatState ? system.CombatTarget : null;
            if (target != encounterTarget)
            {
                EndEncounter();
                encounterTarget = target;
                ambushing = TryGetPlayerCamera(target, out ambushCamera) && CanAmbush(target);
                if (ambushing) movement.InterruptClimb();
                if (target) system.BehaviorsComponent.BehaviorState = "Aggressive";
            }
            if (ambushing && !CanAmbush(target)) ambushing = false;

            if (movement.OnSurface && !movement.HasSupport())
            {
                EndAttack();
                dodgeRemaining = 0f;
                movement.BeginFall();
            }

            if (animationState.IsStunned || animationState.InternalHit || animationState.IsGettingHit ||
                system.AIAnimator.GetBool("Stunned Active"))
            {
                EndAttack();
                dodgeRemaining = 0f;
                movement.Stop();
                if (movement.Falling) MoveNormally(target, 0f);
                AnimateMovement(0f);
                return;
            }

            if (movement.Falling)
            {
                MoveNormally(target, 0f);
                AnimateMovement(0f);
                return;
            }

            if (activeAbility)
            {
                if (!attackEntered && Time.time > attackRequested + 0.5f) EndAttack();
                else
                {
                    bool moving = attack == Attack.CrawlBite || attack == Attack.CrawlWeb;
                    if (moving && attackTarget)
                    {
                        Vector3 direction = attackTarget.position - transform.position;
                        if (direction.magnitude > 0.5f) movement.Move(direction, 1.5f * Time.deltaTime, false);
                    }
                    AnimateMovement(moving ? 0.5f : 0f);
                    return;
                }
            }

            if (animationState.IsAttacking)
            {
                movement.Stop();
                AnimateMovement(0f);
                return;
            }

            if (dodgeRemaining > 0f || (target && TryDodge(target)))
            {
                float step = Mathf.Min(dodgeRemaining, DodgeDistance / DodgeDuration * Time.deltaTime);
                dodgeRemaining = movement.Move(dodgeDirection, step, false) ? dodgeRemaining - step : 0f;
                AnimateMovement(0.5f);
                return;
            }

            if (target)
            {
                Vector3 toward = target.position - transform.position;
                bool visible = HasLineOfSight(target, JawPosition, TargetPoint(target));
                if (visible && Time.time >= nextAttack && ChooseAttack(toward, out var selected))
                {
                    movement.Face(toward);
                    Vector3 tangent = Vector3.ProjectOnPlane(toward, movement.Normal);
                    if (tangent.sqrMagnitude < 0.01f || Vector3.Angle(transform.forward, tangent) <= 30f)
                    {
                        StartAttack(selected, target);
                        return;
                    }
                    movement.Stop();
                    AnimateMovement(0f);
                    return;
                }
            }

            float speed = !target || ambushing ? system.MovementComponent.WalkSpeed : system.MovementComponent.RunSpeed;
            MoveNormally(target, speed);
            AnimateMovement(movement.Moving ? (target && !ambushing ? 1f : 0.5f) : 0f);
            system.BehaviorsComponent.DetectTargetTracker();
        }

        private void MoveNormally(Transform target, float speed)
        {
            movement.Tick(target, speed, !ambushing, 0.45f, system.MovementComponent.WanderRadius, home);
        }

        private bool CanAmbush(Transform target)
        {
            if (!target || !ambushCamera || !movement.HasGroundPath(target.position)) return false;
            Vector3 forward = Vector3.ProjectOnPlane(ambushCamera.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.ProjectOnPlane(target.forward, Vector3.up);
            Vector3 offset = Vector3.ProjectOnPlane(transform.position - ambushCamera.position, Vector3.up);
            return Vector3.Dot(forward.normalized, offset.normalized) <= -0.5f;
        }

        private static bool TryGetPlayerCamera(Transform target, out Transform camera)
        {
            camera = null;
            if (!target || !target.TryGetComponent<FPSEmeraldPlayerAdapter>(out _) ||
                !target.TryGetComponent<WeaponController>(out var weapons) || !weapons.MainCamera) return false;
            camera = weapons.MainCamera.transform;
            return true;
        }

        private bool TryDodge(Transform target)
        {
            if (ambushing || !movement.Moving || Time.time < nextDodge ||
                !TryGetPlayerCamera(target, out var camera)) return false;
            var weapons = target.GetComponent<WeaponController>();
            if (!weapons.Weapon || !Physics.Raycast(camera.position, camera.forward, out var hit,
                weapons.Weapon.bulletRange, weapons.settings.hitLayer) || !hit.transform.IsChildOf(transform)) return false;
            nextDodge = Time.time + 1.25f;
            if (Random.value >= 0.75f) return false;

            Vector3 side = transform.right * (Random.value < 0.5f ? -1f : 1f);
            if (!movement.CanMove(side, DodgeDistance))
            {
                side = -side;
                if (!movement.CanMove(side, DodgeDistance)) return false;
            }

            movement.Stop();
            dodgeDirection = side;
            dodgeRemaining = DodgeDistance;
            return true;
        }

        private bool ChooseAttack(Vector3 toward, out Attack selected)
        {
            float distance = toward.magnitude;
            if (distance < 0.65f)
                selected = !ambushing && Time.time >= nextCombo ? Attack.Combo : Attack.Bite;
            else if (distance <= 1.6f)
            {
                selected = ambushing ? Attack.AmbushJump : Attack.Jump;
                if (!ambushing && distance <= 1.2f && movement.Moving && Time.time >= nextCrawl &&
                    Vector3.Dot(transform.forward, toward.normalized) > 0.7f && movement.CanMove(toward, 0.35f))
                    selected = Attack.CrawlBite;
            }
            else
            {
                selected = movement.Moving ? Attack.CrawlWeb : nextWeb == 0f ? Attack.NormalWeb : Attack.Web;
                return !ambushing && distance <= 8f && Time.time >= nextWeb;
            }
            return true;
        }

        private void StartAttack(Attack selected, Transform target)
        {
            attack = selected;
            attackTarget = target;
            attackRequested = Time.time;
            attackEntered = false;
            var data = combat.Type1Attacks.AttackDataList[(int)attack];
            activeAbility = (DarknessSpiderAbility)data.AbilityObject;
            remainingHits = attack == Attack.Combo ? 3 : 1;
            movement.Stop();
            EmeraldCombatManager.SetAttackValues(system, data, false);
            combat.AttackPosition = target.position - transform.position;
            nextAttack = Time.time + combat.Type1AttackCooldown;
            animationState.PlayAttackAnimation();
        }

        private void AttackStarted()
        {
            if (!activeAbility) return;
            attackEntered = true;
            ambushing = false;
            if (attack == Attack.Combo) nextCombo = Time.time + 4f;
            if (attack == Attack.CrawlBite) nextCrawl = Time.time + 2f;
            if (activeAbility.Web) nextWeb = Time.time + 4f;
        }

        internal void ResolveAttack(DarknessSpiderAbility ability)
        {
            if (ability != activeAbility || remainingHits <= 0 || !attackTarget ||
                attackTarget != system.CombatTarget || health.CurrentHealth <= 0 || movement.Falling) return;
            remainingHits--;
            Vector3 targetPoint = TargetPoint(attackTarget);
            Vector3 origin = ability.Web && webOrigin ? webOrigin.position : JawPosition;
            if (!HasLineOfSight(attackTarget, origin, targetPoint)) return;

            if (ability.Web)
            {
                Vector3 direction = targetPoint - origin;
                if (direction.sqrMagnitude < 0.0001f) return;
                var projectile = Instantiate(ability.Web, origin, Quaternion.LookRotation(direction));
                projectile.Launch(transform, ability.Damage);
                return;
            }

            Vector3 offset = attackTarget.position - transform.position;
            if (offset.sqrMagnitude > 1.9f * 1.9f || Vector3.Dot(transform.forward, offset) < -0.05f) return;
            EmeraldDamageDispatch.Damage(attackTarget.GetComponent<EmeraldAI.IDamageable>(), ability.Damage,
                transform, 100, false, EmeraldAttackKind.Melee);
        }

        private bool HasLineOfSight(Transform target, Vector3 origin, Vector3 point)
        {
            Vector3 direction = point - origin;
            int count = Physics.RaycastNonAlloc(origin, direction, sightHits, direction.magnitude,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Transform hit = sightHits[i].transform;
                if (!hit.IsChildOf(transform) && !hit.IsChildOf(target)) return false;
            }
            return count < sightHits.Length;
        }

        private static Vector3 TargetPoint(Transform target) =>
            target.TryGetComponent<ICombat>(out var combatTarget) ? combatTarget.DamagePosition() : target.position;

        private Vector3 JawPosition => detection.HeadTransform.position;

        private void AnimateMovement(float speed)
        {
            system.AIAnimator.SetFloat("Speed", speed, 0.08f, Time.deltaTime);
            system.AIAnimator.SetFloat("Direction", 0f);
        }

        private void Damaged(int damage, Transform source)
        {
            if (damage > 0) ambushing = false;
        }

        private void EndAttack()
        {
            if (activeAbility && system.AIAnimator && system.AIAnimator.isActiveAndEnabled)
                system.AIAnimator.ResetTrigger("Attack");
            activeAbility = null;
            attackTarget = null;
            remainingHits = 0;
        }

        private void EndEncounter()
        {
            EndAttack();
            ambushing = false;
            encounterTarget = null;
            ambushCamera = null;
            dodgeRemaining = 0f;
        }

        private void Died()
        {
            EndEncounter();
            movement.Stop();
        }

        private void ResetLife()
        {
            EndEncounter();
            nextAttack = nextCombo = nextCrawl = nextWeb = nextDodge = 0f;
            movement.ResetMotion();
        }
    }
}
