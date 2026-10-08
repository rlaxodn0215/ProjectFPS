using System.Collections.Generic;
using UnityEngine;

namespace cowsins
{
    /// <summary>
    /// Static service locator for damage routing.
    /// </summary>
    public static class DamageService
    {
        private static IDamageRouter router;
        private static int requestDepth;

        public static DamageContext CurrentContext { get; private set; }

        public static bool EnvironmentalContext { get; set; }

        public static IDamageRouter Router
        {
            get => router ?? DefaultDamageRouter.Instance;
            set => router = value;
        }

        public static void RequestDamage(IDamageable target, float damage, bool isHeadshot)
        {
            RequestDamage(target, damage, isHeadshot,
                new DamageContext(null, null, EnvironmentalContext ? DamageKind.Environmental : DamageKind.Unknown));
        }

        public static void RequestDamage(IDamageable target, float damage, bool isHeadshot, DamageContext context)
        {
            var previousContext = CurrentContext;
            bool previousEnvironmental = EnvironmentalContext;
            requestDepth++;
            CurrentContext = context;
            EnvironmentalContext = context.Kind == DamageKind.Environmental;
            try { Router.RequestDamage(target, damage, isHeadshot); }
            finally
            {
                requestDepth--;
                CurrentContext = previousContext;
                EnvironmentalContext = requestDepth > 0 && previousEnvironmental;
            }
        }

        /// <summary>Resolve child hitboxes to their owner, keeping the closest collider per character.</summary>
        public static Dictionary<IDamageable, Collider> GatherExplosionTargets(IEnumerable<Collider> colliders, Vector3 origin)
        {
            var targets = new Dictionary<IDamageable, Collider>();
            foreach (var collider in colliders)
            {
                if (collider == null) continue;
                var target = CowsinsUtilities.GatherDamageableParent(collider.transform);
                if (target == null || target.IsDead) continue;
                if (!targets.TryGetValue(target, out var previous) ||
                    (collider.ClosestPoint(origin) - origin).sqrMagnitude < (previous.ClosestPoint(origin) - origin).sqrMagnitude)
                    targets[target] = collider;
            }
            return targets;
        }

        public static void Reset()
        {
            router = null;
            if (requestDepth == 0)
            {
                CurrentContext = default;
                EnvironmentalContext = false;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad() => Reset();

        private sealed class DefaultDamageRouter : IDamageRouter
        {
            public static readonly DefaultDamageRouter Instance = new DefaultDamageRouter();
            public void RequestDamage(IDamageable target, float damage, bool isHeadshot)
                => target?.Damage(damage, isHeadshot);
        }
    }
}
