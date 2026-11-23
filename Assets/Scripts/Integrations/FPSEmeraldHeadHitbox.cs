using EmeraldAI;
using UnityEngine;

namespace ProjectFPS
{
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EmeraldHealth))]
    public sealed class FPSEmeraldHeadHitbox : MonoBehaviour
    {
        [SerializeField] private Collider headCollider;

        private EmeraldHealth health;

        private void Awake() => health = GetComponent<EmeraldHealth>();

        private void OnEnable()
        {
            health.OnLifeStarted += EnableHitbox;
            health.OnDeath += DisableHitbox;
            SyncHitbox();
        }

        // Emerald's detection Start disables child colliders as part of ragdoll initialization.
        private void Start() => SyncHitbox();

        private void OnDisable()
        {
            health.OnLifeStarted -= EnableHitbox;
            health.OnDeath -= DisableHitbox;
            DisableHitbox();
        }

        private void SyncHitbox() => SetHitboxEnabled(health.CurrentHealth > 0);
        private void EnableHitbox() => SetHitboxEnabled(true);
        private void DisableHitbox() => SetHitboxEnabled(false);

        private void SetHitboxEnabled(bool hitboxEnabled)
        {
            if (headCollider != null) headCollider.enabled = hitboxEnabled;
        }
    }
}
