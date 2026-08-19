using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TAOM.Features.RevoltTuning;

namespace TAOM.Features.CulturalFeats.Models;

public class TaomSettlementLoyaltyModel : DefaultSettlementLoyaltyModel
{
    private readonly RevoltTuningConfig _revoltConfig;
    private readonly ICulturalFeatsService _feats;

    public TaomSettlementLoyaltyModel(
        ICulturalFeatsService feats,
        IRevoltTuningConfigProvider revoltTuning)
    {
        _feats = feats;
        _revoltConfig = revoltTuning.GetConfig();
    }

    // v1.5.0: vanilla swings these on Options.IsHighRebellionEnabled (15/25 -> 50/60), which is how
    // the Advanced Starting Options "Civil Unrest" modifier takes effect. Overriding them with a
    // single hardcoded pair makes that modifier inert, so read the flag and pick the matching pair.
    // `?.` throughout: TaleWorlds computed getters throw before a plain null check, and Campaign
    // .Current is null in unit tests and on the main menu. Absent campaign means "not enabled".
    private static bool IsCivilUnrestEnabled =>
        Campaign.Current?.Options?.IsHighRebellionEnabled ?? false;

    public override int RebellionStartLoyaltyThreshold =>
        IsCivilUnrestEnabled
            ? _revoltConfig.HighRebellionStartLoyaltyThreshold
            : _revoltConfig.RebellionStartLoyaltyThreshold;

    public override int RebelliousStateStartLoyaltyThreshold =>
        IsCivilUnrestEnabled
            ? _revoltConfig.HighRebellionRebelliousStateStartLoyaltyThreshold
            : _revoltConfig.RebelliousStateStartLoyaltyThreshold;

    public override float SettlementOwnerDifferentCultureLoyaltyEffect =>
        _revoltConfig.SettlementOwnerDifferentCultureLoyaltyEffect;

    public override ExplainedNumber CalculateLoyaltyChange(Town town, bool includeDescriptions = false)
    {
        var result = base.CalculateLoyaltyChange(town, includeDescriptions);
        _feats.ApplyLoyaltyFeats(CultureFeatAdapter.FromOrNull(town.Owner?.Culture), ref result);
        return result;
    }
}
