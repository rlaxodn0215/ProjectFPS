namespace cowsins.Inventory
{
    /// <summary>
    /// Defines how item durability is reduced.
    /// </summary>
    public enum DurabilityReductionMethod
    {
        Usage,        // Reduced on shooting / using the item
        TakingDamage  // Reduced when the player takes damage
    }
}
