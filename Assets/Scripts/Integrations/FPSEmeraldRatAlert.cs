using cowsins;
using EmeraldAI;
using UnityEngine;

namespace ProjectFPS
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EmeraldSystem))]
    public sealed class FPSEmeraldRatAlert : MonoBehaviour
    {
        [SerializeField] private float ImpactRadius = 2f;
        [SerializeField] private float ContactTolerance = 0.02f;

        private EmeraldSystem system;
        private EmeraldDetection detection;
        private EmeraldHealth health;
        private BoxCollider[] hitboxes;
        private readonly Collider[] contacts = new Collider[8];
        private readonly RaycastHit[] sightHits = new RaycastHit[16];

        // Emerald publishes its target-detected event synchronously.
        internal bool IsAlerting { get; private set; }

        private void Awake()
        {
            system = GetComponent<EmeraldSystem>();
            detection = GetComponent<EmeraldDetection>();
            health = GetComponent<EmeraldHealth>();
            hitboxes = GetComponentsInChildren<BoxCollider>();
        }

        private void OnEnable() => ShotImpact.Occurred += OnShotImpact;
        private void OnDisable() => ShotImpact.Occurred -= OnShotImpact;

        private bool CanAlert => detection.isActiveAndEnabled && health.CurrentHealth > 0 &&
            system.AnimationComponent && !system.AnimationComponent.IsDead && !system.CombatTarget &&
            !system.CombatComponent.CombatState;

        private void FixedUpdate()
        {
            if (!CanAlert) return;

            foreach (var box in hitboxes)
            {
                if (!box.enabled || !box.gameObject.activeInHierarchy) continue;
                var shape = box.transform;
                var scale = shape.lossyScale;
                var size = Vector3.Scale(box.size * 0.5f,
                    new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
                int count = Physics.OverlapBoxNonAlloc(shape.TransformPoint(box.center),
                    size + Vector3.one * ContactTolerance, contacts, shape.rotation,
                    detection.DetectionLayerMask, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                {
                    var player = contacts[i].GetComponentInParent<FPSEmeraldPlayerAdapter>();
                    if (!IsHostilePlayer(player)) continue;
                    StartPursuit(player.transform);
                    return;
                }
            }
        }

        private void OnShotImpact(Transform shooter, Vector3 point, Collider hit)
        {
            if (!CanAlert || (point - transform.position).sqrMagnitude > ImpactRadius * ImpactRadius ||
                hit.transform.IsChildOf(transform) || hit.GetComponentInParent<FPSEmeraldPlayerAdapter>()) return;
            if (hit.isTrigger && !hit.GetComponentInParent<EmeraldSystem>()) return;

            var player = shooter.GetComponentInParent<FPSEmeraldPlayerAdapter>();
            if (!IsHostilePlayer(player)) return;
            float range = detection.DetectionRadius;
            if ((player.transform.position - transform.position).sqrMagnitude > range * range ||
                !HasClearSight(player)) return;

            StartPursuit(player.transform);
        }

        private bool IsHostilePlayer(FPSEmeraldPlayerAdapter player) =>
            player && player.isActiveAndEnabled && player.Health > 0 &&
            player.CompareTag(detection.PlayerTag) &&
            detection.GetTargetFactionRelation(player.transform) == "Enemy";

        private bool HasClearSight(FPSEmeraldPlayerAdapter player)
        {
            var playerCollider = player.GetComponent<Collider>();
            if (!playerCollider || !playerCollider.enabled || !detection.HeadTransform) return false;

            var bounds = playerCollider.bounds;
            var target = bounds.center + Vector3.up * (bounds.size.y * 0.35f);
            var origin = detection.HeadTransform.position;
            var direction = target - origin;
            int count = Physics.RaycastNonAlloc(origin, direction.normalized, sightHits, direction.magnitude,
                ~detection.ObstructionDetectionLayerMask.value, QueryTriggerInteraction.Ignore);
            if (count == sightHits.Length) return false;
            Collider first = null;
            float distance = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var hit = sightHits[i];
                if (hit.distance >= distance || hit.collider.transform.IsChildOf(transform)) continue;
                first = hit.collider;
                distance = hit.distance;
            }
            return first && first.GetComponentInParent<FPSEmeraldPlayerAdapter>() == player;
        }

        private void StartPursuit(Transform player)
        {
            IsAlerting = true;
            try
            {
                detection.SetDetectedTarget(player);
            }
            finally
            {
                IsAlerting = false;
            }
        }
    }
}
