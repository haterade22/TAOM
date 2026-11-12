using System;
using Common.Messaging;
using ProtoBuf;

namespace TAOM.CoopCompat.Messages;

/// <summary>Server to the one client that sent a <see cref="CoopCompatHello"/>. Wire contract: never rename or renumber.</summary>
[ProtoContract]
public sealed class CoopCompatHelloReply : IEvent
{
    [ProtoMember(1)] public int ProtocolVersion { get; set; }
    [ProtoMember(2)] public int Verdict { get; set; }
    [ProtoMember(3)] public string[] DivergentGroups { get; set; } = Array.Empty<string>();
    [ProtoMember(4)] public bool BuildMismatch { get; set; }
}
