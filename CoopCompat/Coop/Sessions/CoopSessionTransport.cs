using System;
using System.Collections.Generic;
using System.Net;
using Common;
using Common.Messaging;
using Common.Network.Session;
using GameInterface.Services.Sessions;
using GameInterface.Services.Sessions.Messages;
using TaleWorlds.MountAndBlade;
using TAOM.CoopCompat.Messages;

namespace TAOM.CoopCompat.Sessions;

/// <summary>
/// The only class that touches Coop's session API (#1280). Coop's message broker holds subscriptions weakly, so the launcher and this
/// object live in fields for the whole process. Coop raises its events on its own threads, so each one is copied and run on the game
/// thread, as <c>CoopTransport</c> does.
/// </summary>
public sealed class CoopSessionTransport : ICoopSessionTransport
{
    private readonly Action<Action> _runOnGameThread;
    private SessionLauncher? _launcher;

    public CoopSessionTransport() : this(action => GameThread.RunSafe(action, context: "TAOM.CoopCompat.Sessions"))
    {
    }

    /// <summary>Tests replace the game-thread marshal, which needs a running game loop.</summary>
    internal CoopSessionTransport(Action<Action> runOnGameThread)
    {
        _runOnGameThread = runOnGameThread;
    }

    public event Action<string, string, byte[]>? MissionOpenRequested;

    public event Action<string, int, bool>? HostChanged;

    public event Action<string>? MissionEndRequested;

    public event Action<CoopCompatLdRunEnded>? RunEndedReceived;

    public void Start()
    {
        if (_launcher != null) return;

        _launcher = new SessionLauncher(OnOpen);
        _launcher.HostChanged += OnHostChanged;
        _launcher.MissionEndRequested += OnEnd;
        MessageBroker.Instance.Subscribe<CoopCompatLdRunEnded>(OnRunEnded);
    }

    public void Host(string modId, byte[] payload) => _launcher?.Host(modId, payload, "", ServerVisibility.FriendsOnly);

    public void Join(string address, int port) => _launcher?.Join(IPAddress.Parse(address), port, "");

    public IReadOnlyList<MissionBehavior> CreateMissionBehaviors(string sessionId) =>
        _launcher?.CreateMissionBehaviors(sessionId) ?? Array.Empty<MissionBehavior>();

    public bool Close() => _launcher?.Close() ?? false;

    public bool SendToSession(CoopCompatLdRunEnded message) => _launcher?.Channel.SendToSession(message) ?? false;

    private void OnOpen(SessionMissionOpenRequested request)
    {
        var (sessionId, modId, payload) = (request.SessionId, request.ModId, request.Payload);
        _runOnGameThread(() => MissionOpenRequested?.Invoke(sessionId, modId, payload));
    }

    private void OnHostChanged(SessionHostChanged changed)
    {
        var (sessionId, epoch, isLocalHost) = (changed.SessionId, changed.Epoch, changed.IsLocalHost);
        _runOnGameThread(() => HostChanged?.Invoke(sessionId, epoch, isLocalHost));
    }

    private void OnEnd(SessionMissionEndRequested ended)
    {
        var sessionId = ended.SessionId;
        _runOnGameThread(() => MissionEndRequested?.Invoke(sessionId));
    }

    private void OnRunEnded(MessagePayload<CoopCompatLdRunEnded> payload)
    {
        var message = payload.What;
        _runOnGameThread(() => RunEndedReceived?.Invoke(message));
    }
}
