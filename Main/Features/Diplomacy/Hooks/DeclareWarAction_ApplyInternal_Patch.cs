using System.Reflection;
using HarmonyLib;
using TAOM.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace TAOM.Features.Diplomacy.Hooks;

[HarmonyPatchCategory("Patch11_Diplomacy")]
public static class DeclareWarAction_ApplyInternal_Patch
{
    private static IOnAllianceAction _hook;
    private static IModLogger _logger;

    public static void Initialize(IOnAllianceAction hook)
    {
        _hook = hook;
    }

    public static void Initialize(IModLogger logger)
    {
        _logger = logger;
    }

    public static MethodBase TargetMethod()
    {
        return AccessTools.Method(typeof(DeclareWarAction), "ApplyInternal");
    }

    [HarmonyPriority(Priority.High)]
    [HarmonyPrefix]
    public static bool Prefix(
        IFaction faction1, IFaction faction2, DeclareWarAction.DeclareWarDetail declareWarDetail)
    {
        if (_hook == null)
        {
            _logger?.LogWarning("[Diplomacy] DeclareWarAction_ApplyInternal_Patch: hook not initialized");
            return true;
        }

        if (!faction1.IsKingdomFaction || !faction2.IsKingdomFaction)
            return true;

        if (_hook.ShouldPreventWarDeclaration(
                faction1.StringId, faction2.StringId, IsCausedByPlayer(declareWarDetail)))
        {
            // Phase 9b #153 — Prefix returns false to skip vanilla ApplyInternal. Vanilla then
            // skips its CampaignEventDispatcher.OnWarDeclared event dispatch (line 54 of v1.4
            // ApplyInternal). Listeners — including AllianceCampaignBehavior.OnWarDeclared which
            // handles forced alliance ends — do NOT receive notification of the blocked war
            // attempt. This is intentional behavior (the war never happened from vanilla's
            // perspective), but documented here so a future "force-declare war through TAOM's own
            // path" implementation knows to either (a) use DeclareWarAction.ApplyByX(...) which
            // emits the event, or (b) manually dispatch OnWarDeclared via
            // CampaignEventDispatcher.Instance after the forced-war state change. Diagnostic log
            // surfaces blocked attempts for visibility.
            _logger?.LogDebug($"[Diplomacy] DeclareWarAction blocked: {faction1.StringId} → {faction2.StringId}. " +
                              "OnWarDeclared event suppressed (vanilla behavior — listeners not notified of blocked war).");
            return false;
        }

        // A later prefix of another mod could still skip the declaration, so the line says allowed, not declared.
        _logger?.LogInfo(AllowedLine(faction1.StringId, faction2.StringId, declareWarDetail));
        return true;
    }

    // #1283: the engine path (an AI war vote, a call to war, a War of the Ring declaration) tells a long run's reader how a war began.
    // Kingdom pairs only: a rebel clan's war returns above. For a call to war the called realm comes first; the caller is in the
    // DiplomacyBehavior "Call to war started" line just before it.
    internal static string AllowedLine(string faction1Id, string faction2Id, DeclareWarAction.DeclareWarDetail detail)
    {
        return $"[Diplomacy] War declaration allowed: {faction1Id} -> {faction2Id} ({detail})";
    }

    // The player's own acts: the war may start before the script allows. A crime-rating war
    // matters most: with the war allowed, ChangeCrimeRatingAction latches once instead of re-applying
    // a relation loss every day. Founding a kingdom is the player's act too: in v1.5.5 only the player
    // reaches KingdomManager.CreateKingdom (the governor menu and the cheat; United Empire, the third
    // caller, is removed from the start options), and the founding carries the founder clan's wars over to it.
    internal static bool IsCausedByPlayer(DeclareWarAction.DeclareWarDetail detail)
    {
        return detail == DeclareWarAction.DeclareWarDetail.CausedByPlayerHostility
            || detail == DeclareWarAction.DeclareWarDetail.CausedByCrimeRatingChange
            || detail == DeclareWarAction.DeclareWarDetail.CausedByKingdomCreation;
    }
}
