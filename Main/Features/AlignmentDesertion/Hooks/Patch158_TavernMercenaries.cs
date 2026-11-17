using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;

namespace TAOM.Features.AlignmentDesertion.Hooks;

/// <summary>
/// Patch158, #1189 item 1: an AI lord's tavern hire. Prefix on the private <c>RecruitmentCampaignBehavior.ApplyInternal</c>
/// (v1.5.7), acting only on <c>RecruitingDetail.MercenaryFromTavern</c>, which only <c>ApplyRecruitMercenary</c> passes; both
/// branches of <c>CheckRecruiting</c> (caravans and lord parties) hire through it. <c>ApplyRecruitMercenary</c> itself is 15
/// bytes of IL, so a caller compiled before TAOM's patch (Coop patches <c>CheckRecruiting</c> before the save loads) could
/// have inlined it; <c>ApplyInternal</c> is too large to inline. Vanilla takes the town's mercenary troop with no recruit
/// model, so a lord bought mercenaries that alignment desertion took the next day. For a troop that
/// <see cref="IDesertionRecruitGuard.WouldDesertFromParty"/> blocks, the prefix skips the hire: no gold is paid and the
/// town keeps its mercenaries. The guard is false for caravans. The guard reads per-peer settings, so while
/// <see cref="ICoopSessionProvider.ShouldDeferToHost"/> holds every hire runs vanilla.
/// Notes: docs/reference/harmony-patch-registry.md "Patch158_DesertionMercenaries".
/// </summary>
[HarmonyPatchCategory(AlignmentDesertionModule.MercenaryPatchCategory)]
public static class Patch158_AiMercenaryHire_Prefix
{
    private static IDesertionRecruitGuard? _guard;
    private static ICoopSessionProvider? _coop;
    private static IModLogger? _logger;
    private static bool _logged;

    public static void Initialize(IDesertionRecruitGuard guard, ICoopSessionProvider coop, IModLogger logger)
    {
        _guard = guard;
        _coop = coop;
        _logger = logger;
    }

    public static MethodBase TargetMethod() =>
        AccessTools.Method(typeof(RecruitmentCampaignBehavior), "ApplyInternal",
            new[]
            {
                typeof(MobileParty), typeof(Settlement), typeof(Hero), typeof(CharacterObject), typeof(int), typeof(int),
                typeof(RecruitmentCampaignBehavior.RecruitingDetail),
            });

    [HarmonyPrefix]
    public static bool Prefix(MobileParty side1Party, CharacterObject troop, RecruitmentCampaignBehavior.RecruitingDetail detail)
    {
        var guard = _guard;
        if (detail != RecruitmentCampaignBehavior.RecruitingDetail.MercenaryFromTavern || guard == null || side1Party == null)
            return true;

        try
        {
            return !guard.WouldDesertFromParty(side1Party.Party, troop) || _coop?.ShouldDeferToHost != false;
        }
        catch (Exception ex)
        {
            if (!_logged)
            {
                _logged = true;
                _logger?.LogError($"[AlignDesert] Patch158 AI mercenary prefix failed, keeping vanilla's hire: {ex.GetType().Name}");
            }

            return true;
        }
    }
}

/// <summary>
/// Patch158, #1189 item 1: the player's "Recruit mercenaries" option in the town backstreet menu. Postfix on the private
/// <c>RecruitmentCampaignBehavior.buy_mercenaries_condition(MenuCallbackArgs)</c> (v1.5.7). When vanilla shows the option and
/// the town's mercenaries would desert the player's party, the option is disabled and its tooltip names the cause.
/// </summary>
[HarmonyPatchCategory(AlignmentDesertionModule.MercenaryPatchCategory)]
public static class Patch158_MercenaryMenu_Postfix
{
    public static MethodBase TargetMethod() =>
        AccessTools.Method(typeof(RecruitmentCampaignBehavior), "buy_mercenaries_condition", new[] { typeof(MenuCallbackArgs) });

    [HarmonyPostfix]
    public static void Postfix(RecruitmentCampaignBehavior __instance, MenuCallbackArgs args, bool __result)
    {
        if (!__result || args == null)
            return;
        var town = MobileParty.MainParty?.CurrentSettlement?.Town;
        if (town == null || !TavernMercenaries.WouldDesertThePlayer(__instance.GetMercenaryData(town).TroopType))
            return;

        args.IsEnabled = false;
        args.Tooltip = new TextObject("{=taom_mercenary_menu_would_desert}These mercenaries would desert your party: their people will not serve your side.");
    }
}

/// <summary>
/// Patch158, #1189 item 1: the tavern mercenary's offer. Postfix on <c>RecruitmentCampaignBehavior.AddDialogs</c> (v1.5.7)
/// adds one line on the "start" token at priority 110, above vanilla's offer lines (100), so a mercenary who would desert
/// the player's party refuses instead of offering. It fires only where vanilla's offer would (a mercenary, caravan guard
/// or gangster in a town whose mercenaries are still for hire), for the speaker, the troop <c>BuyMercenaries</c> adds.
/// </summary>
[HarmonyPatchCategory(AlignmentDesertionModule.MercenaryPatchCategory)]
public static class Patch158_MercenaryDialog_Postfix
{
    internal const string LineId = "taom_mercenary_recruit_refuse";

    public static MethodBase TargetMethod() =>
        AccessTools.Method(typeof(RecruitmentCampaignBehavior), "AddDialogs", new[] { typeof(CampaignGameStarter) });

    [HarmonyPostfix]
    public static void Postfix(RecruitmentCampaignBehavior __instance, CampaignGameStarter campaignGameStarter)
    {
        campaignGameStarter?.AddDialogLine(LineId, "start", "close_window",
            "{=taom_mercenary_refuses_your_side}Fight for your side? No coin buys that. Find your swords elsewhere.",
            () => Refuses(__instance), null, 110);
    }

    private static bool Refuses(RecruitmentCampaignBehavior behavior)
    {
        var troop = CharacterObject.OneToOneConversationCharacter;
        var settlement = PlayerEncounter.EncounterSettlement;
        if (troop == null || settlement == null || !settlement.IsTown
            || (troop.Occupation != Occupation.Mercenary && troop.Occupation != Occupation.CaravanGuard && troop.Occupation != Occupation.Gangster))
            return false;
        return behavior.GetMercenaryData(settlement.Town).Number > 0 && TavernMercenaries.WouldDesertThePlayer(troop);
    }
}

/// <summary>The player-side question both Patch158 postfixes ask, with one fault log.</summary>
public static class TavernMercenaries
{
    private static IDesertionRecruitGuard? _guard;
    private static IModLogger? _logger;
    private static bool _logged;

    public static void Initialize(IDesertionRecruitGuard guard, IModLogger logger)
    {
        _guard = guard;
        _logger = logger;
    }

    /// <summary>True when <paramref name="troop"/> would desert the player's party. A fault answers false (vanilla).</summary>
    public static bool WouldDesertThePlayer(CharacterObject? troop)
    {
        var guard = _guard;
        if (guard == null || troop == null)
            return false;
        try
        {
            return guard.WouldDesertFromParty(MobileParty.MainParty?.Party, troop);
        }
        catch (Exception ex)
        {
            if (!_logged)
            {
                _logged = true;
                _logger?.LogError($"[AlignDesert] Patch158 player mercenary check failed, vanilla offer kept: {ex.GetType().Name}");
            }

            return false;
        }
    }
}
