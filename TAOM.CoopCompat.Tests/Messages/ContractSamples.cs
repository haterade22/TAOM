using Common.Messaging;
using TAOM.CoopCompat.Messages;

namespace TAOM.CoopCompat.Tests.Messages;

internal static class ContractSamples
{
    public static CoopCompatHello Hello() => new CoopCompatHello
    {
        ProtocolVersion = 1,
        TaomVersion = "v2.0.38",
        TaomMvid = "11111111-2222-3333-4444-555555555555",
        CoopCompatMvid = "66666666-7777-8888-9999-000000000000",
        SettingsGlobal = "abc123",
        GroupNames = new[] { "Battle Tactics", "Momentum" },
        GroupHashes = new[] { "h1", "h2" },
        Conclusive = true,
    };

    public static CoopCompatHelloReply Reply() => new CoopCompatHelloReply
    {
        ProtocolVersion = 1,
        Verdict = 2,
        DivergentGroups = new[] { "Battle Tactics" },
        BuildMismatch = true,
    };

    public static CoopCompatMomentumSnapshot Snapshot() => new CoopCompatMomentumSnapshot
    {
        ProtocolVersion = 1,
        Sequence = 9_000_000_000L,
        Keys = new[] { "a", "b" },
        Values = new[] { "1", "2" },
    };

    public static IMessage[] All() => new IMessage[] { Hello(), Reply(), Snapshot() };
}
