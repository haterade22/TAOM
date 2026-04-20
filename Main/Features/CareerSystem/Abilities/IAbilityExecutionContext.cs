namespace TAOM.Features.CareerSystem.Abilities;

public interface IAbilityExecutionContext
{
    string HeroStringId { get; }
    float Duration { get; }
    float Radius { get; }

    void ApplyMoraleBurst(float radius, float magnitude);
    void ApplyStealthMode(float duration);
    void ApplyAllyBuff(float damageBonusFlat, float damageReductionFlat, float radius, float duration);
    void ApplyAllyRangedBuff(float speedBonus, float damageBonus, float drawSpeedBonus, float radius, float duration);
    void ApplyAllyCavalryBuff(float mountSpeedBonus, float chargeDamageBonus, float damageBonus, float radius, float duration);
    void PlaySound(string soundId);
    void PlayParticle(string particleId);
}
