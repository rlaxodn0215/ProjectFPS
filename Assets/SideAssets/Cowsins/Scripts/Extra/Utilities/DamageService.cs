namespace cowsins
{
    /// <summary>
    /// Static service locator for damage routing.
    /// </summary>
    public static class DamageService
    {
        private static IDamageRouter router;
        private static int requestDepth;

        private static bool pendingEnvironmental;
        public static bool EnvironmentalContext
        {
            get => pendingEnvironmental || CurrentContext.Kind == DamageKind.Environmental;
            set => pendingEnvironmental = value;
        }
        public static DamageContext CurrentContext { get; private set; }

        public static IDamageRouter Router
        {
            get => router ?? DefaultDamageRouter.Instance;
            set => router = value;
        }

        public static void RequestDamage(IDamageable target, float damage, bool isHeadshot)
        {
            var context = pendingEnvironmental
                ? new DamageContext(null, null, DamageKind.Environmental) : default;
            pendingEnvironmental = false;
            RequestDamage(target, damage, isHeadshot, context);
        }

        public static void RequestDamage(IDamageable target, float damage, bool isHeadshot, DamageContext context)
        {
            var previous = CurrentContext;
            bool previousEnvironmental = pendingEnvironmental;
            requestDepth++;
            CurrentContext = context;
            pendingEnvironmental = false;
            try { Router.RequestDamage(target, damage, isHeadshot); }
            finally
            {
                requestDepth--;
                CurrentContext = previous;
                pendingEnvironmental = requestDepth > 0 && previousEnvironmental;
            }
        }

        public static void Reset()
        {
            router = null;
            if (requestDepth == 0)
            {
                CurrentContext = default;
                pendingEnvironmental = false;
            }
        }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad() => Reset();

        private sealed class DefaultDamageRouter : IDamageRouter
        {
            public static readonly DefaultDamageRouter Instance = new DefaultDamageRouter();
            public void RequestDamage(IDamageable target, float damage, bool isHeadshot)
                => target?.Damage(damage, isHeadshot);
        }
    }
}
