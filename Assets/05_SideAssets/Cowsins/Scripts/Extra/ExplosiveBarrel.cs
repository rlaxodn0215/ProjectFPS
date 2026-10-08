/// <summary>
/// This script belongs to cowsins™ as a part of the cowsins´ FPS Engine. All rights reserved. 
/// </summary>
using UnityEngine;

namespace cowsins
{
    /// <summary>
    /// Inheriting from destructible, lets you explode barrels
    /// </summary>
    public class ExplosiveBarrel : Destructible
    {
        [SerializeField] private float explosionRadius;

        [SerializeField] private float explosionForce;

        [SerializeField] private bool hurtPlayer = true;

        [Tooltip("Damage dealt on explosion to any Damageable object within the radius." +
            "NOTE:Damage will be scaled depending on how far the object is from the center of the explosion "), SerializeField]
        private float damage;

        [Header("Effects")]
        [Tooltip("Instantiate this when the barrel explodes"), SerializeField]
        private GameObject destroyedObject, explosionVFX;

        /// <summary>
        /// Override the method from Destructible.cs
        /// Here we are damaging IDamageables within a certain radius & also instantiating some effect on destructed.
        /// </summary>
        public override void Die()
        {
            SoundManager.Instance.PlaySoundAtPosition(destroyedSFX,transform.position, 0, .1f, true);
            Collider[] cols = Physics.OverlapSphere(transform.position, explosionRadius);

            Instantiate(destroyedObject, transform.position, Quaternion.identity);
            Instantiate(explosionVFX, transform.position, Quaternion.identity);

            foreach (var entry in DamageService.GatherExplosionTargets(cols, transform.position))
            {
                var collider = entry.Value;
                if (!hurtPlayer && collider.GetComponentInParent<PlayerStats>() != null) continue;
                float dmg = damage / (Vector3.Distance(collider.transform.position, transform.position) + 0.1f);
                DamageService.RequestDamage(entry.Key, dmg, false,
                    new DamageContext(null, collider, DamageKind.Environmental));
                var movement = collider.GetComponentInParent<PlayerMovement>();
                if (movement != null && movement.TryGetComponent<CameraEffects>(out var effects))
                    effects.ExplosionShake(Vector3.Distance(effects.transform.position, transform.position));
            }

            foreach (Collider c in cols)
            {
                bool isPlayer = c.CompareTag("Player") || (c.CompareTag("Enemy") && c.GetComponentInParent<PlayerStats>() != null);
                if (isPlayer && !hurtPlayer) continue;

                if (c.TryGetComponent<Rigidbody>(out Rigidbody rb))
                {
                    rb.AddExplosionForce(
                        explosionForce / (Vector3.Distance(c.transform.position, transform.position) + 0.1f),
                        transform.position,
                        explosionRadius,
                        5f,
                        ForceMode.Impulse
                    );
                }
            }
            base.Die();
        }
    }
}
