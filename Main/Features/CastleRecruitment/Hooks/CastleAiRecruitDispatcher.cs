using System;
using System.Reflection;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TAOM.Core.Logging;

namespace TAOM.Features.CastleRecruitment.Hooks;

/// <summary>
/// Engine glue for the in-castle AI recruit. Vanilla's <c>RecruitmentCampaignBehavior.HourlyTickParty</c>
/// routes AI lord/caravan parties in a town/village to the private <c>CheckRecruiting</c> but skips
/// castles. For an AI lord party in a non-besieged castle this replicates vanilla's outer guards and calls
/// <c>CheckRecruiting</c>, reusing vanilla's recruit logic. Called once per party per hour from
/// <see cref="CastleRecruitmentBehavior"/>, never for the main party, so there is no double-recruit.
/// Caravans never recruit in a castle (CheckRecruiting's caravan branch is town-only).
///
/// The private method is bound once per campaign, at session launch, to a closed delegate over the
/// vanilla behavior (no per-call reflection or argument array: this runs per AI party per hour). A bind
/// miss logs once and stops only the hourly in-castle top-up, because vanilla's OnBeforeSettlementEntered
/// still recruits on entry.
/// </summary>
internal sealed class CastleAiRecruitDispatcher
{
    private readonly IModLogger _logger;
    private Action<MobileParty, Settlement>? _checkRecruiting;

    public CastleAiRecruitDispatcher(IModLogger logger) => _logger = logger;

    internal static MethodInfo? FindCheckRecruiting() =>
        AccessTools.Method(typeof(RecruitmentCampaignBehavior), "CheckRecruiting", new[] { typeof(MobileParty), typeof(Settlement) });

    /// <summary>Binds vanilla's CheckRecruiting at session launch.</summary>
    public void Bind()
    {
        _checkRecruiting = null;
        var vanilla = Campaign.Current?.GetCampaignBehavior<RecruitmentCampaignBehavior>();
        if (vanilla == null)
        {
            _logger.LogWarning("[CastleRecruitment] RecruitmentCampaignBehavior not found, in-castle AI recruit inert");
            return;
        }
        var method = FindCheckRecruiting();
        if (method == null)
        {
            _logger.LogWarning("[CastleRecruitment] RecruitmentCampaignBehavior.CheckRecruiting not found, in-castle AI recruit inert");
            return;
        }
        try
        {
            _checkRecruiting = (Action<MobileParty, Settlement>)Delegate.CreateDelegate(
                typeof(Action<MobileParty, Settlement>), vanilla, method);
        }
        catch (Exception ex)
        {
            _logger.LogError($"[CastleRecruitment] CheckRecruiting bind failed: {ex.GetType().Name}: {ex.Message}, in-castle AI recruit inert");
        }
    }

    public void TryRecruit(MobileParty mobileParty)
    {
        var checkRecruiting = _checkRecruiting;
        if (checkRecruiting == null) return;
        // Vanilla HourlyTickParty's own guards: AI lord/caravan parties only, not in a map event,
        // never the main party.
        if ((!mobileParty.IsCaravan && !mobileParty.IsLordParty) || mobileParty.MapEvent != null || mobileParty == MobileParty.MainParty)
            return;
        Settlement settlement = MobilePartyHelper.GetCurrentSettlementOfMobilePartyForAICalculation(mobileParty);
        if (settlement == null || !settlement.IsCastle || settlement.IsUnderSiege)
            return;
        try
        {
            checkRecruiting(mobileParty, settlement);
        }
        catch (Exception ex)
        {
            _logger.LogError($"[CastleRecruitment] CheckRecruiting failed: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
