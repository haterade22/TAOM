using TAOM.Core.Validation;

namespace TAOM.Features.FieldCamp;

/// <summary>
/// Production wire-up of <see cref="ICampSettingsProvider"/> over the MCM <c>TaomSettings</c>
/// singleton. Re-validated here: a stale json2 value falls back to the shipped default rather
/// than reaching morale, forage or ambush maths (Config Providers MUST Validate).
/// </summary>
public class CampSettingsProvider : ICampSettingsProvider
{
    // Enabled is read every campaign frame while the camp stands, so the settings object is taken once, on the first non-null read, and read through
    // (BattleBalanceSettingsProvider pattern; CampaignHotPathSettingsProvidersTests, #746).
    private TaomSettings? _settings;
    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    public CampSettingsProvider() { }
    internal CampSettingsProvider(TaomSettings settings) => _settings = settings;

    public const float DefaultSetupHours = 4f;
    public const float DefaultMoralePerHour = 1f;
    public const float DefaultForageFactor = 0.1f;
    public const float DefaultMaxAmbushRange = 10f;
    public const float DefaultBaseAmbushChance = 0.5f;
    public const float DefaultMinTownDistance = 10f;
    public const int DefaultFortifyCost = 500;

    public bool Enabled => Settings?.EnableFieldCamps ?? true;

    public float CampSetupHours =>
        Sane(Settings?.CampSetupHours, 0.5f, 24f, DefaultSetupHours);

    public float CampMoralePerHour =>
        Sane(Settings?.CampMoralePerHour, 0f, 5f, DefaultMoralePerHour);

    public float ForagePerTroopFactor =>
        Sane(Settings?.CampForagePerTroopFactor, 0f, 1f, DefaultForageFactor);

    public float MaxAmbushRange =>
        Sane(Settings?.CampMaxAmbushRange, 1f, 30f, DefaultMaxAmbushRange);

    public float BaseAmbushChance =>
        Sane(Settings?.CampBaseAmbushChance, 0f, 1f, DefaultBaseAmbushChance);

    public float MinTownDistance =>
        Sane(Settings?.CampMinTownDistance, 0f, 50f, DefaultMinTownDistance);

    public int FortifiedUpgradeCost
    {
        get
        {
            var raw = Settings?.CampFortifiedUpgradeCost ?? DefaultFortifyCost;
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
