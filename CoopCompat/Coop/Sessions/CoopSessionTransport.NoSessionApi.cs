using System;
using System.Collections.Generic;
using TaleWorlds.MountAndBlade;
using TAOM.CoopCompat.Messages;

namespace TAOM.CoopCompat.Sessions;

/// <summary>
/// Built in place of CoopSessionTransport.cs when the Coop at CoopBinDir has no session API (no Missions.Sessions.dll, before
/// BannerlordCoop #4694), so TAOM.CoopCompat still builds against an older Coop. Start throws, and CoopCompatSubModule then logs
/// "sessions off" and leaves the co-op Long Defeat option hidden. The project file picks one of the two files.
/// </summary>
public sealed class CoopSessionTransport : ICoopSessionTransport
{
    public event Action<string, string, byte[]>? MissionOpenRequested { add { } remove { } }

    public event Action<string, int, bool>? HostChanged { add { } remove { } }

    public event Action<string>? MissionEndRequested { add { } remove { } }

    public event Action<CoopCompatLdRunEnded>? RunEndedReceived { add { } remove { } }

    public void Start() => throw new NotSupportedException("this Coop build has no session API (BannerlordCoop #4694)");

    public void Host(string modId, byte[] payload)
    {
    }

    public void Join(string address, int port)
    {
    }

    public IReadOnlyList<MissionBehavior> CreateMissionBehaviors(string sessionId) => Array.Empty<MissionBehavior>();

    public bool Close() => false;

    public bool SendToSession(CoopCompatLdRunEnded message) => false;
}
