using EmeraldAI;
using UnityEngine;

namespace ProjectFPS
{
    public sealed class DarknessSpiderWeb : MonoBehaviour
    {
        [SerializeField] private GameObject launchEffect;
        [SerializeField] private GameObject impactEffect;

        private const float Speed = 12f;
        private const float Lifetime = 1f;
        private const float Radius = 0.075f;
        private readonly RaycastHit[] hits = new RaycastHit[32];
        private Transform owner;
        private int damage;
        private float elapsed;

        public void Launch(Transform source, int amount)
        {
            owner = source;
            damage = amount;
            SpawnEffect(launchEffect, transform.position, transform.rotation);
        }

        private void Update()
        {
            float step = Mathf.Min(Time.deltaTime, Lifetime - elapsed);
            float distance = Speed * step;
            int count = Physics.SphereCastNonAlloc(transform.position, Radius, transform.forward,
                hits, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            float closest = float.PositiveInfinity;
            RaycastHit impact = default;
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (hit.distance >= closest || (owner && hit.transform.IsChildOf(owner))) continue;
                if (hit.collider.isTrigger && !hit.collider.GetComponentInParent<FPSEmeraldPlayerAdapter>()) continue;
                closest = hit.distance;
                impact = hit;
            }

            if (impact.collider)
            {
                var player = impact.collider.GetComponentInParent<FPSEmeraldPlayerAdapter>();
                if (player) player.ReceiveSpiderWeb(damage, owner);
                SpawnEffect(impactEffect, impact.point, Quaternion.LookRotation(impact.normal));
                Destroy(gameObject);
                return;
            }

            transform.position += transform.forward * distance;
            elapsed += step;
            if (elapsed >= Lifetime) Destroy(gameObject);
        }

        private static void SpawnEffect(GameObject effect, Vector3 position, Quaternion rotation)
        {
            if (effect) Destroy(Instantiate(effect, position, rotation), 3f);
        }
    }
}
