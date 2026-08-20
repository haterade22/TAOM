using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TAOM.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.ScreenSystem;

namespace TAOM.Features.MapLoadDiagnostics.Hooks;

/// <summary>
/// Per-frame heartbeat on the campaign map. Thin entry point per ADR-002: reads the engine and
/// delegates every decision and all formatting to <see cref="IMapLoadHeartbeatService"/>.
///
/// <para>
/// <c>Campaign.RealTick(float)</c> is internal, so it is resolved through <c>AccessTools</c> rather
/// than a typed attribute. It is the per-frame campaign tick that the v1.5.0 stall dumps caught
/// executing, so it is the one place guaranteed to run while the map appears frozen. Bracketing it
/// with a stopwatch also answers how much of each frame the simulation actually accounts for.
/// </para>
///
/// <para>
/// The party census walks every mobile party and therefore runs ONLY on frames that emit, gated by
/// <c>ShouldEmit</c>. Walking 2,000-odd parties every frame would add to the very cost being
/// measured.
/// </para>
///
/// <para>
/// Everything is wrapped and the heartbeat self-disables after a single failure: a diagnostic must
/// never become the thing that breaks the load it was added to explain, and must not throw once per
/// frame. Service and logger are captured once into statics, because resolving through IoC on a
/// per-frame path is what the hot-path rule forbids.
/// </para>
/// </summary>
[HarmonyPatch]
[HarmonyPatchCategory("Patch66_MapLoadDiagnostics")]
public static class Campaign_RealTick_MapLoad_Patch
{
    private static IMapLoadHeartbeatService _service;
    private static IModLogger _logger;

    public static void Initialize(IMapLoadHeartbeatService service, IModLogger logger)
    {
        _service = service;
        _logger = logger;
    }

    static MethodBase TargetMethod() => AccessTools.Method(typeof(Campaign), "RealTick");

    [HarmonyPrefix]
    public static void Prefix(out Stopwatch __state) => __state = Stopwatch.StartNew();

    [HarmonyPostfix]
    public static void Postfix(Stopwatch __state)
    {
        var service = _service;
        if (service == null) return;

        try
        {
            var tickMs = __state?.Elapsed.TotalMilliseconds ?? 0d;
            var now = DateTime.UtcNow;
            if (!service.ShouldEmit(now, tickMs)) return;

            var campaign = Campaign.Current;
            if (campaign == null) return;

            int lord = 0, villager = 0, caravan = 0, bandit = 0, militia = 0, garrison = 0, other = 0;
            var parties = campaign.MobileParties;
            var total = parties?.Count ?? 0;
            for (int i = 0; i < total; i++)
            {
                var p = parties[i];
                if (p == null) { other++; continue; }
                // Ordered most-specific first; a party matches exactly one bucket.
                if (p.IsVillager) villager++;
                else if (p.IsCaravan) caravan++;
                else if (p.IsBandit) bandit++;
                else if (p.IsMilitia) militia++;
                else if (p.IsGarrison) garrison++;
                else if (p.IsLordParty) lord++;
                else other++;
            }

            // The whole state stack, bottom to top. A state pushed above MapState would hold the
            // loading overlay while the map ticks underneath, which is the shape the heartbeat found.
            var stack = "?";
            var active = "?";
            try
            {
                var gsm = Game.Current?.GameStateManager;
                if (gsm != null)
                {
                    active = gsm.ActiveState?.GetType().Name ?? "<null>";
                    stack = string.Join(" > ", gsm.GameStates.Select(s => s?.GetType().Name ?? "<null>"));
                }
            }
            catch { /* diagnostic only */ }

            var topScreen = "?";
            try { topScreen = ScreenManager.TopScreen?.GetType().Name ?? "<null>"; }
            catch { /* diagnostic only */ }

            var timeControl = "?";
            try { timeControl = $"{campaign.TimeControlMode}{(campaign.TimeControlModeLock ? "(locked)" : "")}"; }
            catch { /* diagnostic only */ }

            var sample = new MapLoadSample(
                partyCount: total,
                lordParties: lord, villagers: villager, caravans: caravan, bandits: bandit,
                militia: militia, garrisons: garrison, otherParties: other,
                settlementCount: Settlement.All?.Count ?? -1,
                heroCount: Hero.AllAliveHeroes?.Count ?? -1,
                clanCount: campaign.Clans?.Count ?? -1,
                campaignTime: Campaign.CurrentTime,
                isLoadingWindowActive: LoadingWindow.IsLoadingWindowActive,
                tickMsAvg: service.TickMsAverage,
                activeState: active, stateStack: stack, topScreen: topScreen, timeControl: timeControl);

            _logger?.LogInfo(service.BuildLine(now, sample));
        }
        catch (Exception ex)
        {
            _service = null;
            try { _logger?.LogError($"[MapLoad] heartbeat disabled after error: {ex.GetType().Name}: {ex.Message}"); }
            catch { /* diagnostic only */ }
        }
    }
}
