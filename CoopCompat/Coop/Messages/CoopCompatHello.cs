using System;
using Common.Messaging;
using ProtoBuf;

namespace TAOM.CoopCompat.Messages;

/// <summary>
/// The one place the wire protocol version lives. Every contract carries it as field 1, and a peer
/// drops a message whose version differs from its own.
/// </summary>
public static class CoopCompatProtocol
{
    /// <summary>
    /// 2: every server-to-client message carries the client's session token. A build that speaks 1 neither sends nor
    /// checks it, so the two drop each other's requests instead of acting on them with no answer.
    /// </summary>
    public const int ProtocolVersion = 2;
}

/// <summary>
/// Client to server, sent when the client's campaign join completes. The full type name is the wire
/// id and the field numbers are the layout: never rename, move or renumber (WireContractNamesTests).
/// </summary>
[ProtoContract]
public sealed class CoopCompatHello : ICommand
{
    [ProtoMember(1)] public int ProtocolVersion { get; set; }
    [ProtoMember(2)] public string TaomVersion { get; set; } = "";
    [ProtoMember(3)] public string TaomMvid { get; set; } = "";
    [ProtoMember(4)] public string CoopCompatMvid { get; set; } = "";
    [ProtoMember(5)] public string SettingsGlobal { get; set; } = "";
    [ProtoMember(6)] public string[] GroupNames { get; set; } = Array.Empty<string>();
    [ProtoMember(7)] public string[] GroupHashes { get; set; } = Array.Empty<string>();
    [ProtoMember(8)] public bool Conclusive { get; set; }

    /// <summary>The client's active module ids in load order (at most 256). Empty from a peer built before it existed.</summary>
    [ProtoMember(9)] public string[] ModuleOrder { get; set; } = Array.Empty<string>();
    /// <summary>A random token for this join, which the server echoes in every message to this client. Empty from a peer built before it existed.</summary>
    [ProtoMember(10)] public string SessionToken { get; set; } = "";
}
