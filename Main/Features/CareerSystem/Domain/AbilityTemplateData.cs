namespace TAOM.Features.CareerSystem.Domain;

public sealed class AbilityTemplateData
{
    public string Id { get; set; }
    public string DisplayName { get; set; }
    public float Duration { get; set; }
    public float Radius { get; set; }
    public float MaxCharge { get; set; }
    public string ParticleEffect { get; set; }
    public string SoundEffect { get; set; }
    public string TooltipDescription { get; set; }

    // Issue #104 Option B — per-activation cooldown reduction in seconds. The 98 dead MaxCharge
    // mutations in taom_career_choices.xml were repurposed to target this property after the
    // cooldown rework (#103) made MaxCharge unread for CooldownOnly abilities. Calculator
    // convention: <Mutation property="CooldownReduction" calculator="flat" value="6"/> for a
    // 6-second reduction — POSITIVE values. The `flat` calculator is `baseValue + value`
    // (BuiltInCalculators.cs:7-8), so positive XML produces a positive CooldownReduction;
    // AbilityEffectExecutor guards on `> 0f` before calling ApplyCooldownAdjustment. Codex
    // pass-2 (2026-06-02) caught a HIGH where the first XML rewrite emitted negative values
    // and the entire feature was a no-op. Effective cooldown is floored at MinCooldownSeconds.
    public float CooldownReduction { get; set; }

    public AbilityTemplateData() { }

    public AbilityTemplateData(AbilityTemplateData source)
    {
        Id = source.Id;
        DisplayName = source.DisplayName;
        Duration = source.Duration;
        Radius = source.Radius;
        MaxCharge = source.MaxCharge;
        ParticleEffect = source.ParticleEffect;
        SoundEffect = source.SoundEffect;
        TooltipDescription = source.TooltipDescription;
        CooldownReduction = source.CooldownReduction;
    }
}
