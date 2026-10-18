using UnityEngine;

public enum DamageType
{
    Physical,
    Spell
}

public interface ITypedDamageable
{
    void TakeDamage(float amount, Vector2 hitPoint, Vector2 hitDirection, DamageType damageType);
}

public static class DamageApplier
{
    public static void Apply(IDamageable target, float amount, Vector2 hitPoint, Vector2 hitDirection, DamageType damageType)
    {
        if (target == null)
        {
            return;
        }

        if (target is ITypedDamageable typed)
        {
            typed.TakeDamage(amount, hitPoint, hitDirection, damageType);
            return;
        }

        target.TakeDamage(amount, hitPoint, hitDirection);
    }
}
