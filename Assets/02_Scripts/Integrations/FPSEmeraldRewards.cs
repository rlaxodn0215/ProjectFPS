using cowsins;
using EmeraldAI;
using UnityEngine;

namespace ProjectFPS.Integrations
{
    [DefaultExecutionOrder(100), DisallowMultipleComponent, RequireComponent(typeof(EmeraldSystem), typeof(EmeraldHealth))]
    public sealed class FPSEmeraldRewards : MonoBehaviour
    {
        [Tooltip("적대 관계 판단과 보상 수령 기준 플레이어입니다.")]
        [SerializeField] private PlayerStats player;
        [SerializeField] private ProgressionManager progression;
        [SerializeField, Min(0)] private int coins = 10;
        [SerializeField, Min(0)] private float experience = 25;
        [SerializeField] private GameObject ammoPrefab;
        [SerializeField] private GameObject healthPrefab;
        [SerializeField, Range(0, 1)] private float ammoChance = 0.3f;
        [SerializeField, Range(0, 1)] private float healthChance = 0.15f;
        [SerializeField, Min(0.1f)] private float dropLifetime = 60;
        private EmeraldSystem system;
        private EmeraldHealth health;
        private readonly FPSLifeCredit credit = new FPSLifeCredit();
        private void Awake() { system = GetComponent<EmeraldSystem>(); health = GetComponent<EmeraldHealth>(); }
        private void OnEnable()
        {
            health.OnDamageResolved += Received;
            health.OnDeath += Died;
            health.OnLifeStarted += ResetLife;
            if (credit.Consumed && health.CurrentHealth > 0) ResetLife();
        }
        private void OnDisable()
        {
            health.OnDamageResolved -= Received;
            health.OnDeath -= Died;
            health.OnLifeStarted -= ResetLife;
        }
        private void Start()
        {
            if (player == null) { Debug.LogError("FPSEmeraldRewards: 기준 플레이어를 지정하세요.", this); enabled = false; return; }
            if (progression == null) progression = player.GetComponent<PlayerDependencies>()?.ProgressionManager;
            if (progression == null) Debug.LogWarning("FPSEmeraldRewards: ProgressionManager가 없어 보상을 지급하지 않습니다.", this);
            if (ammoPrefab == null || healthPrefab == null) Debug.LogWarning("FPSEmeraldRewards: 미지정 프리팹의 결과는 드롭 없음으로 처리합니다.", this);
        }
        private void Received(int amount, Transform attacker) => credit.Record(amount, attacker);
        private void ResetLife() => credit.Reset();
        private void Died()
        {
            if (!credit.Consume()) return;
            if (player == null || system.DetectionComponent.GetTargetFactionRelation(player.transform) != "Enemy" || GetComponent<FPSEmeraldCompanion>() != null) return;
            if (ResolveRecipient(credit.LastAttacker) == player && progression != null)
            {
                if (progression.UseCoins) progression.coinService.AddCoins(coins, true);
                if (progression.UseExperience && progression.ExperienceRequirements != null && progression.ExperienceRequirements.Length > 0)
                    progression.experienceService.AddExperience(experience);
            }
            int selection = FPSCombatRules.SelectDrop(Random.value, ammoChance, healthChance);
            var prefab = selection == 0 ? ammoPrefab : selection == 1 ? healthPrefab : null;
            if (prefab == null) return;
            var drop = Instantiate(prefab, transform.position + Vector3.up * 0.25f, Quaternion.identity);
            Destroy(drop, dropLifetime);
        }
        public static PlayerStats ResolveRecipient(Transform attacker)
        {
            if (attacker == null) return null;
            var direct = attacker.GetComponentInParent<PlayerStats>();
            return direct != null ? direct : attacker.GetComponentInParent<FPSEmeraldCompanion>()?.Owner;
        }
    }
}
