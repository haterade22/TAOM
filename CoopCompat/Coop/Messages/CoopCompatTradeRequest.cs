using Common.Messaging;
using ProtoBuf;

namespace TAOM.CoopCompat.Messages;

/// <summary>
/// Client to server: a guest asks the host to run an emissary or armoury action for the guest's hero (#1329). The
/// server names the hero from the sender's connection, never from this message. Wire contract: never rename or renumber.
/// </summary>
[ProtoContract]
public sealed class CoopCompatTradeRequest : ICommand
{
    [ProtoMember(1)] public int ProtocolVersion { get; set; }
    [ProtoMember(2)] public string RequestId { get; set; } = "";
    [ProtoMember(3)] public int Action { get; set; }
    [ProtoMember(4)] public string SettlementId { get; set; } = "";
    [ProtoMember(5)] public string Subject { get; set; } = "";
    [ProtoMember(6)] public string Modifier { get; set; } = "";
    [ProtoMember(7)] public string Target { get; set; } = "";
    [ProtoMember(8)] public int Quantity { get; set; }
}
