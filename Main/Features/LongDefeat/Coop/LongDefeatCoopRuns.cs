using System;
using System.Collections.Generic;
using System.Globalization;
using CultureInfo = System.Globalization.CultureInfo;
using System.Net;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Features.LongDefeat.Coop;

/// <summary>
/// A Long Defeat run played as a BannerlordCoop session (#1280). The host sends the setup as the session payload; every member opens
/// the same run from it. Coop elects the host when the missions load, and only the host runs the waves. Slice 1 has no handoff: when
/// a follower is made host, the run ends for everyone. Every call comes on the game thread and none throws.
/// </summary>
internal sealed class LongDefeatCoopRuns : ILongDefeatCoopPort
{
    internal const string NoCoopMessage = "{=TAOM_LD_Coop_NoCoop}Co-op needs the Coop mod loaded.";
    internal const string BadPayloadMessage = "{=TAOM_LD_Coop_BadPayload}The host's run could not be read. Install the same TAOM as the host.";
    internal const string BadAddressMessage = "{=TAOM_LD_Coop_BadAddress}Enter the host's address as address:port, for example 192.168.1.20:4200.";
    internal const string HostLeftMessage = "{=TAOM_LD_Coop_HostLeft}The host left, so the run ended.";
    internal const string RunEndedMessage = "{=TAOM_LD_Coop_RunEnded}The run ended at wave {WAVE}. Waves cleared: {WAVES}. Kills: {KILLS}.";

    private readonly LongDefeatCoopState _state;
    private readonly Func<LongDefeatSetup, bool> _start;
    private readonly ILongDefeatMenuAdapter _menu;
    private readonly IModLogger _logger;

    /// <param name="start">The prompt-free start path (<see cref="LongDefeatSetupPrompt.Start"/>), so a member's run loads like a solo run.</param>
    public LongDefeatCoopRuns(LongDefeatCoopState state, Func<LongDefeatSetup, bool> start, ILongDefeatMenuAdapter menu, IModLogger logger)
    {
        _state = state;
        _start = start;
        _menu = menu;
        _logger = logger;
    }

    /// <summary>The session state these runs write, for the mission launcher.</summary>
    internal LongDefeatCoopState State => _state;

    public void Attach(ILongDefeatCoopLink link)
    {
        _state.Link = link;
        _logger.LogInfo("[LongDefeat][Coop] session link attached");
    }

    public void Host(LongDefeatSetup setup) => Guarded("Host", () =>
    {
        if (_state.Link == null)
        {
            _menu.Notify(NoCoopMessage);
            return;
        }

        _logger.LogInfo($"[LongDefeat][Coop] hosting fortress {setup.FortressId}");
        _state.Link.Host(LongDefeatSessionPayload.Encode(setup.FortressId, setup.Options));
    });

    public void Join(string addressText) => Guarded("Join", () =>
    {
        if (_state.Link == null)
        {
            _menu.Notify(NoCoopMessage);
            return;
        }

        if (!TryParseAddress(addressText, out var address, out var port))
        {
            _menu.Notify(BadAddressMessage);
            return;
        }

        _logger.LogInfo($"[LongDefeat][Coop] joining {address}:{port}");
        _state.Link.Join(address, port);
    });

    /// <summary>The run's result, sent by the host so every member sees how it ended. The run host sends it once (LongDefeatRunHost.ReportEnd).</summary>
    public void ReportEnded(string sessionId, RunResult result) => Guarded("ReportEnded", () =>
    {
        if (_state.RoleFor(sessionId) != LongDefeatCoopRole.Host)
            return;

        _state.Link?.SendRunEnded(sessionId, _state.Epoch, (int)result.Reason, result.Wave, result.WavesCleared, result.Kills);
    });

    /// <summary>
    /// The run's mission ends, by any exit. The host closes the session, while its mission is still current (Coop's close goes through
    /// it), and the other members end on Coop's end event. A follower's own Coop controller leaves the session for it.
    /// </summary>
    public void MissionEnded(string sessionId) => Guarded("MissionEnded", () =>
    {
        var role = _state.RoleFor(sessionId);
        if (role == LongDefeatCoopRole.None)
            return;

        if (role == LongDefeatCoopRole.Host)
            _state.Link?.Close();
        _state.Exit();
    });

    public void OnMissionOpenRequested(string sessionId, byte[] payload) => Guarded("OnMissionOpenRequested", () =>
    {
        if (!LongDefeatSessionPayload.TryDecode(payload, out var fortressId, out var options))
        {
            _logger.LogWarning($"[LongDefeat][Coop] session {sessionId}: the payload could not be read ({payload?.Length ?? 0} bytes)");
            _menu.Notify(BadPayloadMessage);
            return;
        }

        _state.Enter(sessionId);
        _logger.LogInfo($"[LongDefeat][Coop] session {sessionId}: opening fortress {fortressId}");
        if (!_start(new LongDefeatSetup(fortressId, "coop", options!, SessionId: sessionId)))
            _state.Exit();
    });

    public void OnHostChanged(string sessionId, int epoch, bool isLocalHost) => Guarded("OnHostChanged", () =>
    {
        var role = _state.RoleFor(sessionId);
        if (role == LongDefeatCoopRole.None || epoch < _state.Epoch)
            return;

        if (role == LongDefeatCoopRole.Follower && isLocalHost)
        {
            _logger.LogInfo($"[LongDefeat][Coop] session {sessionId}: the host left at epoch {epoch}; ending the run");
            _state.Link?.Close();
            _state.Exit();
            _menu.Notify(HostLeftMessage);
            _menu.EndGame();
            return;
        }

        _state.SetRole(epoch, isLocalHost ? LongDefeatCoopRole.Host : LongDefeatCoopRole.Follower);
        _logger.LogInfo($"[LongDefeat][Coop] session {sessionId}: epoch {epoch}, this member is {(isLocalHost ? "the host" : "a follower")}");
    });

    public void OnMissionEndRequested(string sessionId) => Guarded("OnMissionEndRequested", () =>
    {
        if (_state.RoleFor(sessionId) == LongDefeatCoopRole.None)
            return;

        _state.Exit();
        _menu.EndGame();
    });

    public void OnRunEnded(string sessionId, int epoch, int reason, int wave, int wavesCleared, int kills) => Guarded("OnRunEnded", () =>
    {
        if (_state.RoleFor(sessionId) != LongDefeatCoopRole.Follower || epoch < _state.Epoch)
            return;

        _logger.LogInfo($"[LongDefeat][Coop] session {sessionId}: the host's run ended ({(RunEndReason)reason}, wave {wave})");
        _menu.Notify(RunEndedMessage, new Dictionary<string, string>
        {
            ["WAVE"] = wave.ToString(CultureInfo.InvariantCulture),
            ["WAVES"] = wavesCleared.ToString(CultureInfo.InvariantCulture),
            ["KILLS"] = kills.ToString(CultureInfo.InvariantCulture),
        });
    });

    internal static bool TryParseAddress(string? text, out string address, out int port)
    {
        address = "";
        port = 0;
        var trimmed = text?.Trim() ?? "";
        var colon = trimmed.LastIndexOf(':');
        if (colon <= 0 || !int.TryParse(trimmed.Substring(colon + 1), NumberStyles.None, CultureInfo.InvariantCulture, out port)
            || port < 1 || port > 65535 || !IPAddress.TryParse(trimmed.Substring(0, colon), out _))
            return false;

        address = trimmed.Substring(0, colon);
        return true;
    }

    private void Guarded(string where, Action step)
    {
        try
        {
            step();
        }
        catch (Exception ex)
        {
            _logger.LogError($"[LongDefeat][Coop] {where} failed: {ex}");
        }
    }
}
