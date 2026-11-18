using System;
using Common.Messaging;
using ProtoBuf;

namespace TAOM.CoopCompat.Messages;

/// <summary>
/// Server to clients: a hero's career as the server holds it (career sync, #967), to the other players after an
/// accepted report, or to the sender after a refusal or a join report that differs. <see cref="Sequence"/> is the
/// last report the server handled for that hero. <see cref="Known"/> is false when the server holds no entry for the
/// hero; <see cref="Refused"/> marks an answer to a refused report. Wire contract: never rename or renumber.
/// </summary>
[ProtoContract]
public sealed class CoopCompatCareerUpdate : IEvent
{
    [ProtoMember(1)] public int ProtocolVersion { get; set; }
    [ProtoMember(2)] public long Sequence { get; set; }
    [ProtoMember(3)] public string HeroId { get; set; } = "";
    [ProtoMember(4)] public string CareerId { get; set; } = "";
    [ProtoMember(5)] public string[] Choices { get; set; } = Array.Empty<string>();
    [ProtoMember(6)] public int[] Tiers { get; set; } = Array.Empty<int>();
    [ProtoMember(7)] public string[] Flags { get; set; } = Array.Empty<string>();
    [ProtoMember(8)] public bool Refused { get; set; }
    [ProtoMember(9)] public bool Known { get; set; }
}
