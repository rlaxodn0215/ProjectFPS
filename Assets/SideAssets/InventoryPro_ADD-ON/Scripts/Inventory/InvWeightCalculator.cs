using UnityEngine;

namespace cowsins.Inventory
{
    /// <summary>
    /// Pure calculator/state holder for inventory weight.
    /// Updates the player's movement multiplier through <see cref="PlayerMultipliers"/>.
    /// </summary>
    public sealed class InvWeightCalculator
    {
        private readonly PlayerMultipliers playerMultipliers;
        private int totalSlots;
        private readonly float minWeightMultiplier;

        private float currentWeight;

        public float CurrentWeight => currentWeight;

        public InvWeightCalculator(PlayerMultipliers multipliers, int rows, int cols, float minWeightMultiplier = 0.2f)
        {
            playerMultipliers = multipliers;
            totalSlots = Mathf.Max(1, rows * cols);
            this.minWeightMultiplier = minWeightMultiplier;
            ApplyMultiplier();
        }

        public void UpdateDimensions(int rows, int cols)
        {
            int oldTotal = totalSlots;
            totalSlots = Mathf.Max(1, rows * cols);
            if (oldTotal > 0 && totalSlots > 0 && currentWeight > 0)
            {
                currentWeight = currentWeight * ((float)oldTotal / totalSlots);
                ApplyMultiplier();
            }
        }

        public void ResetWeight()
        {
            currentWeight = 0f;
            ApplyMultiplier();
        }

        /// <summary>
        /// Applies a delta of items to the carried weight. Positive adds weight, negative removes weight.
        /// </summary>
        public void UpdateCurrentWeight(Item_SO item, int amountDelta, float weightDampeningMultiplier = 1f)
        {
            if (item == null || amountDelta == 0) return;

            float weight = item.applyWeight ? item.weightMultiplier : 1f;

            // How many full stacks this delta represents (keeps original behaviour).
            float fullStacks = amountDelta / (float)item.maxStack;

            // Total weight contribution by this operation (keeps original behaviour).
            float weightContribution = (1f - weight) * fullStacks * weightDampeningMultiplier;
            weightContribution /= totalSlots;

            currentWeight += weightContribution;

            ApplyMultiplier();
        }

        private void ApplyMultiplier()
        {
            if (playerMultipliers == null) return;

            playerMultipliers.WeightMultiplier.BaseValue = Mathf.Clamp(1f - currentWeight, minWeightMultiplier, 1.0f);
        }
    }
}

