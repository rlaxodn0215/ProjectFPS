/// <summary>
/// This script belongs to cowsins™ as a part of the cowsins´ FPS Engine. All rights reserved. 
/// </summary>using UnityEngine;
using UnityEngine;

namespace cowsins
{
    public class Bullet : MonoBehaviour, IBullet
    {
        [HideInInspector] public float Speed { get; set; }
        [HideInInspector] public float Damage { get; set; }
        [HideInInspector] public Vector3 Destination { get; set; }
        [HideInInspector] public bool Gravity { get; set; }
        [HideInInspector] public Transform Player { get; set; }
        [HideInInspector] public bool HurtsPlayer { get; set; }
        [HideInInspector] public bool ExplosionOnHit { get; set; }
        [HideInInspector] public GameObject ExplosionVFX { get; set; }
        [HideInInspector] public float ExplosionRadius { get; set; }
        [HideInInspector] public float ExplosionForce { get; set; }
        [HideInInspector] public float CriticalMultiplier { get; set; }
        [HideInInspector] public float Duration { get; set; }

        [SerializeField] private LayerMask projectileHitLayer;


        private bool projectileHasAlreadyHit = false; // Prevent from double hitting issues
        private bool despawnRequested;

        private void Start()
        {
            transform.LookAt(Destination);
            Invoke(nameof(DestroyProjectile), Duration);
        }

        private void Update()
        {
            transform.Translate(0.0f, 0.0f, Speed * Time.deltaTime);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (projectileHasAlreadyHit || other.gameObject.layer == LayerMask.NameToLayer("Effects")) return;

            if (Player != null && other.transform.root == Player.root) return;
            IDamageable damageable = CowsinsUtilities.GatherDamageableParent(other.transform);
            if (damageable != null)
            {
                bool critical = other.CompareTag(CowsinsUtilities.CRITICAL_TAG);
                DamageTarget(damageable, critical ? Damage * CriticalMultiplier : Damage, critical, other);
            }
            else if (IsGroundOrObstacleLayer(other.gameObject.layer))
            {
                DestroyProjectile();
            }
        }

        private void DamageTarget(IDamageable target, float dmg, bool isCritical, Collider hitCollider)
        {
            if (target != null)
            {
                projectileHasAlreadyHit = true;
                DamageService.RequestDamage(target, dmg, isCritical,
                    new DamageContext(Player, hitCollider, DamageKind.Projectile));
                DestroyProjectile();
            }
        }

        private bool IsGroundOrObstacleLayer(int layer)
        {
            return (projectileHitLayer.value & (1 << layer)) != 0;
        }

        private void DestroyProjectile()
        {
            if (!isActiveAndEnabled || despawnRequested) return;
            despawnRequested = true;
            projectileHasAlreadyHit = true;
            CancelInvoke(nameof(DestroyProjectile));
            if (ExplosionOnHit)
            {
                if (ExplosionVFX != null)
                {
                    var contact = GetComponent<Collider>().ClosestPoint(transform.position);
                    Instantiate(ExplosionVFX, contact, Quaternion.identity);
                }

                Collider[] colliders = Physics.OverlapSphere(transform.position, ExplosionRadius);

                foreach (var entry in DamageService.GatherExplosionTargets(colliders, transform.position))
                {
                    var collider = entry.Value;
                    bool isShooter = Player != null && collider.transform.root == Player.root;
                    if (isShooter && !HurtsPlayer) continue;
                    float distance = Vector3.Distance(collider.ClosestPoint(transform.position), transform.position);
                    float distanceRatio = ExplosionRadius > 0 ? 1 - Mathf.Clamp01(distance / ExplosionRadius) : 0;
                    DamageService.RequestDamage(entry.Key, Damage * distanceRatio, false,
                        new DamageContext(Player, collider, DamageKind.Explosion));
                }

                var shakenPlayers = new System.Collections.Generic.HashSet<PlayerMovement>();
                var pushedBodies = new System.Collections.Generic.HashSet<Rigidbody>();
                foreach (var collider in colliders)
                {
                    var playerMovement = collider.GetComponentInParent<PlayerMovement>();
                    var rigidbody = collider.attachedRigidbody;
                    if (playerMovement != null && shakenPlayers.Add(playerMovement))
                    {
                        CameraEffects cameraEffects = playerMovement.GetComponent<CameraEffects>();
                        if (cameraEffects != null)
                            cameraEffects.ExplosionShake(Vector3.Distance(cameraEffects.transform.position, transform.position));
                    }

                    if (rigidbody != null && rigidbody.gameObject != gameObject && pushedBodies.Add(rigidbody))
                    {
                        rigidbody.AddExplosionForce(ExplosionForce, transform.position, ExplosionRadius, 5, ForceMode.Force);
                    }
                }
            }

            SpawnService.Despawn(gameObject);
        }
    }
}
