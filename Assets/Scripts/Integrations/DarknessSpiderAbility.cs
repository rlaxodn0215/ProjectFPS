using EmeraldAI;
using UnityEngine;

namespace ProjectFPS
{
    public sealed class DarknessSpiderAbility : EmeraldAbilityObject
    {
        public int Damage;
        public DarknessSpiderWeb Web;

        public override void InvokeAbility(GameObject owner, Transform attackTransform = null)
        {
            if (owner.TryGetComponent<FPSEmeraldHardSpider>(out var spider))
                spider.ResolveAttack(this);
        }
    }
}
