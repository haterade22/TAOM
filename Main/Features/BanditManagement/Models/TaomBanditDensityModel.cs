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

    // Helpers stay private + branch-free; per gamemodels.md, the property bodies above hold the
    // ternary which is allowed (it's a single conditional expression, not a multi-line block).
    private static int Cap(int baseValue, float multiplier, int hardCap)
    {
        var scaled = (int)MathF.Round(baseValue * multiplier);
        return scaled < baseValue ? baseValue : scaled > hardCap ? hardCap : scaled;
    }

    private static int Scale(int baseValue, float multiplier)
    {
        var scaled = (int)MathF.Round(baseValue * multiplier);
        return scaled < baseValue ? baseValue : scaled;
    }

    private static float GetPlayerProgress() => Campaign.Current?.PlayerProgress ?? 0f;
}
