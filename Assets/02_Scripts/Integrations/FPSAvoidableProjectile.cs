using EmeraldAI;
using UnityEngine;

namespace ProjectFPS.Integrations
{
    [DisallowMultipleComponent]
    public sealed class FPSAvoidableProjectile : MonoBehaviour, IAvoidable
    {
        [SerializeField, Min(0.1f)] private float predictionDistance = 10;
        private FPSAvoidableProjectile owner;
        private Transform target;
        private cowsins.Bullet bullet;
        public Transform AbilityTarget { get => owner != null ? owner.AbilityTarget : target; set => target = value; }
        public void Initialize(Transform initialTarget)
        {
            target = initialTarget;
            bullet = GetComponent<cowsins.Bullet>();
            foreach (var collider in GetComponentsInChildren<Collider>())
            {
                if (collider.gameObject == gameObject) continue;
                var child = collider.GetComponent<FPSAvoidableProjectile>() ?? collider.gameObject.AddComponent<FPSAvoidableProjectile>();
                child.owner = this;
            }
        }
        private void Update()
        {
            if (owner != null) return;
            var hits = Physics.RaycastAll(transform.position, transform.forward, predictionDistance, ~0, QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            target = null;
            foreach (var hit in hits)
            {
                if (hit.collider.transform.IsChildOf(transform) ||
                    bullet != null && bullet.Player != null && hit.collider.GetComponentInParent<cowsins.PlayerStats>()?.transform == bullet.Player) continue;
                var ai = hit.collider.GetComponentInParent<EmeraldSystem>();
                if (ai != null) { target = ai.transform; break; }
                if (!hit.collider.isTrigger) break;
            }
        }
    }
}
