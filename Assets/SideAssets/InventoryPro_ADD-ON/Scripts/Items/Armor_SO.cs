using UnityEngine;
using cowsins.Inventory;

namespace cowsins
{
    [System.Serializable]
    [CreateAssetMenu(fileName = "newArmorItem", menuName = "COWSINS/Inventory/New Armor Item", order = 2)]
    public class Armor_SO : Item_SO
    {
        [Tooltip("Damage multiplier applied when taking damage while equipped in a custom slot. E.g. 0.8 means taking 20% less damage.")]
        [SerializeField, Range(0.01f, 1f)] private float damageMultiplier = 0.8f;

        public float DamageMultiplier
        {
            get => damageMultiplier;
            set => damageMultiplier = value;
        }

        public override void ApplyCustomSlotEffects(InventorySlot slot, PlayerDependencies player)
        {
            base.ApplyCustomSlotEffects(slot, player);
            if (player == null || player.PlayerMultipliers == null) return;

            player.PlayerMultipliers.DamageTakenMultiplier.RemoveModifierFromSource(slot);

            // Add the damage taken modifier
            player.PlayerMultipliers.DamageTakenMultiplier.AddModifier(
                new StatModifier(damageMultiplier - 1f, StatModifierType.Additive, slot)
            );
        }

        public override void RemoveCustomSlotEffects(InventorySlot slot, PlayerDependencies player)
        {
            base.RemoveCustomSlotEffects(slot, player);
            if (player == null || player.PlayerMultipliers == null) return;

            player.PlayerMultipliers.DamageTakenMultiplier.RemoveModifierFromSource(slot);
        }
    }
}
