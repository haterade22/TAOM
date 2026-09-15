using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using HarmonyLib;
using TAOM.Core.Logging;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade.View;

namespace TAOM.Features.PreloadBodyGuard.Hooks;

/// <summary>
/// Keeps a missing collision body from freezing the game (#352, #599, #601).
///
/// <para>
/// <c>PreloadHelper.WaitForMeshesToBeLoaded</c> (installed v1.5.3, byte-identical in shape to
/// v1.4.8) is a do/while with no exit that counts, on every pass, each name in
/// <c>_uniqueDynamicPhysicsShapeName</c> for which <c>PhysicsShape.GetFromResource(name, true)</c>
/// returns null. A body no loaded tpac ships never resolves, so one bad <c>body_name</c> on any item
/// in the preload set pins the game thread on the first frame after the loading window drops. Six
/// callers, all main thread: <c>MissionPreloadView</c> (campaign battles and sieges),
/// <c>ArenaPreloadView</c> (tournaments; the player's kit is first in that set),
/// <c>MissionCustomBattlePreloadView</c>, <c>MissionMultiplayerPreloadView</c>,
/// <c>NavalShipsPreloadView</c>, and <c>GauntletEducationScreen.OnFrameTick</c>.
/// </para>
///
/// <para>
/// This prefix repairs the precondition vanilla relies on instead of replacing the method: it
/// drains the names that cannot resolve out of the set, with a bounded wait so "not loaded yet"
/// is not mistaken for "not there", logs each drop at ERROR, and returns <c>true</c> so vanilla's
/// loop runs unchanged against a set that can now empty. <c>_uniqueMetaMeshes</c> is never touched.
/// The set arrives by Harmony field injection: four underscores, because the engine's field name
/// starts with one. A healthy load pays one pass of resolved lookups; a broken one pays the budget
/// once and then loads with that weapon carrying no collision shape, which is a visible, logged
/// defect in place of a frozen game.
/// </para>
///
/// <para>
/// Everything is wrapped: a guard that throws inside the load it protects is worse than the hang.
/// On its own failure it logs once and lets vanilla proceed exactly as before the patch existed.
/// </para>
/// </summary>
[HarmonyPatch(typeof(PreloadHelper), nameof(PreloadHelper.WaitForMeshesToBeLoaded))]
[HarmonyPatchCategory("Patch90_PreloadBodyGuard")]
public static class PreloadHelper_WaitForMeshesToBeLoaded_Patch
{
    /// <summary>Generous on purpose: a false drop removes a real weapon's collision shape, a late
    /// drop costs a few seconds once. Healthy loads resolve on the first pass and never wait.</summary>
    public const double BudgetSeconds = 5.0;

    private static IPreloadBodyGuardService _service;
    private static IModLogger _logger;

    public static void Initialize(IPreloadBodyGuardService service, IModLogger logger)
    {
        _service = service;
        _logger = logger;
    }

    [HarmonyPrefix]
    public static bool Prefix(HashSet<string> ____uniqueDynamicPhysicsShapeName)
    {
        try
        {
            if (_service == null || ____uniqueDynamicPhysicsShapeName == null
                || ____uniqueDynamicPhysicsShapeName.Count == 0)
                return true;

            var clock = Stopwatch.StartNew();
            var dropped = _service.DrainUnresolvable(
                ____uniqueDynamicPhysicsShapeName,
                name => PhysicsShape.GetFromResource(name, true) != null,
                () => clock.Elapsed.TotalSeconds,
                () => Thread.Sleep(1),
                BudgetSeconds);

            if (dropped.Count > 0)
            {
                _logger?.LogError(
                    $"[PreloadGuard] dropped {dropped.Count} unresolvable collision body name(s) after " +
                    $"{clock.Elapsed.TotalSeconds:F1}s so the mission can load: {string.Join(", ", dropped)}. " +
                    "No loaded tpac ships them; the items naming them have no collision shape this mission. " +
                    "Repoint the body_name to the art that ships: python tools/audit_armory_refs.py names the items and troops (#599).");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[PreloadGuard] guard failed, vanilla wait runs unguarded: {ex.GetType().Name}: {ex.Message}");
        }
        return true;
    }
}
