namespace cowsins
{
    public interface IPlayerDamageFilter
    {
        float FilterDamage(float amount, DamageContext context);
    }
}
