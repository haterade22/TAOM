using TAOM.Adapters;
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
/// mapping, the strength inflation, and the conditional reach measurement inside the model body
/// broke that rule. Moving them here leaves the model as three straight-line statements.</para>
///
/// <para><b>It also fixes a real cost bug.</b> The inline version measured reach before
/// <c>ApplyTargetScoreModifiers</c> ever saw the master toggle, so with the feature disabled every
/// besieger candidate still walked the attacking faction's fortifications and hit the distance
/// cache. "Disabled is a no-op" was false at the hottest boundary. The toggle is now checked before
/// any measurement happens.</para>
/// </summary>
public interface ITargetScoreContextFactory
{
    /// <summary>
    /// Builds the context for one candidate. <c>BaseScore</c> is left unset: the caller fills it
    /// after invoking vanilla with <see cref="TargetScoreContext.EffectiveStrength"/>.
    /// </summary>
    TargetScoreContext Create(Settlement targetSettlement, Army.ArmyTypes missionType, MobileParty mobileParty, float ourStrength);
}

public class TargetScoreContextFactory : ITargetScoreContextFactory
{
    private readonly IArmyTargetingService _service;
    private readonly IArmyTargetingSettingsProvider _settings;
    private readonly IMapReachAdapter _reach;

    private object _observedCampaign;

    public TargetScoreContextFactory(
        IArmyTargetingService service,
        IArmyTargetingSettingsProvider settings,
        IMapReachAdapter reach)
    {
        _service = service;
        _settings = settings;
        _reach = reach;
    }

    public TargetScoreContext Create(Settlement targetSettlement, Army.ArmyTypes missionType, MobileParty mobileParty, float ourStrength)
    {
        ResetDiagnosticsOnNewCampaign();

        var mission = ArmyMissionMapper.FromArmyType(missionType);
        var attackerFaction = mobileParty?.MapFaction;
        string factionId = attackerFaction?.StringId;
        bool isBesieger = mission == ArmyTargetingMission.Besieger;

        return new TargetScoreContext
        {
            Mission = mission,
            FactionId = factionId,
            TargetFactionId = targetSettlement?.MapFaction?.StringId,
            TargetSettlementId = targetSettlement?.StringId,
            CommittedTargetId = (mobileParty?.Army?.AiBehaviorObject as Settlement)?.StringId,
            EffectiveStrength = _service.GetEffectiveStrength(factionId, isBesieger, ourStrength),
            NormalizedDistance = MeasureReach(isBesieger, targetSettlement, attackerFaction),
        };
    }

    /// <summary>
    /// Measured only when it will actually be used: for sieges, and only while the feature is on.
    /// Raiders are already hard-zeroed past five town gaps by vanilla's own
    /// <c>GetDistanceScoreForRaiding</c>, and a Defender's target is its own fief. NaN is the
    /// adapter's "unmeasurable" value, which the service reads as no suppression.
    /// </summary>
    private float MeasureReach(bool isBesieger, Settlement targetSettlement, IFaction attackerFaction)
    {
        if (!isBesieger) return float.NaN;
        if (!_settings.EnableArmyStrategicIntelligence) return float.NaN;
        return _reach.GetNormalizedDistanceToNearestFortification(targetSettlement, attackerFaction);
    }

    /// <summary>
    /// The service is a process-lifetime singleton, so its one-shot log latches would be spent on
    /// the first campaign of a session and stay silent for every campaign after it.
    /// </summary>
    private void ResetDiagnosticsOnNewCampaign()
    {
        var campaign = Campaign.Current;
        if (ReferenceEquals(campaign, _observedCampaign)) return;

        _observedCampaign = campaign;
        if (campaign != null) _service.ResetDiagnostics();
    }
}
