using System;
using System.Linq;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TAOM.Composition;
using TAOM.Core.Logging;

namespace TAOM.Core.Diagnostics;

/// <summary>
/// Issue #1185: shows the fixes that turned themselves off (<see cref="SelfDisables"/>), in two steps on the main
/// thread. <see cref="Record"/> writes one WARNING per new entry on the first tick, so the log has it even when the
/// session ends at the main menu or during a load. <see cref="Show"/> then names the logged entries in one red chat
/// line, and writes one [Check] line per id, once the chat can show it (<see cref="Gate"/>). On a dedicated server
/// it writes the WARNING and a skip line and shows nothing.
/// </summary>
internal static class SelfDisableNotice
{
    internal const string CheckId = "taom.patch-notice.self-disable";

    internal enum ChatGate { Hold, Show, Off }

    // Main thread, from SubModule.OnApplicationTick: one int read and one count read while nothing is waiting.
    internal static void Tick()
    {
        var ledger = SelfDisables.Ledger;
        if (!ledger.HasPending && ledger.Unshown.Count == 0) return;
        try
        {
            var server = ClientComposition.IsDedicatedServer();
            if (ledger.HasPending) Record(ledger, server, IoC.Resolve<IModLogger>(), IoC.Resolve<IOutcomeLog>());
            if (server || ledger.ShowFailed || ledger.Unshown.Count == 0) return;
            var gate = ClientGate();
            if (gate == ChatGate.Hold) return;
            Show(ledger, gate, IoC.Resolve<IOutcomeLog>(), ShowChatLine);
        }
        catch (Exception ex)
        {
            Outcomes.Swallowed("PatchNotice", "SelfDisableNotice.Tick", ex);
        }
    }

    // The engine's chat filter (v1.5.7 MPChatVM.IsChatAllowedByOptions) drops a line, and keeps no copy of it, when
    // the single-player chat box is off, or in a mission with Hide Battle UI on. The line also waits for a game
    // (no line survives the main menu), for the loading window to close, and for the map or a mission: on character
    // creation or an intro video it would fade before the player can read it.
    internal static ChatGate Gate(bool gameRunning, bool loading, bool onMapOrInMission, bool inMission, bool hideBattleUi, bool chatBoxOn)
    {
        if (!gameRunning || loading || !onMapOrInMission) return ChatGate.Hold;
        if (!chatBoxOn) return ChatGate.Off;
        return inMission && hideBattleUi ? ChatGate.Hold : ChatGate.Show;
    }

    internal static void Record(SelfDisableLedger ledger, bool server, IModLogger logger, IOutcomeLog outcomes)
    {
        var taken = ledger.Take();
        if (taken.Count == 0) return;
        foreach (var entry in taken)
        {
            try { logger.LogWarning("[SelfDisable] " + entry.Id + " is off: " + entry.Reason); }
            catch { /* swallow-ok: a log that fails must not cost the chat line or the [Check] line */ }
        }
        if (server)
        {
            foreach (var entry in taken)
                outcomes.Skip(CheckId, entry.Id, "feature=" + entry.Id + " reason=dedicated-server");
            return;
        }
        ledger.Unshown.AddRange(taken);
        ledger.ShowFailed = false; // a new entry earns the chat line another attempt
    }

    internal static void Show(SelfDisableLedger ledger, ChatGate gate, IOutcomeLog outcomes, Action<string> show)
    {
        if (gate == ChatGate.Hold || ledger.Unshown.Count == 0) return;
        var entries = ledger.Unshown.ToArray();
        if (gate == ChatGate.Off)
        {
            ledger.Unshown.Clear();
            foreach (var entry in entries)
                outcomes.Skip(CheckId, entry.Id, "feature=" + entry.Id + " reason=chat-off");
            return;
        }
        // A line that throws leaves the entries waiting and Tick records the throw; the next new entry tries again.
        ledger.ShowFailed = true;
        show(string.Join(", ", entries.Select(e => e.Id)));
        ledger.ShowFailed = false;
        ledger.Unshown.Clear();
        foreach (var entry in entries)
            outcomes.Pass(CheckId, entry.Id, "feature=" + entry.Id + " shown=chat");
    }

    // MapState is a CampaignSystem type: a separate method, so a server, which returns before it, never compiles it.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ChatGate ClientGate()
    {
        var state = GameStateManager.Current?.ActiveState;
        var inMission = state is MissionState;
        return Gate(Game.Current != null, LoadingWindow.IsLoadingWindowActive, inMission || state is MapState, inMission,
            BannerlordConfig.HideBattleUI, BannerlordConfig.EnableSingleplayerChatBox);
    }

    private static void ShowChatLine(string ids) =>
        InformationManager.DisplayMessage(new InformationMessage(
            new TextObject("{=taom_patch_self_disabled}TAOM: some fixes turned themselves off: {FEATURES}. They stay off for the rest of this battle or this session; the TAOM log names the cause.")
                .SetTextVariable("FEATURES", ids).ToString(),
            Colors.Red));
}
