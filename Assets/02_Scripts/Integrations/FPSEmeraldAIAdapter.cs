using cowsins;
using EmeraldAI;
using UnityEngine;

namespace ProjectFPS.Integrations
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EmeraldSystem))]
    public sealed class FPSEmeraldAIAdapter : MonoBehaviour, cowsins.IDamageable
    {
        [SerializeField, Min(0)] private int ragdollForce = 40;
        private EmeraldSystem system;
        private EmeraldHealth health;

        public float Health => HealthComponent != null ? HealthComponent.CurrentHealth : 0;
        public float Shield => 0;
        public bool IsDead => Health <= 0 || (SystemComponent.AnimationComponent != null && SystemComponent.AnimationComponent.IsDead);
        private EmeraldHealth HealthComponent => health != null ? health : health = GetComponent<EmeraldHealth>();
        private EmeraldSystem SystemComponent => system != null ? system : system = GetComponent<EmeraldSystem>();

        private void Awake()
        {
            health = GetComponent<EmeraldHealth>();
            system = GetComponent<EmeraldSystem>();
        }

        public static int RoundDamage(float amount)
        {
            if (float.IsNaN(amount) || float.IsInfinity(amount) || amount <= 0) return 0;
            return (int)System.Math.Min(int.MaxValue, System.Math.Floor((double)amount + 0.5d));
        }

        public void Damage(float damage, bool isHeadshot)
        {
            int amount = RoundDamage(damage);
            if (amount == 0 || IsDead) return;

            var context = DamageService.CurrentContext;
            if (context.Attacker != null && (context.Attacker == SystemComponent.TargetToFollow ||
                SystemComponent.DetectionComponent.GetTargetFactionRelation(context.Attacker) == "Friendly")) return;
            var collider = context.HitCollider;
            // A shared scene parent is not a character identity: only accept this AI's own colliders.
            bool ownCollider = collider != null && collider.GetComponentInParent<FPSEmeraldAIAdapter>() == this;
            var area = ownCollider ? collider.GetComponent<LocationBasedDamageArea>() : null;
            var combat = SystemComponent.CombatComponent;
            if (ownCollider && combat != null) combat.RagdollTransform = collider.transform;

            // Do not call DamageArea: FPS Engine has already applied the weapon's headshot multiplier.
            HealthComponent.Damage(amount, context.Attacker, ragdollForce, isHeadshot);
            if (area != null && !SystemComponent.AnimationComponent.IsBlocking && !SystemComponent.AnimationComponent.IsDodging)
                area.CreateImpactEffect(collider.ClosestPoint(transform.position), HealthComponent.AttachHitEffects);
        }
    }
}
