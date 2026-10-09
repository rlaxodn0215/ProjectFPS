using cowsins;
using UnityEngine;

namespace ProjectFPS.Integrations
{
    public static class FPSCombatRules
    {
        public static bool CanHear(float distance, float radius, bool obstructed, float obstructionMultiplier)
            => radius > 0 && distance <= radius * (obstructed ? Mathf.Clamp01(obstructionMultiplier) : 1f);

        public static float BlockDamage(float amount, bool blocking, Vector3 forward, Vector3 incoming,
            DamageKind kind, float halfAngle, float reduction)
        {
            if (!blocking || kind == DamageKind.Unknown || kind == DamageKind.Explosion || kind == DamageKind.Environmental) return amount;
            incoming.y = 0; forward.y = 0;
            if (incoming.sqrMagnitude < 0.0001f || forward.sqrMagnitude < 0.0001f) return amount;
            return Vector3.Angle(forward, incoming) <= halfAngle ? amount * (1f - Mathf.Clamp01(reduction)) : amount;
        }

        public static int SelectDrop(float roll, float ammoChance, float healthChance)
        {
            float ammo = Mathf.Clamp01(ammoChance);
            float health = Mathf.Clamp(healthChance, 0, 1 - ammo);
            return roll < ammo ? 0 : roll < ammo + health ? 1 : -1;
        }
    }

    public sealed class FPSLifeCredit
    {
        public Transform LastAttacker { get; private set; }
        public bool Consumed { get; private set; }
        public void Record(int actualDamage, Transform attacker)
        {
            if (actualDamage > 0 || actualDamage == 0 && attacker == null) LastAttacker = attacker;
        }
        public bool Consume()
        {
            if (Consumed) return false;
            Consumed = true; return true;
        }
        public void Reset() { LastAttacker = null; Consumed = false; }
    }
}
