using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Library;

namespace TAOM.Features.BanditManagement.Models;

/// <summary>
/// Overrides vanilla <see cref="DefaultBanditDensityModel"/> to scale hideout density +
/// boss-fight troop counts by PlayerProgress * MCM curves. Vanilla is the floor — bandit
/// scaling never reduces difficulty below vanilla, only amplifies it as the campaign
/// progresses.
///
/// Per gamemodels.md rule: this class is a thin entry — every property delegates to the
/// service or returns a single expression. No <c>if</c>/<c>foreach</c>/branching here.
/// </summary>
public class TaomBanditDensityModel : DefaultBanditDensityModel
{
    private readonly IBanditScalingService _scaling;

    public TaomBanditDensityModel(IBanditScalingService scaling)
    {
        _scaling = scaling;
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

    public override int NumberOfMaximumTroopCountForBossFightInHideout =>
        _scaling.IsEnabled
            ? Scale(base.NumberOfMaximumTroopCountForBossFightInHideout, _scaling.GetBossFightMultiplier(GetPlayerProgress()))
            : base.NumberOfMaximumTroopCountForBossFightInHideout;

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
