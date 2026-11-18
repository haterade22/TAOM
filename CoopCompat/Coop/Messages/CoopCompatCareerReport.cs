using System;
using Common.Messaging;
using ProtoBuf;

namespace TAOM.CoopCompat.Messages;

/// <summary>
/// Client to server: the career of the hero this client plays (career sync, #967). The server names the hero from the
/// sender's connection and only compares <see cref="HeroId"/> with it. <see cref="Sequence"/> grows with every report
/// of that client and comes back in the server's answer. <see cref="JoinReport"/> marks the first report after a
/// campaign join. Wire contract: never rename or renumber.
/// </summary>
[ProtoContract]
public sealed class CoopCompatCareerReport : ICommand
{
    [ProtoMember(1)] public int ProtocolVersion { get; set; }
    [ProtoMember(2)] public long Sequence { get; set; }
    [ProtoMember(3)] public bool JoinReport { get; set; }
    [ProtoMember(4)] public string HeroId { get; set; } = "";
    [ProtoMember(5)] public string CareerId { get; set; } = "";
    [ProtoMember(6)] public string[] Choices { get; set; } = Array.Empty<string>();
    [ProtoMember(7)] public int[] Tiers { get; set; } = Array.Empty<int>();
    [ProtoMember(8)] public string[] Flags { get; set; } = Array.Empty<string>();
}
