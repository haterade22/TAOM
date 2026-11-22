using System;
using Common.Messaging;
using ProtoBuf;

namespace TAOM.CoopCompat.Messages;

/// <summary>
/// Server to the one client that sent a <see cref="CoopCompatTradeRequest"/> (#1329). Wire contract: never rename or renumber.
/// Fields 14 to 22 are the guest hero's rung of the lord's gear ladder, added after v2.0.40 with ProtocolVersion unchanged: a
/// v2.0.40 peer skips them, and a reply from a v2.0.40 server reads as HasLadder false. LadderSlot is the LadderSlot value;
/// LadderMaterialId is "" when the rung waits for no materials.
/// </summary>
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
    [ProtoMember(14)] public bool HasLadder { get; set; }
    [ProtoMember(15)] public int LadderSlot { get; set; }
    [ProtoMember(16)] public bool LadderReady { get; set; }
    [ProtoMember(17)] public bool LadderCanClaim { get; set; }
    [ProtoMember(18)] public int LadderLordLevel { get; set; }
    [ProtoMember(19)] public string LadderMaterialId { get; set; } = "";
    [ProtoMember(20)] public int LadderNeeded { get; set; }
    [ProtoMember(21)] public int LadderCarried { get; set; }
    [ProtoMember(22)] public string[] LadderChoices { get; set; } = Array.Empty<string>();
}
