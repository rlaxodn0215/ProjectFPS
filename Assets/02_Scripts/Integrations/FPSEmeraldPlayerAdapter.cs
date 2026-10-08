using System.Collections.Generic;
using cowsins;
using EmeraldAI;
using EmeraldAI.Utility;
using UnityEngine;
using UnityEngine.Events;

namespace ProjectFPS.Integrations
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerStats), typeof(FactionExtension), typeof(TargetPositionModifier))]
    public sealed class FPSEmeraldPlayerAdapter : MonoBehaviour, EmeraldAI.IDamageable, ICombat
    {
        public bool Immortal;
        public UnityEvent OnTakeDamage = new UnityEvent();
        public UnityEvent OnDeath = new UnityEvent();
        private PlayerStats stats;
        private WeaponController weapons;
        private TargetPositionModifier position;
        private bool deathNotified;

        public Transform LastAttacker { get; private set; }

        public List<string> ActiveEffects { get; set; } = new List<string>();
        private PlayerStats Stats => stats != null ? stats : stats = GetComponent<PlayerStats>();

        public int Health
        {
            get => Stats.IsDead || Stats.health <= 0 ? 0 : Mathf.Max(1, FPSEmeraldAIAdapter.RoundDamage(Stats.health));
            set
            {
                if (Stats.IsDead) return; // Only PlayerStats.Respawn may revive the player.
                Stats.OverrideHealth(Mathf.Clamp(value, 0, Stats.maxHealth), Stats.maxHealth, Stats.shield, Stats.maxShield);
                Stats.Events.OnHealthChanged.Invoke(Stats.health, Stats.shield, false);
                if (Stats.health <= 0) Stats.SetDead();
            }
        }

        public int StartHealth
        {
            get => Mathf.Max(1, FPSEmeraldAIAdapter.RoundDamage(Stats.maxHealth));
            set => Stats.OverrideHealth(Mathf.Min(Stats.health, Mathf.Max(1, value)), Mathf.Max(1, value), Stats.shield, Stats.maxShield);
        }

        private void Awake()
        {
            stats = GetComponent<PlayerStats>();
            weapons = GetComponent<WeaponController>();
            position = GetComponent<TargetPositionModifier>();
        }

        private void OnEnable()
        {
            Stats.AddOnDieListener(NotifyDeath);
            Stats.Events.OnHealthChanged.AddListener(HealthChanged);
            deathNotified = Stats.IsDead;
        }

        private void OnDisable()
        {
            if (stats == null) return;
            stats.RemoveOnDieListener(NotifyDeath);
            stats.Events.OnHealthChanged.RemoveListener(HealthChanged);
        }

        private void HealthChanged(float health, float shield, bool damaged)
        {
            if (!Stats.IsDead && health > 0 && deathNotified)
            {
                deathNotified = false;
                ActiveEffects.Clear();
                LastAttacker = null;
            }
        }

        private void NotifyDeath()
        {
            if (deathNotified) return;
            deathNotified = true;
            OnDeath.Invoke();
        }

        public void Damage(int DamageAmount, Transform AttackerTransform = null, int RagdollForce = 100, bool CriticalHit = false)
        {
            if (Immortal || Stats.IsDead || DamageAmount <= 0) return;
            float before = Mathf.Max(0, Stats.health) + Mathf.Max(0, Stats.shield);
            var previousAttacker = LastAttacker;
            LastAttacker = AttackerTransform;
            Stats.Damage(DamageAmount, CriticalHit);
            float received = before - (Mathf.Max(0, Stats.health) + Mathf.Max(0, Stats.shield));
            if (received <= 0)
            {
                LastAttacker = previousAttacker;
                return; // Includes the existing dash damage protection.
            }
            OnTakeDamage.Invoke();
            if (CombatTextSystem.Instance != null)
                CombatTextSystem.Instance.CreateCombatText(FPSEmeraldAIAdapter.RoundDamage(received), DamagePosition(), CriticalHit, false, false);
        }

        public Transform TargetTransform() => transform;
        public Vector3 DamagePosition() => position != null && position.TransformSource != null
            ? position.TransformSource.position + Vector3.up * position.PositionModifier
            : transform.position + Vector3.up;
        public bool IsAttacking() => weapons != null && !Stats.IsDead && (weapons.IsShooting || !weapons.IsMeleeAvailable);
        public bool IsBlocking() => false;
        public bool IsDodging() => false;
        public void TriggerStun(float StunLength) { }
    }
}
