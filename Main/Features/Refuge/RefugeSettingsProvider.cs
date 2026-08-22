using TAOM.Core.Validation;
using TAOM.Features;

namespace TAOM.Features.Refuge;

/// <summary>
/// Production wire-up of <see cref="IRefugeSettingsProvider"/> over the MCM <c>TaomSettings</c>
/// singleton, re-validated so a stale json2 value cannot reach founding, defence or militia maths
/// (Config Providers MUST Validate).
/// </summary>
public class RefugeSettingsProvider : IRefugeSettingsProvider
{
    public bool Enabled => TaomSettings.Instance?.EnableRefuges ?? true;

    public int FoundCost => SaneInt(TaomSettings.Instance?.RefugeFoundCost, 0, 50000, 2000);

    public int StrongholdUpgradeCost => SaneInt(TaomSettings.Instance?.RefugeStrongholdUpgradeCost, 0, 100000, 5000);

    public float BuildHours => Sane(TaomSettings.Instance?.RefugeBuildHours, 1f, 48f, 6f);

    public int MaxRefugesCap => SaneInt(TaomSettings.Instance?.RefugeMaxCap, 1, 10, 3);

    public float ManageRange => Sane(TaomSettings.Instance?.RefugeManageRange, 1f, 15f, 4f);

    public float MinTownDistance => Sane(TaomSettings.Instance?.RefugeMinTownDistance, 0f, 60f, 16f);

    public float StrongholdMinTownDistance => Sane(TaomSettings.Instance?.RefugeStrongholdMinTownDistance, 0f, 80f, 26f);

    public float RefugeDefenseBonus => Sane(TaomSettings.Instance?.RefugeDefenseBonus, 0f, 0.9f, 0.2f);

    public float StrongholdDefenseBonus => Sane(TaomSettings.Instance?.RefugeStrongholdDefenseBonus, 0f, 0.9f, 0.35f);

    public int MilitiaBase => SaneInt(TaomSettings.Instance?.RefugeMilitiaBase, 0, 50, 6);

    public int MilitiaMax => SaneInt(TaomSettings.Instance?.RefugeMilitiaMax, 0, 100, 40);

    public bool EnableRaids => TaomSettings.Instance?.RefugeEnableRaids ?? false;

    public float RaidRange => Sane(TaomSettings.Instance?.RefugeRaidRange, 1f, 20f, 6f);

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
