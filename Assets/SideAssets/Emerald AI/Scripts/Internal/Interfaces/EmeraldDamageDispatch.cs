using UnityEngine;

namespace EmeraldAI
{
    public enum EmeraldAttackKind { Unknown, Melee, Projectile, Explosion, Environmental }

    public interface IContextualDamageable
    {
        void Damage(int amount, Transform attacker, int force, bool critical, EmeraldAttackKind kind);
    }

    // Optional contract. Targets implementing only IDamageable retain the original behavior.
    public static class EmeraldDamageDispatch
    {
        public static void Damage(IDamageable target, int amount, Transform attacker, int force, bool critical, EmeraldAttackKind kind)
        {
            if (target is IContextualDamageable contextual) contextual.Damage(amount, attacker, force, critical, kind);
            else target?.Damage(amount, attacker, force, critical);
        }
    }
}
