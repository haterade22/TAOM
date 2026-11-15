using System.Collections.Generic;
using TaleWorlds.MountAndBlade;
using TAOM.CoopCompat.Messages;
using TAOM.Features.LongDefeat.Coop;

namespace TAOM.CoopCompat.Sessions;

/// <summary>
/// Joins TAOM's Long Defeat co-op port (#1280) to Coop's session API. A session opened for another mod id is not ours and is left alone;
/// a run-ended message of another protocol version is dropped. The session and epoch checks are TAOM's (LongDefeatCoopRuns).
/// </summary>
internal sealed class LongDefeatSessionLink : ILongDefeatCoopLink
{
    private readonly ICoopSessionTransport _transport;
    private readonly ILongDefeatCoopPort _port;

    public LongDefeatSessionLink(ICoopSessionTransport transport, ILongDefeatCoopPort port)
    {
        _transport = transport;
        _port = port;
        transport.MissionOpenRequested += OnOpen;
        transport.HostChanged += port.OnHostChanged;
        transport.MissionEndRequested += port.OnMissionEndRequested;
        transport.RunEndedReceived += OnRunEnded;
    }

    public void Host(byte[] payload) => _transport.Host(LongDefeatCoopContract.ModId, payload);

    public void Join(string address, int port) => _transport.Join(address, port);

    public IReadOnlyList<MissionBehavior> CreateMissionBehaviors(string sessionId) => _transport.CreateMissionBehaviors(sessionId);

    public bool Close() => _transport.Close();

    public bool SendRunEnded(string sessionId, int epoch, int reason, int wave, int wavesCleared, int kills) =>
        _transport.SendToSession(new CoopCompatLdRunEnded
        {
            ProtocolVersion = CoopCompatProtocol.ProtocolVersion,
            SessionId = sessionId,
            Epoch = epoch,
            Reason = reason,
            Wave = wave,
            WavesCleared = wavesCleared,
            Kills = kills,
        });

    private void OnOpen(string sessionId, string modId, byte[] payload)
    {
        if (modId == LongDefeatCoopContract.ModId)
            _port.OnMissionOpenRequested(sessionId, payload);
    }

    private void OnRunEnded(CoopCompatLdRunEnded message)
    {
        if (message.ProtocolVersion == CoopCompatProtocol.ProtocolVersion)
            _port.OnRunEnded(message.SessionId, message.Epoch, message.Reason, message.Wave, message.WavesCleared, message.Kills);
    }
}
