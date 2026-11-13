using System;
using Common.Messaging;
using ProtoBuf;

namespace TAOM.CoopCompat.Messages;

/// <summary>Server to clients: the war half of the momentum store. Wire contract: never rename or renumber.</summary>
[ProtoContract]
public sealed class CoopCompatMomentumSnapshot : IEvent
{
    [ProtoMember(1)] public int ProtocolVersion { get; set; }
    [ProtoMember(2)] public long Sequence { get; set; }
    [ProtoMember(4)] public string[] Keys { get; set; } = Array.Empty<string>();
    [ProtoMember(5)] public string[] Values { get; set; } = Array.Empty<string>();
}
