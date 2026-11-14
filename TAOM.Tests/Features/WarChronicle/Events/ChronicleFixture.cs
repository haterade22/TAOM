using System;
using System.Collections.Generic;
using System.Linq;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.ArmyTargeting;
using TAOM.Features.CoopInterop;
using TAOM.Features.Diplomacy;
using TAOM.Features.Diplomacy.Models;
using TAOM.Features.Execution;
using TAOM.Features.WarChronicle;
using TAOM.Features.WarChronicle.Effects;
using TAOM.Features.WarChronicle.Events;
using TAOM.Features.WarChronicle.Ledger;
using TAOM.Features.WarChronicle.Rally;
using TAOM.Features.WarOfTheRingMomentum;
using TAOM.Features.WarOfTheRingMomentum.Domain;

namespace TAOM.Tests.Features.WarChronicle.Events;

/// <summary>A hand-written map for the chronicle tests: owners, besiegers and the clock are plain fields.</summary>
internal sealed class FakeChronicleWorld : IChronicleWorldAdapter
{
    public double Day { get; set; }

    public Dictionary<string, string?> Owners { get; } = new Dictionary<string, string?>();

    public Dictionary<string, string?> Besiegers { get; } = new Dictionary<string, string?>();

    public HashSet<string> Kingdoms { get; } = new HashSet<string>();

    public double GetElapsedDays() => Day;

    public double GetNowHours() => 1000d + Day * 24d;

    public bool SettlementExists(string settlementId) => settlementId != null && Owners.ContainsKey(settlementId);

    public bool KingdomExists(string kingdomId) => kingdomId != null && Kingdoms.Contains(kingdomId);

    public string? GetOwnerKingdomId(string settlementId) =>
        settlementId != null && Owners.TryGetValue(settlementId, out var owner) ? owner : null;

    public string? GetBesiegerKingdomId(string settlementId) =>
        settlementId != null && Besiegers.TryGetValue(settlementId, out var besieger) ? besieger : null;
}

/// <summary>
/// The chronicle wired from real services over fakes of the engine edges: the effect registry, the store,
/// the queue, the focus registry and the ledger are real; the world, the alliances, the phase machine,
/// the presenter and the momentum award are fakes the tests drive.
/// </summary>
internal sealed class ChronicleFixture
{
    public ChronicleFixture()
    {
        Logger = Substitute.For<IModLogger>();
        Logger.When(l => l.LogInfo(Arg.Any<string>())).Do(c => Logged.Add(c.Arg<string>()));
        Logger.When(l => l.LogWarning(Arg.Any<string>())).Do(c => Warnings.Add(c.Arg<string>()));

        World = new FakeChronicleWorld();
        foreach (var k in new[] { "isengard", "empire", "vlandia", "empire_s", "empire_w", "rivendell", "khuzait", "sturgia" })
            World.Kingdoms.Add(k);
        World.Owners["town_V2"] = "vlandia";
        World.Owners["town_EW1"] = "empire_w";
        World.Owners["town_EW3"] = "empire_w";

        Alliance = Substitute.For<IAllianceAdapter>();
        Alliance.GetAllKingdomIds().Returns(_ => World.Kingdoms.ToList());
        Alliance.AreAtWar(Arg.Any<string>(), Arg.Any<string>()).Returns(c => AtWar.Contains(Pair(c.ArgAt<string>(0), c.ArgAt<string>(1))));
        Alliance.GetKingdomCultureId(Arg.Any<string>()).Returns(c => c.Arg<string>());
        AtWar.Add(Pair("isengard", "vlandia"));
        AtWar.Add(Pair("empire", "vlandia"));
        AtWar.Add(Pair("empire_s", "empire_w"));

        Alignment = Substitute.For<IAlignmentService>();
        Alignment.ResolveSide(Arg.Any<string>(), Arg.Any<string>()).Returns(c =>
            new[] { "vlandia", "empire_w", "rivendell", "sturgia" }.Contains(c.ArgAt<string>(0)) ? FactionSide.Free
            : new[] { "isengard", "empire", "empire_s", "khuzait" }.Contains(c.ArgAt<string>(0)) ? FactionSide.Evil
            : FactionSide.Neutral);

        Wotr = Substitute.For<IWarOfTheRingService>();
        Wotr.CurrentPhase.Returns(_ => Phase);
        Wotr.GetEffectivePhaseDay(WarPhase.IsengardWar).Returns(30);
        Wotr.GetEffectivePhaseDay(WarPhase.FullWar).Returns(44);

        Settings = Substitute.For<IWarChronicleSettingsProvider>();
        Settings.WarEffectStrength.Returns(1f);
        Settings.ChronicleEnabled.Returns(true);
        Settings.ChronicleAnnouncements.Returns(true);
        Settings.ArmyFocusMultiplier.Returns(1.4f);
        Settings.StoryOffersEnabled.Returns(true);

        Coop = Substitute.For<ICoopSessionProvider>();
        Coop.IsAuthority.Returns(true);

        Presenter = Substitute.For<IChroniclePresenter>();
        Presenter.IsMapFree().Returns(true);
        Text = Substitute.For<ITextLocalizerAdapter>();
        Text.Localize(Arg.Any<string>()).Returns(c => "resolved:" + c.Arg<string>());

        Momentum = Substitute.For<IMomentumAwardService>();
        Momentum.TryAwardScripted(Arg.Any<MomentumSide>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<double>()).Returns(true);

        Effects = new WarEffectService(Settings, Logger);
        Store = new ChronicleStateStore();
        Focus = new ArmyTargetFocusRegistry();
        Queue = new ChronicleAnnouncementQueue(Presenter, Coop, Settings, Logger);

        var snapshots = Substitute.For<IKingdomWarSnapshotAdapter>();
        snapshots.GetCampaignId().Returns("c1");
        snapshots.GetElapsedDay().Returns(_ => (int)World.Day);
        Ledger = new WarLedgerService(snapshots, new WarBaselineService(), new RallyTierStore(), Effects, Wotr, Alignment, Logger);

        Config = Substitute.For<IChronicleConfigProvider>();
        Config.GetEvents().Returns(_ => Events);
    }

    public IModLogger Logger { get; }

    public List<string> Logged { get; } = new List<string>();

    public List<string> Warnings { get; } = new List<string>();

    public FakeChronicleWorld World { get; }

    public IAllianceAdapter Alliance { get; }

    public HashSet<string> AtWar { get; } = new HashSet<string>();

    public IAlignmentService Alignment { get; }

    public IWarOfTheRingService Wotr { get; }

    public WarPhase Phase { get; set; } = WarPhase.FullWar;

    public IWarChronicleSettingsProvider Settings { get; }

    public ICoopSessionProvider Coop { get; }

    public IChroniclePresenter Presenter { get; }

    public ITextLocalizerAdapter Text { get; }

    public IMomentumAwardService Momentum { get; }

    public WarEffectService Effects { get; }

    public ChronicleStateStore Store { get; }

    public ArmyTargetFocusRegistry Focus { get; }

    public ChronicleAnnouncementQueue Queue { get; }

    public WarLedgerService Ledger { get; }

    /// <summary>The once-per-day gate, shared like the container singleton it stands for.</summary>
    public WarChronicleDailyGate Daily { get; } = new WarChronicleDailyGate();

    public IChronicleConfigProvider Config { get; }

    public List<ChronicleEventDefinition> Events { get; } = new List<ChronicleEventDefinition>();

    public ChronicleService CreateService() => new ChronicleService(
        Config, Store, World, Alliance, Wotr,
        new ChronicleOutcomeService(Effects, Alliance, Alignment, Momentum, Ledger, Text, Logger),
        Queue, Focus, Settings, Logger, Daily);

    public IEnumerable<string> LedgerLines => Logged.Where(l => l.Contains("ev=chronicle"));

    public static string Pair(string a, string b) => string.CompareOrdinal(a, b) < 0 ? a + "|" + b : b + "|" + a;

    public static ChronicleEventDefinition Narrative(string id, ChronicleAnchor anchor = ChronicleAnchor.CampaignStart, int offset = 20,
        string? anchorEvent = null, ChronicleEventState? requires = null, params ChronicleEffectDefinition[] effects) =>
        new ChronicleEventDefinition
        {
            Id = id,
            Kind = ChronicleEventKind.Narrative,
            Anchor = anchor,
            AnchorEventId = anchorEvent,
            RequiresOutcome = requires,
            OffsetDays = offset,
            Title = "{=taom_wc_" + id + "_title}" + id,
            Occurred = new ChronicleOutcomeDefinition { Text = "{=taom_wc_" + id + "_occurred}done", Effects = effects },
        };

    public static ChronicleEventDefinition SiegeDef(string id = "hornburg", string target = "town_V2", string defender = "vlandia",
        string[]? attackers = null, ChronicleAnchor anchor = ChronicleAnchor.Phase2, int offset = 5, int window = 20, string? anchorEvent = null) =>
        new ChronicleEventDefinition
        {
            Id = id,
            Kind = ChronicleEventKind.Siege,
            Anchor = anchor,
            AnchorEventId = anchorEvent,
            OffsetDays = offset,
            WindowDays = window,
            TargetSettlementId = target,
            Attackers = attackers ?? new[] { "isengard", "empire" },
            Defender = defender,
            Title = "{=taom_wc_" + id + "_title}" + id,
            OpenText = "{=taom_wc_" + id + "_open}open {TARGET} {DAYS}",
            LapsedText = "{=taom_wc_" + id + "_lapsed}lapsed",
            Held = new ChronicleOutcomeDefinition
            {
                Text = "{=taom_wc_" + id + "_held}held",
                Effects = new[] { Effect("defender", WarEffectKind.VolunteerRate, 0.15f, 25), Effect("attacker", WarEffectKind.PrisonerEscape, 0.20f, 25) },
            },
            Fell = new ChronicleOutcomeDefinition
            {
                Text = "{=taom_wc_" + id + "_fell}fell",
                Effects = new[] { Effect("attacker", WarEffectKind.VolunteerRate, 0.15f, 25), Effect("defender", WarEffectKind.PrisonerEscape, 0.20f, 25) },
            },
        };

    public static ChronicleEffectDefinition Effect(string target, WarEffectKind kind = WarEffectKind.VolunteerRate, float magnitude = 0.1f, int days = 20) =>
        new ChronicleEffectDefinition { Target = target, Kind = kind, Magnitude = magnitude, DurationDays = days };

    public float Volunteers(string kingdom) => Effects.GetMultiplier(kingdom, WarEffectKind.VolunteerRate);

    public float Escape(string kingdom) => Effects.GetMultiplier(kingdom, WarEffectKind.PrisonerEscape);

    public ChronicleEventState StateOf(string id) =>
        Store.Get(id)?.State ?? throw new InvalidOperationException("no record for " + id);
}
