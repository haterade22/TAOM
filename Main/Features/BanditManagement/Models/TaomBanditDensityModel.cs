using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Library;

namespace TAOM.Features.BanditManagement.Models;

/// <summary>
/// Overrides vanilla <see cref="DefaultBanditDensityModel"/> to scale hideout density +
/// first-fight troop counts by PlayerProgress * MCM curves. Vanilla is the floor for those:
/// bandit scaling never reduces them below vanilla, only amplifies them as the campaign
/// progresses.
///
/// The one exception is <see cref="NumberOfMaximumTroopCountForBossFightInHideout"/>, which is
/// <c>1 + Hideout Boss Bodyguards</c> regardless of the toggle (#564). Its only engine consumer is
/// <c>HideoutCampaignBehavior.ArrangeHideoutTroopCountsForMission</c>, which trims the hideout to
/// <c>FirstFightMax + this</c> before the mission opens; the boss fight itself is sized by the
/// Patch86 prefixes from the same <see cref="IHideoutBossFightService"/>, so the value here keeps
/// phase 1 at its pre-#564 size rather than deciding the fight.
///
/// Per gamemodels.md rule: this class is a thin entry — every property delegates to the
/// service or returns a single expression. No <c>if</c>/<c>foreach</c>/branching here.
/// </summary>
public class TaomBanditDensityModel : DefaultBanditDensityModel
{
    private readonly IBanditScalingService _scaling;
    private readonly IHideoutBossFightService _bossFight;

    public TaomBanditDensityModel(IBanditScalingService scaling, IHideoutBossFightService bossFight)
    {
        _scaling = scaling;
        _bossFight = bossFight;
    }

    public override int NumberOfMinimumBanditPartiesInAHideoutToInfestIt =>
        _scaling.IsEnabled
            ? _scaling.MinPartiesToInfest
            : base.NumberOfMinimumBanditPartiesInAHideoutToInfestIt;

    public override int NumberOfMaximumHideoutsAtEachBanditFaction =>
        _scaling.IsEnabled
            ? Cap(base.NumberOfMaximumHideoutsAtEachBanditFaction, _scaling.GetDensityMultiplier(GetPlayerProgress()), _scaling.MaxHideoutsPerFactionCap)
            : base.NumberOfMaximumHideoutsAtEachBanditFaction;

    public override int NumberOfInitialHideoutsAtEachBanditFaction =>
        _scaling.IsEnabled
            ? _scaling.InitialHideoutsPerFaction
            : base.NumberOfInitialHideoutsAtEachBanditFaction;

    public override int NumberOfMaximumBanditPartiesInEachHideout =>
        _scaling.IsEnabled
            ? Cap(base.NumberOfMaximumBanditPartiesInEachHideout, _scaling.GetDensityMultiplier(GetPlayerProgress()), _scaling.MaxPartiesPerHideoutCap)
            : base.NumberOfMaximumBanditPartiesInEachHideout;

    public override int NumberOfMaximumTroopCountForFirstFightInHideout =>
        _scaling.IsEnabled
            ? Scale(base.NumberOfMaximumTroopCountForFirstFightInHideout, _scaling.GetBossFightMultiplier(GetPlayerProgress()))
            : base.NumberOfMaximumTroopCountForFirstFightInHideout;

    public override int NumberOfMaximumTroopCountForBossFightInHideout => _bossFight.BossPhaseTroopCap;

    // Helpers stay branch-free; per gamemodels.md, the property bodies above hold the ternary
    // which is allowed (it's a single conditional expression, not a multi-line block).
    // internal (not private) for direct unit testing via InternalsVisibleTo("TAOM.Tests") —
    // these hold the only computation in the otherwise-thin model.
    //
    // The effective ceiling is max(baseValue, hardCap): because the multiplier is always >= 1.0,
    // a user-set MCM cap BELOW the vanilla base must never push density under vanilla. That
    // preserves the "vanilla is always the floor" invariant documented on this class even when a
    // cap slider is dragged below the vanilla value (e.g. parties-per-hideout cap 3 -> 2).
    internal static int Cap(int baseValue, float multiplier, int hardCap)
    {
        var scaled = (int)MathF.Round(baseValue * multiplier);
        var ceiling = hardCap < baseValue ? baseValue : hardCap;
        return scaled < baseValue ? baseValue : scaled > ceiling ? ceiling : scaled;
    }

    internal static int Scale(int baseValue, float multiplier)
    {
        var scaled = (int)MathF.Round(baseValue * multiplier);
        return scaled < baseValue ? baseValue : scaled;
    }

    private static float GetPlayerProgress() => Campaign.Current?.PlayerProgress ?? 0f;
}
