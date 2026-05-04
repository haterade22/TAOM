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
    }
}
