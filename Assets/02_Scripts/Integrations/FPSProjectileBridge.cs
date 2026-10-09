using cowsins;
using EmeraldAI;
using UnityEngine;

namespace ProjectFPS.Integrations
{
    [DisallowMultipleComponent, RequireComponent(typeof(WeaponController))]
    public sealed class FPSProjectileBridge : MonoBehaviour
    {
        private WeaponController controller;
        private void Awake() => controller = GetComponent<WeaponController>();
        private void OnEnable() => controller.Events.OnProjectileCreated.AddListener(ConnectProjectile);
        private void OnDisable() => controller.Events.OnProjectileCreated.RemoveListener(ConnectProjectile);
        private void ConnectProjectile(GameObject projectile, Transform hitTarget)
        {
            if (projectile == null) return;
            var ai = hitTarget != null ? hitTarget.GetComponentInParent<EmeraldSystem>() : null;
            var bridge = projectile.GetComponent<FPSAvoidableProjectile>() ?? projectile.AddComponent<FPSAvoidableProjectile>();
            bridge.Initialize(ai != null ? ai.transform : null);
        }
    }
}
