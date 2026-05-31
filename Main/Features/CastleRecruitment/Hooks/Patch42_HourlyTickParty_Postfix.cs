using System;
using System.Reflection;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TAOM.Core.Logging;

namespace TAOM.Features.CastleRecruitment.Hooks;

/// <summary>
/// Patch42 (T2) — Postfix on <c>RecruitmentCampaignBehavior.HourlyTickParty</c>. Vanilla routes AI
/// lord/caravan parties present in a town/village to the private <c>CheckRecruiting</c>, but skips
/// castles (its line-300 gate is <c>IsVillage || IsTown</c>). We replicate vanilla's own outer guards
/// and, for an AI party sitting in a non-besieged castle, invoke <c>CheckRecruiting</c> — reusing
/// vanilla's exact recruit logic rather than reimplementing it. Runs exactly once per tick and never
/// for the main party, so there is no double-recruit.
///
/// The private <c>CheckRecruiting</c> is bound once to an open-instance delegate (no per-call
/// reflection or argument-array allocation — this runs per-AI-party per-hour).
/// </summary>
[HarmonyPatchCategory("Patch42_CastleRecruitment")]
public static class Patch42_HourlyTickParty_Postfix
{
    private static ICastleRecruitmentSettingsProvider? _settings;
    private static IModLogger? _logger;
    private static Action<RecruitmentCampaignBehavior, MobileParty, Settlement>? _checkRecruiting;

    public static void Initialize(ICastleRecruitmentSettingsProvider settings, IModLogger logger)
    {
        _settings = settings;
        _logger = logger;

        var method = AccessTools.Method(typeof(RecruitmentCampaignBehavior), "CheckRecruiting",
            new[] { typeof(MobileParty), typeof(Settlement) });
        if (method == null)
        {
            logger.LogWarning("[CastleRecruitment] RecruitmentCampaignBehavior.CheckRecruiting not found — AI castle recruitment disabled");
            return;
        }
        try
        {
            _checkRecruiting = (Action<RecruitmentCampaignBehavior, MobileParty, Settlement>)Delegate.CreateDelegate(
                typeof(Action<RecruitmentCampaignBehavior, MobileParty, Settlement>), method);
        }
        catch (Exception ex)
        {
            logger.LogError($"[CastleRecruitment] Failed to bind CheckRecruiting delegate: {ex.GetType().Name}: {ex.Message} — AI castle recruitment disabled");
            _checkRecruiting = null;
        }
    }

    public static MethodBase TargetMethod() =>
        AccessTools.Method(typeof(RecruitmentCampaignBehavior), "HourlyTickParty", new[] { typeof(MobileParty) });

    [HarmonyPostfix]
    public static void Postfix(RecruitmentCampaignBehavior __instance, MobileParty mobileParty)
    {
        if (_settings == null || _checkRecruiting == null || !_settings.IsEnabled || !_settings.IsAiEnabled)
            return;
        // Vanilla HourlyTickParty's own guards (line 293): AI lord/caravan parties only, not in a
        // map event, never the main party.
        if ((!mobileParty.IsCaravan && !mobileParty.IsLordParty) || mobileParty.MapEvent != null || mobileParty == MobileParty.MainParty)
            return;
        Settlement settlement = MobilePartyHelper.GetCurrentSettlementOfMobilePartyForAICalculation(mobileParty);
        if (settlement == null || !settlement.IsCastle || settlement.IsUnderSiege)
            return;
        try
        {
            _checkRecruiting(__instance, mobileParty, settlement);
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[CastleRecruitment] CheckRecruiting failed: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
