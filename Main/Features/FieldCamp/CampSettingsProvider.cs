using TAOM.Core.Validation;

namespace TAOM.Features.FieldCamp;

/// <summary>
/// Production wire-up of <see cref="ICampSettingsProvider"/> over the MCM <c>TaomSettings</c>
/// singleton. Re-validated here: a stale json2 value falls back to the shipped default rather
/// than reaching morale, forage or ambush maths (Config Providers MUST Validate).
/// </summary>
public class CampSettingsProvider : ICampSettingsProvider
{
    public const float DefaultSetupHours = 4f;
    public const float DefaultMoralePerHour = 1f;
    public const float DefaultForageFactor = 0.1f;
    public const float DefaultMaxAmbushRange = 10f;
    public const float DefaultBaseAmbushChance = 0.5f;
    public const float DefaultMinTownDistance = 10f;
    public const int DefaultFortifyCost = 500;

    public bool Enabled => TaomSettings.Instance?.EnableFieldCamps ?? true;

    public float CampSetupHours =>
        Sane(TaomSettings.Instance?.CampSetupHours, 0.5f, 24f, DefaultSetupHours);

    public float CampMoralePerHour =>
        Sane(TaomSettings.Instance?.CampMoralePerHour, 0f, 5f, DefaultMoralePerHour);

    public float ForagePerTroopFactor =>
        Sane(TaomSettings.Instance?.CampForagePerTroopFactor, 0f, 1f, DefaultForageFactor);

    public float MaxAmbushRange =>
        Sane(TaomSettings.Instance?.CampMaxAmbushRange, 1f, 30f, DefaultMaxAmbushRange);

    public float BaseAmbushChance =>
        Sane(TaomSettings.Instance?.CampBaseAmbushChance, 0f, 1f, DefaultBaseAmbushChance);

    public float MinTownDistance =>
        Sane(TaomSettings.Instance?.CampMinTownDistance, 0f, 50f, DefaultMinTownDistance);

    public int FortifiedUpgradeCost
    {
        get
        {
            var raw = TaomSettings.Instance?.CampFortifiedUpgradeCost ?? DefaultFortifyCost;
            return raw < 0 || raw > 10000 ? DefaultFortifyCost : raw;
        }
    }

    private static float Sane(float? raw, float min, float max, float fallback)
    {
        if (!raw.HasValue)
            return fallback;
        return FiniteFloatValidator.IsFiniteInRange(raw.Value, min, max) ? raw.Value : fallback;
    }
}
