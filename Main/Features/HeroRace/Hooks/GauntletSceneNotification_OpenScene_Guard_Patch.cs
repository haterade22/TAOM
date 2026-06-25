using System;
using HarmonyLib;
using TAOM.Core.Logging;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade.GauntletUI.SceneNotification;
using TaleWorlds.MountAndBlade.View;

namespace TAOM.Features.HeroRace.Hooks;

/// <summary>
/// Patch56_SceneNotificationVisualGuard — Finalizer that prevents the become-king (and sibling) cinematic CTD.
///
/// WHY: becoming the ruler of a kingdom fires the engine's <c>BecomeKingSceneNotificationItem</c>
/// (<c>scn_become_king_notification</c>), raised from
/// <c>DefaultCutscenesCampaignBehavior.OnKingdomDecisionConcluded</c> (active campaign only — so this patch is
/// registered in <c>SubModule.OnGameInitializationFinished</c>, not the cold-menu site Patch55 uses). The
/// cinematic enqueues ~20 characters (the new ruler + the kingdom culture's townsfolk + bodyguards + the
/// ruler's clan companions) and renders each through the RAW engine scene-notification path
/// <c>GauntletSceneNotification.OpenScene</c> → <c>PopupSceneSpawnPoint.InitializeWithAgentVisuals</c>. That
/// engine method dereferences the human <c>AgentVisuals</c> WITHOUT a null guard
/// (<c>PopupSceneSpawnPoint.cs:91</c> <c>GetCopyAgentVisualsData()</c> then <c>:92 GetEquipment().Clone(false)</c>,
/// and the unconditional else at <c>:108/109</c>). The MOUNT visual is fully null-guarded (foot characters
/// legitimately have none), so the asymmetry is the engine bug: it assumes the human visual is always non-null.
/// When any one character's visual build yields null, the whole cinematic NREs (managed
/// <c>System.NullReferenceException</c>, HResult 0x80004003 — catchable here). No TAOM patch is on the crash
/// stack — unguarded engine code, the FOURTH raw custom-race/visual render path TAOM has to guard (siblings:
/// Save/Load preview → Patch55_BasicTableauRaceGuard; in-game tableaus → CharacterTableauService; mission spawns
/// → CharacterSpawnerService). The same path backs the behavior's KingdomCreated/JoinKingdom/Marriage/death
/// notifications, so the guard is intentionally generic rather than keyed to one notification type.
///
/// The Finalizer swallows ONLY <see cref="NullReferenceException"/> — the exact, observed crash class; this is
/// the deliberate SEH-specificity discipline (mirrors Patch49/Patch50), NOT a blanket catch. A non-NRE
/// build-failure (e.g. <c>AgentVisuals.Create</c> throwing for a degenerate custom race) would still propagate
/// to TAOM's generic CrashReport finalizers — i.e. a diagnostic bundle, not a guarded abort. Returning to
/// <c>OnTick</c> normally lets it run <c>GauntletSceneNotification.cs:135</c> (<c>_isPendingSceneLoad = false</c>)
/// so there is no per-frame re-crash loop.
///
/// DEFERRED CLOSE (deep-review MED, 2026-06-25): the Finalizer does NOT call
/// <c>MBInformationManager.HideSceneNotification()</c> synchronously. If it did, the resulting
/// <c>CloseNotification()</c> would release focus/input (<c>GauntletSceneNotification.cs:469-472</c>) — but
/// control then returns to <c>OnTick</c>, which UNCONDITIONALLY re-acquires <c>IsFocusLayer=true</c> +
/// <c>SetInputRestrictions(true)</c> at <c>:127-129</c> on the now-closed layer, leaving the campaign map
/// focus/input-locked (a soft-lock — strictly as bad as the CTD). Instead the Finalizer raises
/// <see cref="CloseRequested"/>; the sibling <see cref="GauntletSceneNotification_OnTick_DeferredClose_Patch"/>
/// Postfix runs <c>HideSceneNotification()</c> AFTER the full <c>OnTick</c> body (so the input release wins).
/// Net effect: a cinematic that CAN render still plays; one that would crash aborts and the campaign continues
/// with input intact — strictly better than a CTD.
/// </summary>
[HarmonyPatch(typeof(GauntletSceneNotification), "OpenScene")]
[HarmonyPatchCategory("Patch56_SceneNotificationVisualGuard")]
public static class GauntletSceneNotification_OpenScene_Guard_Patch
{
    private static IModLogger _logger;

    /// <summary>
    /// Set by the Finalizer when it swallows an OpenScene NRE; consumed by the OnTick Postfix to run the
    /// notification teardown AFTER OnTick re-touches focus/input (see the deferred-close note above). Static
    /// because the Finalizer and the Postfix are separate patch classes coordinating across one OnTick frame;
    /// the engine has a single active GauntletSceneNotification layer.
    /// </summary>
    internal static bool CloseRequested;

    [HarmonyFinalizer]
    public static Exception Finalizer(Exception __exception)
    {
        if (!(__exception is NullReferenceException)) return __exception; // re-throw anything else

        try
        {
            _logger ??= IoC.Resolve<IModLogger>();
            _logger?.LogDebug(
                "SceneNotificationVisualGuard: suppressed NRE building a scene-notification character " +
                "visual (e.g. become-king cinematic); aborting the cinematic cleanly (deferred close).");
            CloseRequested = true; // dequeued by the OnTick Postfix, after OnTick re-touches focus/input
        }
        catch
        {
            // IoC not ready / logging failure must never re-throw out of a Finalizer.
        }

        return null; // swallow — the campaign continues, no CTD
    }
}

/// <summary>
/// Deferred-close Postfix (same Patch56 category) on <c>GauntletSceneNotification.OnTick</c>. When the OpenScene
/// Finalizer raised <see cref="GauntletSceneNotification_OpenScene_Guard_Patch.CloseRequested"/>, this Postfix
/// — running AFTER the full OnTick body, i.e. after the <c>:127-129</c> focus/input re-acquire — tears the
/// aborted notification down via <c>MBInformationManager.HideSceneNotification()</c>. Because it runs last, the
/// <c>CloseNotification()</c> focus/input release is the final word and the campaign map is left usable. Hot
/// path: a single static-bool read per frame on the common (no-abort) path — the body only runs in the frame an
/// abort was flagged.
/// </summary>
[HarmonyPatch(typeof(GauntletSceneNotification), "OnTick", new[] { typeof(float) })]
[HarmonyPatchCategory("Patch56_SceneNotificationVisualGuard")]
public static class GauntletSceneNotification_OnTick_DeferredClose_Patch
{
    [HarmonyPostfix]
    public static void Postfix()
    {
        if (!GauntletSceneNotification_OpenScene_Guard_Patch.CloseRequested)
            return;

        GauntletSceneNotification_OpenScene_Guard_Patch.CloseRequested = false;
        try
        {
            // Dequeues the half-built popup: HideSceneNotification → OnHideSceneNotification → CloseNotification
            // (null-tolerant teardown — _isActive=false, destroys character scripts + scene, restores input/pause).
            // Running here, after OnTick's :127-129 re-lock, makes that input/focus release the final state.
            MBInformationManager.HideSceneNotification();
        }
        catch
        {
            // Teardown failure must never re-throw out of a per-frame Postfix.
        }
    }
}

/// <summary>
/// Diagnostic Prefix (same Patch56 category) on the public
/// <c>PopupSceneSpawnPoint.InitializeWithAgentVisuals(AgentVisuals humanVisuals, AgentVisuals mountVisuals)</c>.
///
/// PURE DIAGNOSTIC — never alters behavior (void prefix, never returns false). Static analysis could not name
/// the exact null member behind the crash (every reported become-king character resolves to a valid race-0
/// human), so this probes the engine's OWN first unguarded derefs — <c>GetCopyAgentVisualsData()</c>
/// (<c>PopupSceneSpawnPoint.cs:91/108</c>) then <c>GetEquipment()</c> (<c>:92/109 .Clone()</c>) — and logs which
/// one fails right before the engine NREs on it, so the next occurrence self-identifies the failing deref
/// without a live debugger (deep-review MED, 2026-06-25: the earlier version probed only <c>GetEquipment()==null</c>,
/// which never fires on the real path because the engine's FIRST deref is <c>GetCopyAgentVisualsData()</c> and
/// become-king equipment is always non-null). It only logs when a probe fails — a well-formed visual passes both
/// and stays silent. Every access is try/caught so the diagnostic itself can never throw out of the prefix. An
/// empty log is NOT proof the visual was well-formed: an NRE deeper in the native skeleton/visual build (past
/// these two managed derefs) would still be caught by the OpenScene Finalizer without a probe line here.
/// </summary>
[HarmonyPatch(typeof(PopupSceneSpawnPoint), nameof(PopupSceneSpawnPoint.InitializeWithAgentVisuals))]
[HarmonyPatchCategory("Patch56_SceneNotificationVisualGuard")]
public static class PopupSceneSpawnPoint_InitializeWithAgentVisuals_Diagnostic_Patch
{
    private static IModLogger _logger;

    [HarmonyPrefix]
    public static void Prefix(AgentVisuals humanVisuals)
    {
        try
        {
            if (humanVisuals == null)
            {
                LogProbe("humanVisuals is null");
                return;
            }

            // Replicate the engine's FIRST unguarded human deref (PopupSceneSpawnPoint.cs:91/108).
            try
            {
                humanVisuals.GetCopyAgentVisualsData();
            }
            catch (Exception ex)
            {
                LogProbe("GetCopyAgentVisualsData() threw " + ex.GetType().Name);
                return;
            }

            // Replicate the engine's SECOND unguarded human deref target (:92/109 — GetEquipment().Clone()).
            Equipment equipment;
            try
            {
                equipment = humanVisuals.GetEquipment();
            }
            catch (Exception ex)
            {
                LogProbe("GetEquipment() threw " + ex.GetType().Name);
                return;
            }

            if (equipment == null)
                LogProbe("GetEquipment() returned null (engine GetEquipment().Clone() will NRE)");

            // Both engine derefs pass → this character is well-formed at the managed level; stay silent.
        }
        catch
        {
            // Diagnostic must never throw out of the prefix.
        }
    }

    private static void LogProbe(string failingMember)
    {
        _logger ??= IoC.Resolve<IModLogger>();
        _logger?.LogDebug(
            "SceneNotificationVisualGuard(diag): a scene-notification human visual fails at " + failingMember +
            " — this character's render NREs in PopupSceneSpawnPoint.InitializeWithAgentVisuals (caught by Patch56).");
    }
}
