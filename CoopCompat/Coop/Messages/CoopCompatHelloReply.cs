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

    /// <summary>The compared modules load in another order than on the host. False from a host built before it existed.</summary>
    [ProtoMember(5)] public bool LoadOrderDiffers { get; set; }

    /// <summary>The host's name of the module that loads first on the host and later for this client.</summary>
    [ProtoMember(6)] public string LoadOrderHostFirst { get; set; } = "";

    /// <summary>The host's name of the module that loads second on the host and earlier for this client.</summary>
    [ProtoMember(7)] public string LoadOrderHostSecond { get; set; } = "";
}
