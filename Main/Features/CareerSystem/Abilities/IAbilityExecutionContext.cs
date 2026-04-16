namespace TAOM.Features.CareerSystem.Abilities;

public interface IAbilityExecutionContext
{
    string HeroStringId { get; }
    float Duration { get; }
    float Radius { get; }

    void ApplySpeedBuff(float multiplier, float duration);
    void ApplyDamageBuff(float multiplier, float duration);
    void ApplyResistanceBuff(float multiplier, float duration);
    void ApplyMoraleBurst(float radius, float magnitude);
    void ApplyStealthMode(float duration);
    void ApplyAllyBuff(float damageBonusFlat, float damageReductionFlat, float radius, float duration);
    void ApplyDrawSpeedBuff(float bonus, float duration);
    void ApplyMountSpeedBuff(float bonus, float duration);
    void ApplyChargeDamageBuff(float bonus, float duration);
    void PlaySound(string soundId);
    void PlayParticle(string particleId);
}
