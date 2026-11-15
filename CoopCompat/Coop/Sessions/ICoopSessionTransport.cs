using System;
using System.Collections.Generic;
using TaleWorlds.MountAndBlade;
using TAOM.CoopCompat.Messages;

namespace TAOM.CoopCompat.Sessions;

/// <summary>
/// Coop's campaign-free session API (#1280) as TAOM.CoopCompat sees it. No signature names a Coop type, so the link that holds it loads
/// without Coop (SubModuleShapeTests). Every event is raised on the game thread.
/// </summary>
public interface ICoopSessionTransport
{
    /// <summary>This member joined a session and must open its mission: session id, mod id, payload.</summary>
    event Action<string, string, byte[]>? MissionOpenRequested;

    /// <summary>The host was elected or changed: session id, epoch, whether this member is the host.</summary>
    event Action<string, int, bool>? HostChanged;

    /// <summary>The session ended: session id.</summary>
    event Action<string>? MissionEndRequested;

    event Action<CoopCompatLdRunEnded>? RunEndedReceived;

    void Host(string modId, byte[] payload);

    void Join(string address, int port);

    IReadOnlyList<MissionBehavior> CreateMissionBehaviors(string sessionId);

    bool Close();

    /// <summary>False when no session mission is live.</summary>
    bool SendToSession(CoopCompatLdRunEnded message);
}
