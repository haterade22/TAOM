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
    public static bool Prefix(IFaction faction1, IFaction faction2)
    {
        if (_hook == null)
        {
            _logger?.LogWarning("[Diplomacy] DeclareWarAction_ApplyInternal_Patch: hook not initialized");
            return true;
        }

        if (!faction1.IsKingdomFaction || !faction2.IsKingdomFaction)
            return true;

        if (_hook.ShouldPreventWarDeclaration(faction1.StringId, faction2.StringId))
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

        return true;
    }
}
