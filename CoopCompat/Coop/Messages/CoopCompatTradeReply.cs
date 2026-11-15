using Common.Messaging;
using ProtoBuf;

namespace TAOM.CoopCompat.Messages;

/// <summary>Server to the one client that sent a <see cref="CoopCompatTradeRequest"/> (#1329). Wire contract: never rename or renumber.</summary>
[ProtoContract]
public sealed class CoopCompatTradeReply : IEvent
{
    [ProtoMember(1)] public int ProtocolVersion { get; set; }
    [ProtoMember(2)] public string RequestId { get; set; } = "";
    [ProtoMember(3)] public int Action { get; set; }
    [ProtoMember(4)] public int Refusal { get; set; }
    [ProtoMember(5)] public int Status { get; set; }
    [ProtoMember(6)] public int Quantity { get; set; }
    [ProtoMember(7)] public int TotalCost { get; set; }
    [ProtoMember(8)] public string ResourceName { get; set; } = "";
    [ProtoMember(9)] public string ResourceId { get; set; } = "";
    [ProtoMember(10)] public bool HasBalance { get; set; }
    [ProtoMember(11)] public float Balance { get; set; }
    [ProtoMember(12)] public bool HasArmouryLevel { get; set; }
    [ProtoMember(13)] public int ArmouryLevel { get; set; }
}
