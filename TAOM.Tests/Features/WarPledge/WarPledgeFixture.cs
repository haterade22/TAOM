using System;
using System.Collections.Generic;
using System.Linq;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Diplomacy;
using TAOM.Features.Diplomacy.Models;
using TAOM.Features.Execution;
using TAOM.Features.WarChronicle.Effects;
using TAOM.Features.WarChronicle.Ledger;
using TAOM.Features.WarChronicle.Rally;
using TAOM.Features.WarOfTheRingMomentum;
using TAOM.Features.WarPledge;

namespace TAOM.Tests.Features.WarPledge;

/// <summary>
/// The world of the War Pledge tests (#1113). The alignment, diplomacy and War of the Ring services are the
/// real ones over one shared store: the mechanism under test is that a stored pledge changes the side and the
/// tier those services give, and that the existing Full War code then declares and locks the wars. Only the
/// engine boundary is a fake: <see cref="FakeAllianceAdapter"/> keeps the wars and records every call in order.
/// </summary>
internal sealed class WarPledgeFixture
{
    public readonly WarPledgeStore Store = new WarPledgeStore();
    public readonly FakeAllianceAdapter Alliance = new FakeAllianceAdapter();
    public readonly IWarPledgeSettings Settings = Substitute.For<IWarPledgeSettings>();
    public readonly IMomentumSettingsProvider MomentumSettings = Substitute.For<IMomentumSettingsProvider>();
    public readonly IMomentumQueryService Momentum = Substitute.For<IMomentumQueryService>();
    public readonly ITaomSettingsProvider WotrSettings = Substitute.For<ITaomSettingsProvider>();
    public readonly IWarPledgeWorldAdapter World = Substitute.For<IWarPledgeWorldAdapter>();
    public readonly IOutcomeLog Outcomes = Substitute.For<IOutcomeLog>();
    public readonly IModLogger Logger = Substitute.For<IModLogger>();
    public readonly IModLogger LedgerLogger = Substitute.For<IModLogger>();

    public readonly Dictionary<string, string> Alignments = new Dictionary<string, string>
    {
        { "empire_w", "free" }, { "vlandia", "free" }, { "lindon", "free" },
        { "empire_s", "evil" }, { "isengard", "evil" },
        { "cardolan", "neutral" }, { "dorwinion", "neutral" }, { "rhudaur", "neutral" },
        { "umbar", "neutral" }, { "anduinvale", "neutral" },
    };

    public readonly List<KingdomRelationship> Relationships = new List<KingdomRelationship>
    {
        new() { KingdomA = "empire_w", KingdomB = "empire_s", Tier = AllianceTier.Hostile },
        new() { KingdomA = "empire_w", KingdomB = "isengard", Tier = AllianceTier.Hostile },
        new() { KingdomA = "vlandia", KingdomB = "empire_s", Tier = AllianceTier.Hostile },
        new() { KingdomA = "vlandia", KingdomB = "isengard", Tier = AllianceTier.Hostile },
        new() { KingdomA = "empire_w", KingdomB = "umbar", Tier = AllianceTier.Hostile },
        new() { KingdomA = "empire_s", KingdomB = "umbar", Tier = AllianceTier.Natural },
    };

    public List<WarPledgeLean> Leans = new List<WarPledgeLean>
    {
        new("rhudaur", FactionSide.Evil),
        new("umbar", FactionSide.Evil),
        new("cardolan", FactionSide.Free),
        new("dorwinion", FactionSide.Free),
        new("anduinvale", FactionSide.Neutral),
    };

    /// <summary>blockPeaceBetweenHostileTiers of war_of_the_ring.json: off, the War of the Ring holds no pair at war.</summary>
    public bool BlockPeace = true;

    public int LosingBalance = 25;
    public int DaysBetweenAiJoins = 30;

    public AlignmentService Alignment { get; private set; } = null!;
    public DiplomacyService Diplomacy { get; private set; } = null!;
    public WarOfTheRingService Wotr { get; private set; } = null!;

    public WarPledgeFixture()
    {
        Alliance.Kingdoms.AddRange(Alignments.Keys);
        Settings.Enabled.Returns(true);
        MomentumSettings.MomentumEnabled.Returns(true);
        WotrSettings.IsAvailable.Returns(true);
        WotrSettings.WarOfTheRingEnabled.Returns(true);
    }

    /// <summary>Builds the services from the tables above, in Full War unless <paramref name="phase"/> says otherwise.</summary>
    public WarPledgeService Build(WarPhase phase = WarPhase.FullWar)
    {
        var alignmentConfig = Substitute.For<IAlignmentConfigProvider>();
        alignmentConfig.LoadAlignments().Returns(Alignments);
        Alignment = new AlignmentService(alignmentConfig, Substitute.For<IModLogger>(), Store);

        var diplomacyConfig = Substitute.For<IDiplomacyConfigProvider>();
        diplomacyConfig.LoadConfig().Returns(new DiplomacyConfig { Relationships = Relationships });
        Diplomacy = new DiplomacyService(diplomacyConfig, Alliance, Alignment, Substitute.For<IModLogger>(), Store);

        var wotrConfig = Substitute.For<IWarOfTheRingConfigProvider>();
        wotrConfig.LoadConfig().Returns(new WarOfTheRingConfig
        {
            Phase2 = new PhaseConfig { TriggerDay = 44, AutoWarBetweenHostileTiers = true, BlockPeaceBetweenHostileTiers = BlockPeace },
        });
        Wotr = new WarOfTheRingService(wotrConfig, Diplomacy, Alliance, WotrSettings, Substitute.For<IModLogger>());
        Wotr.SetPhaseFromSave(phase);
        // The engine's IsAtWarWith asks the diplomacy model first, and TaomDiplomacyModel.IsAtConstantWar is
        // ShouldBlockPeace: a pair the War of the Ring holds at war reads "at war" before any declaration.
        Alliance.ModelWar = Wotr.ShouldBlockPeace;

        var snapshots = Substitute.For<IKingdomWarSnapshotAdapter>();
        snapshots.GetCampaignId().Returns("c1");
        snapshots.GetElapsedDay().Returns(80);
        var ledger = new WarLedgerService(
            snapshots, new WarBaselineService(), new RallyTierStore(), Substitute.For<IWarEffectService>(), Wotr, Alignment, LedgerLogger);

        var config = Substitute.For<IWarPledgeConfigProvider>();
        config.LoadConfig().Returns(new WarPledgeConfig(LosingBalance, DaysBetweenAiJoins, Leans));

        return new WarPledgeService(
            Store, config, Settings, MomentumSettings, Momentum, Alignment, Diplomacy, Wotr, Alliance, World, ledger, Outcomes, Logger);
    }

    /// <summary>The wars the Full War script declares for the native members, as a campaign in Full War holds them.</summary>
    public void DeclareNativeWars()
    {
        foreach (var row in Relationships.Where(r => r.Tier == AllianceTier.Hostile))
            Alliance.Wars.Add(FakeAllianceAdapter.Key(row.KingdomA, row.KingdomB));
    }
}

/// <summary>
/// The engine side of diplomacy as far as these tests need it: the live kingdoms, the declared wars, and the
/// order of every write. As in the engine, <c>AreAtWar</c> is true for a declared war and for a pair the model
/// holds at war (<see cref="ModelWar"/>), while <c>HasDeclaredWar</c> reads the stored stance alone, so a test
/// can tell the two apart.
/// </summary>
internal sealed class FakeAllianceAdapter : IAllianceAdapter
{
    public readonly List<string> Kingdoms = new List<string>();
    public readonly HashSet<string> Wars = new HashSet<string>(StringComparer.Ordinal);
    public readonly List<string> Calls = new List<string>();

    /// <summary>The culture of a kingdom whose culture id is not its own id (a player-founded realm).</summary>
    public readonly Dictionary<string, string> Cultures = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Per declared pair, the kingdom that declared: vanilla charges a broken alliance to that one.</summary>
    public readonly Dictionary<string, string> Declarers = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Thrown by the next MakePeace, as a campaign listener that throws inside the action would.</summary>
    public Exception? PeaceThrows;

    /// <summary>A pair key the engine refuses to declare war for (its own veto, an unknown kingdom).</summary>
    public string? RefusedWar;

    /// <summary>A pair key whose peace the engine refuses.</summary>
    public string? RefusedPeace;

    public static string Key(string a, string b) => string.CompareOrdinal(a, b) <= 0 ? a + "|" + b : b + "|" + a;

    public IReadOnlyList<string> GetAllKingdomIds() => Kingdoms.ToList();

    public string GetKingdomCultureId(string kingdomId) => Cultures.TryGetValue(kingdomId, out var culture) ? culture : kingdomId;

    public string GetPlayerKingdomId() => null!;

    public bool AreAllied(string kingdomAId, string kingdomBId) => false;

    public void StartAlliance(string kingdomAId, string kingdomBId) { }

    /// <summary>The diplomacy model's constant war (Kingdom.IsAtWarWith asks it before the stance); null means none.</summary>
    public Func<string, string, bool>? ModelWar;

    public bool AreAtWar(string kingdomAId, string kingdomBId) =>
        Wars.Contains(Key(kingdomAId, kingdomBId)) || (ModelWar != null && ModelWar(kingdomAId, kingdomBId));

    public bool HasDeclaredWar(string kingdomAId, string kingdomBId) => Wars.Contains(Key(kingdomAId, kingdomBId));

    public void DeclareWar(string kingdomAId, string kingdomBId)
    {
        var key = Key(kingdomAId, kingdomBId);
        Calls.Add("war " + key);
        if (key == RefusedWar)
            return;
        Wars.Add(key);
        Declarers[key] = kingdomAId;
    }

    public void MakePeace(string kingdomAId, string kingdomBId)
    {
        var key = Key(kingdomAId, kingdomBId);
        Calls.Add("peace " + key);
        if (PeaceThrows != null)
            throw PeaceThrows;
        if (key != RefusedPeace)
            Wars.Remove(key);
    }
}
