using Common.Messaging;
using ProtoBuf;

namespace TAOM.CoopCompat.Messages;

/// <summary>
/// Host to every member of a Long Defeat co-op session (#1280), over Coop's session mod channel: how the host's run ended. Channel messages
/// carry no host epoch, so the epoch and the session id are fields of their own. The full type name is the wire id and the field
/// numbers are the layout: never rename, move or renumber (WireContractNamesTests).
/// </summary>
[ProtoContract]
public sealed class CoopCompatLdRunEnded : IEvent
{
    [ProtoMember(1)] public int ProtocolVersion { get; set; }
    [ProtoMember(2)] public string SessionId { get; set; } = "";
    [ProtoMember(3)] public int Epoch { get; set; }
    [ProtoMember(4)] public int Reason { get; set; }
    [ProtoMember(5)] public int Wave { get; set; }
    [ProtoMember(6)] public int WavesCleared { get; set; }
    [ProtoMember(7)] public int Kills { get; set; }
}
