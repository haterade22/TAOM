using TAOM.Core.Validation;
using TAOM.Features;

namespace TAOM.Features.Refuge;

/// <summary>
/// Production wire-up of <see cref="IRefugeSettingsProvider"/> over the MCM <c>TaomSettings</c>
/// singleton, re-validated so a stale json2 value cannot reach founding, defence or militia maths
/// (Config Providers MUST Validate). The defence bonuses are read on every hit against a refuge's defenders, real-time
/// and auto-resolve, so the settings object is taken once, on the first non-null read, and read through
/// (BattleBalanceSettingsProvider pattern; HotPathSettingsProvidersTests, #745). MCM keeps one object per settings
/// id, so a live MCM edit still applies.
/// </summary>
public class RefugeSettingsProvider : IRefugeSettingsProvider
{
    private TaomSettings? _settings;
    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    public RefugeSettingsProvider() { }
    internal RefugeSettingsProvider(TaomSettings settings) => _settings = settings;

    public bool Enabled => Settings?.EnableRefuges ?? true;

    public int FoundCost => SaneInt(Settings?.RefugeFoundCost, 0, 50000, 2000);

    public int StrongholdUpgradeCost => SaneInt(Settings?.RefugeStrongholdUpgradeCost, 0, 100000, 5000);

    public float BuildHours => Sane(Settings?.RefugeBuildHours, 1f, 48f, 6f);

    public int MaxRefugesCap => SaneInt(Settings?.RefugeMaxCap, 1, 10, 3);

    public float ManageRange => Sane(Settings?.RefugeManageRange, 1f, 15f, 4f);

    public float MinTownDistance => Sane(Settings?.RefugeMinTownDistance, 0f, 60f, 16f);

    public float StrongholdMinTownDistance => Sane(Settings?.RefugeStrongholdMinTownDistance, 0f, 80f, 26f);

    public float RefugeDefenseBonus => Sane(Settings?.RefugeDefenseBonus, 0f, 0.9f, 0.2f);

    public float StrongholdDefenseBonus => Sane(Settings?.RefugeStrongholdDefenseBonus, 0f, 0.9f, 0.35f);

    public int MilitiaBase => SaneInt(Settings?.RefugeMilitiaBase, 0, 50, 6);

    public int MilitiaMax => SaneInt(Settings?.RefugeMilitiaMax, 0, 100, 40);

    public bool EnableRaids => Settings?.RefugeEnableRaids ?? false;

    public float RaidRange => Sane(Settings?.RefugeRaidRange, 1f, 20f, 6f);

    // Source-parity fallback-layout knobs. TaomSettings (single-owner) carries no fields for
    // these yet, so the provider pins the source module's defaults; wiring MCM rows later needs
    // no change on the consuming side.
    public string BuildingMesh => "empire_street_tent_02";

    public float BuildingScale => 0.4f;

    private static float Sane(float? raw, float min, float max, float fallback)
    {
        if (!raw.HasValue)
            return fallback;
        return FiniteFloatValidator.IsFiniteInRange(raw.Value, min, max) ? raw.Value : fallback;
    }

    private static int SaneInt(int? raw, int min, int max, int fallback)
    {
        if (!raw.HasValue)
            return fallback;
        return raw.Value < min || raw.Value > max ? fallback : raw.Value;
    }
}
