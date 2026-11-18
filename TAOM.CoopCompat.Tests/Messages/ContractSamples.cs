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
        ModuleOrder = new[] { "TAOM_Map", "TAOM" },
    };

    public static CoopCompatHelloReply Reply() => new CoopCompatHelloReply
    {
        ProtocolVersion = 1,
        Verdict = 2,
        DivergentGroups = new[] { "Battle Tactics" },
        BuildMismatch = true,
        LoadOrderDiffers = true,
        LoadOrderHostFirst = "TAOM Map (TAOM_Map)",
        LoadOrderHostSecond = "TAOM",
    };

    public static CoopCompatMomentumSnapshot Snapshot() => new CoopCompatMomentumSnapshot
    {
        ProtocolVersion = 1,
        Sequence = 9_000_000_000L,
        Keys = new[] { "a", "b" },
        Values = new[] { "1", "2" },
    };

    public static CoopCompatLdRunEnded LdRunEnded() => new CoopCompatLdRunEnded
    {
        ProtocolVersion = 1,
        SessionId = "session-1",
        Epoch = 3,
        Reason = 2,
        Wave = 7,
        WavesCleared = 6,
        Kills = 412,
    };

    public static CoopCompatTradeRequest TradeRequest() => new CoopCompatTradeRequest
    {
        ProtocolVersion = 1,
        RequestId = "abc",
        Action = 4,
        SettlementId = "town_A1",
        Subject = "mail",
        Modifier = "fine",
        Target = "plate",
        Quantity = 3,
    };

    public static CoopCompatTradeReply TradeReply() => new CoopCompatTradeReply
    {
        ProtocolVersion = 1,
        RequestId = "abc",
        Action = 2,
        Refusal = 0,
        Status = 6,
        Quantity = 5,
        TotalCost = 50,
        ResourceName = "Horses",
        ResourceId = "horses",
        HasBalance = true,
        Balance = 12.5f,
        HasArmouryLevel = true,
        ArmouryLevel = 2,
    };

    public static IMessage[] All() => new IMessage[] { Hello(), Reply(), Snapshot(), LdRunEnded(), TradeRequest(), TradeReply() };
}
