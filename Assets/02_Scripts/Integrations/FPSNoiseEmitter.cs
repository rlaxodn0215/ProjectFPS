using System;
using System.Collections.Generic;
using cowsins;
using UnityEngine;

namespace ProjectFPS.Integrations
{
    public enum FPSNoiseKind { Gunshot, Footstep }
    public readonly struct FPSNoise
    {
        public readonly Transform Source;
        public readonly FPSNoiseKind Kind;
        public readonly Vector3 Position;
        public readonly float Radius;
        public readonly Weapon_SO Weapon;
        public FPSNoise(Transform source, FPSNoiseKind kind, Vector3 position, float radius, Weapon_SO weapon = null)
        { Source = source; Kind = kind; Position = position; Radius = radius; Weapon = weapon; }
    }

    [DisallowMultipleComponent, RequireComponent(typeof(PlayerMovement), typeof(WeaponController), typeof(PlayerStats))]
    public sealed class FPSNoiseEmitter : MonoBehaviour
    {
        [Serializable]
        public sealed class WeaponNoise
        {
            public Weapon_SO weapon;
            public bool suppressed;
            [Min(0)] public float normalRadius = 40;
            [Min(0)] public float suppressedRadius = 12;
        }
        public static event Action<FPSNoise> Emitted;
        [SerializeField] private List<WeaponNoise> weapons = new List<WeaponNoise>();
        [SerializeField, Min(0)] private float gunshotRadius = 40;
        [SerializeField, Min(0)] private float walkRadius = 8;
        [SerializeField, Min(0)] private float runRadius = 16;
        [SerializeField, Min(0)] private float crouchRadius = 3;
        private PlayerMovement movement;
        private WeaponController controller;
        private PlayerStats stats;
        private void Awake()
        {
            movement = GetComponent<PlayerMovement>();
            controller = GetComponent<WeaponController>();
            stats = GetComponent<PlayerStats>();
        }
        private void OnEnable()
        {
            controller.Events.OnWeaponFired.AddListener(Fired);
            FootstepsBehaviour.OnFootstepPlayed += Footstep;
        }
        private void OnDisable()
        {
            controller.Events.OnWeaponFired.RemoveListener(Fired);
            FootstepsBehaviour.OnFootstepPlayed -= Footstep;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetEvents() { Emitted = null; }
        private void Fired(Weapon_SO weapon)
        {
            if (stats.IsDead || weapon == null || (int)weapon.shootStyle > 1) return;
            var entry = weapons.Find(x => x != null && x.weapon == weapon);
            float radius = entry == null ? gunshotRadius : entry.suppressed ? entry.suppressedRadius : entry.normalRadius;
            Emitted?.Invoke(new FPSNoise(transform, FPSNoiseKind.Gunshot, transform.position, radius, weapon));
        }
        private void Footstep(Transform source, int layer, int clip)
        {
            if (source != transform || stats.IsDead) return;
            float radius = movement.IsCrouching ? crouchRadius : movement.CurrentSpeed > movement.WalkSpeed + 0.1f ? runRadius : walkRadius;
            Emitted?.Invoke(new FPSNoise(transform, FPSNoiseKind.Footstep, transform.position, radius));
        }
    }
}
