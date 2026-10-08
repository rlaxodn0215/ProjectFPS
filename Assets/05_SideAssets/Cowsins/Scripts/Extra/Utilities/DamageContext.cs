using UnityEngine;

namespace cowsins
{
    public enum DamageKind { Unknown, Hitscan, Projectile, Explosion, Melee, QuickMelee, Environmental }

    /// <summary>Metadata for one synchronous damage request. Damage is already multiplied by the weapon.</summary>
    public readonly struct DamageContext
    {
        public Transform Attacker { get; }
        public Collider HitCollider { get; }
        public DamageKind Kind { get; }

        public DamageContext(Transform attacker, Collider hitCollider, DamageKind kind)
        {
            Attacker = attacker;
            HitCollider = hitCollider;
            Kind = kind;
        }
    }
}
