using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace TAOM.Features.ArmyTargeting;

/// <summary>
/// Turns the sealed TaleWorlds arguments of <c>GetTargetScoreForFaction</c> into a
/// <see cref="TargetScoreContext"/>.
///
/// <para><b>Why this type exists rather than doing it inline.</b> gamemodels.md rule 4 is binary:
/// a model override body may hold a single constant expression, or boundary conversion plus a
/// direct delegate, and any branching or multi-line computation is a violation. Doing the mission
/// mapping and the strength inflation inside the model body broke that rule.</para>
/// </summary>
public interface ITargetScoreContextFactory
{
    /// <summary>
    /// Builds the context for one candidate. <c>BaseScore</c> is left unset: the caller fills it
    /// after invoking vanilla with <see cref="TargetScoreContext.EffectiveStrength"/>.
    /// </summary>
    TargetScoreContext Create(Settlement targetSettlement, Army.ArmyTypes missionType, MobileParty mobileParty, float ourStrength);

    /// <summary>Drops the campaign reference held for diagnostics. Called on campaign teardown.</summary>
    void Reset();
}

public class TargetScoreContextFactory : ITargetScoreContextFactory
{
    private readonly IArmyTargetingService _service;

    private object _observedCampaign;

    public TargetScoreContextFactory(IArmyTargetingService service)
    {
        _service = service;
    }

    public TargetScoreContext Create(Settlement targetSettlement, Army.ArmyTypes missionType, MobileParty mobileParty, float ourStrength)
    {
        ResetDiagnosticsOnNewCampaign();

        var mission = ArmyMissionMapper.FromArmyType(missionType);
        string factionId = mobileParty?.MapFaction?.StringId;

        return new TargetScoreContext
        {
            Mission = mission,
            FactionId = factionId,
            TargetFactionId = targetSettlement?.MapFaction?.StringId,
            TargetSettlementId = targetSettlement?.StringId,
            CommittedTargetId = (mobileParty?.Army?.AiBehaviorObject as Settlement)?.StringId,
            EffectiveStrength = _service.GetEffectiveStrength(
                factionId, mission == ArmyTargetingMission.Besieger, ourStrength),
        };
    }

    /// <summary>
    /// The service is a process-lifetime singleton, so its one-shot log latches would be spent on
    /// the first campaign of a session and stay silent for every campaign after it. The reference
    /// held here is cleared by <c>ArmyTargetingLifecycleBehavior</c> on campaign teardown so a
    /// finalized campaign is not kept alive by this field.
    /// </summary>
    private void ResetDiagnosticsOnNewCampaign()
    {
        var campaign = Campaign.Current;
        if (ReferenceEquals(campaign, _observedCampaign)) return;

        _observedCampaign = campaign;
        if (campaign != null) _service.ResetDiagnostics();
    }

    /// <summary>Drops the campaign reference at teardown. Called from SubModule.OnGameEnd.</summary>
    public void Reset() => _observedCampaign = null;
}
