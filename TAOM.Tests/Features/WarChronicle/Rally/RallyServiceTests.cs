using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Diplomacy;
using TAOM.Features.Execution;
using TAOM.Features.WarChronicle;
using TAOM.Features.WarChronicle.Effects;
using TAOM.Features.WarChronicle.Ledger;
using TAOM.Features.WarChronicle.Rally;

namespace TAOM.Tests.Features.WarChronicle.Rally;

[TestClass]
public class RallyServiceTests
{
    private const double Now = 1000d;

    private IModLogger _logger = null!;
    private IWarChronicleSettingsProvider _settings = null!;
    private IRallyConfigProvider _configProvider = null!;
    private IAlignmentService _alignment = null!;
    private IKingdomWarSnapshotAdapter _snapshots = null!;
    private RallyConfig _config = null!;
    private WarEffectService _effects = null!;
    private WarBaselineService _baselines = null!;
    private RallyTierStore _tiers = null!;
    private RallyService _sut = null!;
    private readonly List<string> _logged = new List<string>();
    private readonly Dictionary<string, FactionSide> _sides = new Dictionary<string, FactionSide>();

    [TestInitialize]
    public void Setup()
    {
        _logged.Clear();
        _sides.Clear();
        _logger = Substitute.For<IModLogger>();
        _logger.When(l => l.LogInfo(Arg.Any<string>())).Do(c => _logged.Add(c.Arg<string>()));
        _settings = Substitute.For<IWarChronicleSettingsProvider>();
        _settings.WarEffectStrength.Returns(1f);
        _settings.WarRallyEnabled.Returns(true);
        _config = new RallyConfig();
        _configProvider = Substitute.For<IRallyConfigProvider>();
        _configProvider.GetConfig().Returns(_ => _config);
        _alignment = Substitute.For<IAlignmentService>();
        _alignment.ResolveSide(Arg.Any<string>(), Arg.Any<string>())
            .Returns(c => _sides.TryGetValue(c.ArgAt<string>(0), out var side) ? side : FactionSide.Free);
        _snapshots = Substitute.For<IKingdomWarSnapshotAdapter>();
        _snapshots.GetCampaignId().Returns("c1");
        _snapshots.GetElapsedDay().Returns(40);
        _effects = new WarEffectService(_settings, _logger);
        _baselines = new WarBaselineService();
        _tiers = new RallyTierStore();
        var wotr = Substitute.For<IWarOfTheRingService>();
        var ledger = new WarLedgerService(_snapshots, _baselines, _tiers, _effects, wotr, _alignment, _logger);
        _sut = new RallyService(_baselines, _tiers, _effects, _configProvider, _settings, _alignment, ledger, _logger);
    }

    private static KingdomWarSnapshot K(
        string id, int towns, int castles = 0, bool atWar = true, bool playerRuled = false) =>
        new KingdomWarSnapshot { Id = id, CultureId = "c_" + id, Towns = towns, Castles = castles, AtWar = atWar, IsPlayerRuled = playerRuled };

    // Takes the baseline from <start>, then runs a day on <now>.
    private void Run(KingdomWarSnapshot start, KingdomWarSnapshot now, double hours = Now)
    {
        _baselines.EnsureBaselines(new[] { start });
        _sut.RunDaily(new[] { now }, hours);
    }

    private IEnumerable<string> TierLines() => _logged.Where(l => l.Contains("ev=tier"));

    // ---- no baseline, no tier ----

    [TestMethod]
    public void RunDaily_NoBaseline_NoTierAndNoEffects()
    {
        _sut.RunDaily(new[] { K("gondor", 0) }, Now);

        Assert.AreEqual(0, _tiers.GetTier("gondor"));
        Assert.AreEqual(0, _effects.Snapshot().Count);
        Assert.AreEqual(0, TierLines().Count());
    }

    [TestMethod]
    public void RunDaily_ABaselineOfZero_HasNoLossToMeasureAndNoTier()
    {
        _baselines.EnsureBaselines(new[] { K("gondor", 0) });

        _sut.RunDaily(new[] { K("gondor", 0) }, Now);

        Assert.AreEqual(0, _tiers.GetTier("gondor"));
        Assert.AreEqual(0, _effects.Snapshot().Count);
    }

    // ---- data rows from the live map ----

    [TestMethod]
    public void RunDaily_ATwoPointKingdomThatLosesItsTown_ReachesTierTwo()
    {
        Run(K("sturgia", towns: 1), K("sturgia", towns: 0));

        Assert.AreEqual(2, _tiers.GetTier("sturgia"));
        Assert.AreEqual(1.2f, _effects.GetMultiplier("sturgia", WarEffectKind.VolunteerRate), 0.0001f);
        Assert.AreEqual(2.0f, _effects.GetMultiplier("sturgia", WarEffectKind.PrisonerEscape), 0.0001f);
    }

    [TestMethod]
    public void RunDaily_AThirtyEightPointKingdomThatLosesNinePoints_StaysAtTierZero()
    {
        // 19 towns (38 points) down to 14 towns and 1 castle (29 points): 9 of 38 is 23.7%.
        Run(K("empire_w", towns: 19), K("empire_w", towns: 14, castles: 1));

        Assert.AreEqual(0, _tiers.GetTier("empire_w"));
        Assert.AreEqual(0, _effects.Snapshot().Count);
        Assert.AreEqual(0, TierLines().Count());
    }

    // ---- the tiers and their effects ----

    [TestMethod]
    public void RunDaily_LossAtTierOneEnter_AppliesTheTierOneMagnitudes()
    {
        // 4 towns (8 points) down to 3 towns (6 points): exactly 25%.
        Run(K("empire_w", towns: 4), K("empire_w", towns: 3));

        Assert.AreEqual(1, _tiers.GetTier("empire_w"));
        Assert.AreEqual(1.10f, _effects.GetMultiplier("empire_w", WarEffectKind.VolunteerRate), 0.0001f);
        Assert.AreEqual(1.50f, _effects.GetMultiplier("empire_w", WarEffectKind.PrisonerEscape), 0.0001f);
    }

    [TestMethod]
    public void RunDaily_TheEffectsAreWrittenUnderTheRallySourceOfTheKingdom()
    {
        Run(K("empire_w", towns: 4), K("empire_w", towns: 3));

        var rows = _effects.Snapshot();

        Assert.AreEqual(2, rows.Count);
        Assert.IsTrue(rows.All(e => e.SourceId == "rally:empire_w" && e.KingdomId == "empire_w"));
        CollectionAssert.AreEquivalent(
            new[] { WarEffectKind.VolunteerRate, WarEffectKind.PrisonerEscape }, rows.Select(e => e.Kind).ToList());
    }

    [TestMethod]
    public void RunDaily_TheEndTimeIsNowPlusTheConfiguredTtl()
    {
        _config.EffectTtlHours = 72f;

        Run(K("empire_w", towns: 4), K("empire_w", towns: 3), hours: 1000d);

        Assert.IsTrue(_effects.Snapshot().All(e => e.EndTimeHours == 1072d));
    }

    [TestMethod]
    public void RunDaily_TheDefaultTtlIsFortyEightHours()
    {
        Run(K("empire_w", towns: 4), K("empire_w", towns: 3), hours: 500d);

        Assert.IsTrue(_effects.Snapshot().All(e => e.EndTimeHours == 548d));
    }

    [TestMethod]
    public void RunDaily_TheMcmStrengthScalesTheBakedMultiplier()
    {
        _settings.WarEffectStrength.Returns(2f);

        Run(K("empire_w", towns: 4), K("empire_w", towns: 3));

        Assert.AreEqual(1.20f, _effects.GetMultiplier("empire_w", WarEffectKind.VolunteerRate), 0.0001f);
    }

    [TestMethod]
    public void RunDaily_AZeroMagnitudeKind_WritesNoRowForIt()
    {
        _config.Tier1.PrisonerEscape = 0f;

        Run(K("empire_w", towns: 4), K("empire_w", towns: 3));

        Assert.AreEqual(1, _effects.Snapshot().Count);
        Assert.AreEqual(WarEffectKind.VolunteerRate, _effects.Snapshot()[0].Kind);
    }

    [TestMethod]
    public void RunDaily_ARiseFromTierOneToTwo_ReplacesTheMagnitudesRatherThanStackingThem()
    {
        _baselines.EnsureBaselines(new[] { K("empire_w", towns: 4) });
        _sut.RunDaily(new[] { K("empire_w", towns: 3) }, Now);

        _sut.RunDaily(new[] { K("empire_w", towns: 2) }, Now + 24);

        Assert.AreEqual(2, _tiers.GetTier("empire_w"));
        Assert.AreEqual(2, _effects.Snapshot().Count);
        Assert.AreEqual(1.20f, _effects.GetMultiplier("empire_w", WarEffectKind.VolunteerRate), 0.0001f);
        Assert.AreEqual(2.00f, _effects.GetMultiplier("empire_w", WarEffectKind.PrisonerEscape), 0.0001f);
    }

    [TestMethod]
    public void RunDaily_ADropFromTwoToOne_DropsAKindTheLowerTierDoesNotCarry()
    {
        _config.Tier1.PrisonerEscape = 0f;
        _baselines.EnsureBaselines(new[] { K("empire_w", towns: 10) });
        _sut.RunDaily(new[] { K("empire_w", towns: 4) }, Now);
        Assert.AreEqual(2.0f, _effects.GetMultiplier("empire_w", WarEffectKind.PrisonerEscape), 0.0001f);

        _sut.RunDaily(new[] { K("empire_w", towns: 7) }, Now + 24);

        Assert.AreEqual(1, _tiers.GetTier("empire_w"));
        Assert.AreEqual(1.0f, _effects.GetMultiplier("empire_w", WarEffectKind.PrisonerEscape), 0.0001f);
        Assert.AreEqual(1.10f, _effects.GetMultiplier("empire_w", WarEffectKind.VolunteerRate), 0.0001f);
    }

    [TestMethod]
    public void RunDaily_ASavedKindWhoseConfiguredMagnitudeIsNowZero_IsRemovedAtTheSameTier()
    {
        _config.Tier1.VolunteerRate = 0f;
        _config.Tier2.VolunteerRate = 0f;
        _baselines.EnsureBaselines(new[] { K("empire_w", towns: 10) });
        _tiers.Restore(new Dictionary<string, int> { ["empire_w"] = 2 });
        _effects.RestoreFromSave(new[]
        {
            new WarEffect("rally:empire_w", "empire_w", WarEffectKind.VolunteerRate, 0.20f, Now + 48),
            new WarEffect("rally:empire_w", "empire_w", WarEffectKind.PrisonerEscape, 1.00f, Now + 48),
        });

        _sut.RunDaily(new[] { K("empire_w", towns: 4) }, Now + 24);

        Assert.AreEqual(2, _tiers.GetTier("empire_w"));
        Assert.AreEqual(1.0f, _effects.GetMultiplier("empire_w", WarEffectKind.VolunteerRate), 0.0001f);
        Assert.AreEqual(2.0f, _effects.GetMultiplier("empire_w", WarEffectKind.PrisonerEscape), 0.0001f);
    }

    [TestMethod]
    public void RunDaily_ADropToTierZero_RemovesTheRallySource()
    {
        _baselines.EnsureBaselines(new[] { K("empire_w", towns: 4) });
        _sut.RunDaily(new[] { K("empire_w", towns: 3) }, Now);
        Assert.AreEqual(2, _effects.Snapshot().Count);

        _sut.RunDaily(new[] { K("empire_w", towns: 4) }, Now + 24);

        Assert.AreEqual(0, _tiers.GetTier("empire_w"));
        Assert.AreEqual(0, _effects.Snapshot().Count);
        Assert.AreEqual(1.0f, _effects.GetMultiplier("empire_w", WarEffectKind.VolunteerRate));
    }

    [TestMethod]
    public void RunDaily_APointsGainAboveTheBaseline_IsNoLoss()
    {
        Run(K("empire_w", towns: 4), K("empire_w", towns: 6));

        Assert.AreEqual(0, _tiers.GetTier("empire_w"));
    }

    // ---- eligibility ----

    [TestMethod]
    public void RunDaily_APlayerRuledKingdom_GetsNoEffectsThoughItsTierTracks()
    {
        Run(K("player_k", towns: 1), K("player_k", towns: 0, playerRuled: true));

        Assert.AreEqual(2, _tiers.GetTier("player_k"));
        Assert.AreEqual(0, _effects.Snapshot().Count);
        Assert.AreEqual(1f, _effects.GetMultiplier("player_k", WarEffectKind.VolunteerRate));
    }

    [TestMethod]
    public void RunDaily_NotAtWar_GetsNoEffectsThoughTheTierStillTracks()
    {
        Run(K("empire_w", towns: 1), K("empire_w", towns: 0, atWar: false));

        Assert.AreEqual(2, _tiers.GetTier("empire_w"));
        Assert.AreEqual(0, _effects.Snapshot().Count);
        Assert.AreEqual(1, TierLines().Count());
    }

    [TestMethod]
    public void RunDaily_TheWarStartsLater_TheTrackedTierThenGainsItsEffects()
    {
        _baselines.EnsureBaselines(new[] { K("empire_w", towns: 1) });
        _sut.RunDaily(new[] { K("empire_w", towns: 0, atWar: false) }, Now);

        _sut.RunDaily(new[] { K("empire_w", towns: 0, atWar: true) }, Now + 24);

        Assert.AreEqual(2, _effects.Snapshot().Count);
        Assert.AreEqual(1, TierLines().Count(), "the tier did not change on the second day");
    }

    [TestMethod]
    public void RunDaily_TheWarEnds_TheEffectsAreRemovedAtOnce()
    {
        _baselines.EnsureBaselines(new[] { K("empire_w", towns: 1) });
        _sut.RunDaily(new[] { K("empire_w", towns: 0, atWar: true) }, Now);
        Assert.AreEqual(2, _effects.Snapshot().Count);

        _sut.RunDaily(new[] { K("empire_w", towns: 0, atWar: false) }, Now + 24);

        Assert.AreEqual(0, _effects.Snapshot().Count);
        Assert.AreEqual(2, _tiers.GetTier("empire_w"));
    }

    [TestMethod]
    public void RunDaily_NeutralWithIncludeNeutralOff_GetsNoEffects()
    {
        _config.IncludeNeutral = false;
        _sides["aserai"] = FactionSide.Neutral;

        Run(K("aserai", towns: 1), K("aserai", towns: 0));

        Assert.AreEqual(2, _tiers.GetTier("aserai"));
        Assert.AreEqual(0, _effects.Snapshot().Count);
    }

    [TestMethod]
    public void RunDaily_NeutralWithIncludeNeutralOn_GetsEffects()
    {
        _config.IncludeNeutral = true;
        _sides["aserai"] = FactionSide.Neutral;

        Run(K("aserai", towns: 1), K("aserai", towns: 0));

        Assert.AreEqual(2, _effects.Snapshot().Count);
    }

    [DataTestMethod]
    [DataRow(FactionSide.Free)]
    [DataRow(FactionSide.Evil)]
    public void RunDaily_FreeAndEvilWithIncludeNeutralOff_StillGetEffects(FactionSide side)
    {
        _config.IncludeNeutral = false;
        _sides["empire_w"] = side;

        Run(K("empire_w", towns: 1), K("empire_w", towns: 0));

        Assert.AreEqual(2, _effects.Snapshot().Count);
    }

    // ---- switching the rally off ----

    [TestMethod]
    public void RunDaily_SwitchedOffInMcm_RemovesEveryRallySourceAndKeepsOthers()
    {
        _baselines.EnsureBaselines(new[] { K("a", towns: 1), K("b", towns: 1) });
        _sut.RunDaily(new[] { K("a", towns: 0), K("b", towns: 0) }, Now);
        _effects.Apply(new WarEffect("event:fellowship", "a", WarEffectKind.VolunteerRate, 0.1f, 9999d));
        Assert.AreEqual(5, _effects.Snapshot().Count);

        _settings.WarRallyEnabled.Returns(false);
        _sut.RunDaily(new[] { K("a", towns: 0), K("b", towns: 0) }, Now + 24);

        var left = _effects.Snapshot();
        Assert.AreEqual(1, left.Count);
        Assert.AreEqual("event:fellowship", left[0].SourceId);
    }

    [TestMethod]
    public void RunDaily_SwitchedOffInTheConfig_RemovesTheRallySources()
    {
        _baselines.EnsureBaselines(new[] { K("a", towns: 1) });
        _sut.RunDaily(new[] { K("a", towns: 0) }, Now);
        Assert.AreEqual(2, _effects.Snapshot().Count);

        _config.Enabled = false;
        _sut.RunDaily(new[] { K("a", towns: 0) }, Now + 24);

        Assert.AreEqual(0, _effects.Snapshot().Count);
    }

    [TestMethod]
    public void RunDaily_SwitchedOff_StillTracksTheTierSoACompareRunSeesWhatWouldHaveFired()
    {
        _settings.WarRallyEnabled.Returns(false);

        Run(K("a", towns: 1), K("a", towns: 0));

        Assert.AreEqual(2, _tiers.GetTier("a"));
        Assert.AreEqual(0, _effects.Snapshot().Count);
        Assert.AreEqual(1, TierLines().Count());
    }

    [TestMethod]
    public void RunDaily_SwitchedBackOn_TheTrackedTierGainsItsEffectsAtTheNextTick()
    {
        _settings.WarRallyEnabled.Returns(false);
        _baselines.EnsureBaselines(new[] { K("a", towns: 1) });
        _sut.RunDaily(new[] { K("a", towns: 0) }, Now);

        _settings.WarRallyEnabled.Returns(true);
        _sut.RunDaily(new[] { K("a", towns: 0) }, Now + 24);

        Assert.AreEqual(2, _effects.Snapshot().Count);
    }

    // ---- idempotence ----

    [TestMethod]
    public void RunDaily_TwoTicksOnTheSameDay_LeaveTheSameEffectsAndOneTierLine()
    {
        _baselines.EnsureBaselines(new[] { K("a", towns: 4) });
        var today = new[] { K("a", towns: 3) };

        _sut.RunDaily(today, Now);
        var first = _effects.Snapshot().Select(e => (e.SourceId, e.Kind, e.Magnitude, e.EndTimeHours)).OrderBy(x => x.Kind).ToList();
        _sut.RunDaily(today, Now);
        var second = _effects.Snapshot().Select(e => (e.SourceId, e.Kind, e.Magnitude, e.EndTimeHours)).OrderBy(x => x.Kind).ToList();

        CollectionAssert.AreEqual(first, second);
        Assert.AreEqual(1, TierLines().Count());
        Assert.AreEqual(1.10f, _effects.GetMultiplier("a", WarEffectKind.VolunteerRate), 0.0001f);
    }

    [TestMethod]
    public void RunDaily_ANextDayAtTheSameTier_RefreshesTheEndTime()
    {
        _baselines.EnsureBaselines(new[] { K("a", towns: 4) });
        _sut.RunDaily(new[] { K("a", towns: 3) }, Now);

        _sut.RunDaily(new[] { K("a", towns: 3) }, Now + 24);

        Assert.AreEqual(2, _effects.Snapshot().Count);
        Assert.IsTrue(_effects.Snapshot().All(e => e.EndTimeHours == Now + 24 + 48));
    }

    // ---- the ledger tier line ----

    [TestMethod]
    public void RunDaily_ATierRise_LogsOneTierLineWithFromToAndLoss()
    {
        Run(K("empire_w", towns: 4), K("empire_w", towns: 3));

        CollectionAssert.AreEqual(
            new[] { "[WarLedger] v=1 t=event cid=c1 day=40 ev=tier k=empire_w from=0 to=1 loss=0.250" },
            TierLines().ToList());
    }

    [TestMethod]
    public void RunDaily_EveryTierChange_LogsALine()
    {
        _baselines.EnsureBaselines(new[] { K("a", towns: 10) });
        _sut.RunDaily(new[] { K("a", towns: 7) }, Now);
        _sut.RunDaily(new[] { K("a", towns: 4) }, Now + 24);
        _sut.RunDaily(new[] { K("a", towns: 7) }, Now + 48);
        _sut.RunDaily(new[] { K("a", towns: 10) }, Now + 72);

        var lines = TierLines().ToList();
        Assert.AreEqual(4, lines.Count);
        StringAssert.Contains(lines[0], "from=0 to=1");
        StringAssert.Contains(lines[1], "from=1 to=2");
        StringAssert.Contains(lines[2], "from=2 to=1");
        StringAssert.Contains(lines[3], "from=1 to=0 loss=0.000");
    }

    // ---- housekeeping ----

    [TestMethod]
    public void RunDaily_AKingdomThatLeftTheMap_LosesItsTierAndItsRallySourceButNotOtherSources()
    {
        _baselines.EnsureBaselines(new[] { K("a", towns: 1), K("b", towns: 1) });
        _sut.RunDaily(new[] { K("a", towns: 0), K("b", towns: 0) }, Now);
        _effects.Apply(new WarEffect("event:x", "a", WarEffectKind.VolunteerRate, 0.1f, 9999d));

        _sut.RunDaily(new[] { K("b", towns: 0) }, Now + 24);

        Assert.AreEqual(0, _tiers.GetTier("a"));
        CollectionAssert.AreEquivalent(
            new[] { "event:x", "rally:b", "rally:b" }, _effects.Snapshot().Select(e => e.SourceId).ToList());
    }

    [TestMethod]
    public void RunDaily_DuplicateAndEmptyAndNullSnapshots_AreSkipped()
    {
        _baselines.EnsureBaselines(new[] { K("a", towns: 1) });

        _sut.RunDaily(new[] { K("a", towns: 0), K("a", towns: 0), null!, K("", towns: 0) }, Now);

        Assert.AreEqual(1, TierLines().Count());
        Assert.AreEqual(2, _effects.Snapshot().Count);
    }

    [TestMethod]
    public void RunDaily_NullKingdoms_DoesNothing()
    {
        _sut.RunDaily(null!, Now);

        Assert.AreEqual(0, _effects.Snapshot().Count);
    }

    [TestMethod]
    public void RunDaily_AnEmptyKingdomRead_KeepsTheTiersRatherThanWipingThem()
    {
        _tiers.SetTier("a", 2);

        _sut.RunDaily(new List<KingdomWarSnapshot>(), Now);

        Assert.AreEqual(2, _tiers.GetTier("a"));
    }

    [DataTestMethod]
    [DataRow(double.NaN)]
    [DataRow(double.PositiveInfinity)]
    public void RunDaily_ANonFiniteClock_WritesNothingAndWarns(double hours)
    {
        _baselines.EnsureBaselines(new[] { K("a", towns: 1) });

        _sut.RunDaily(new[] { K("a", towns: 0) }, hours);

        Assert.AreEqual(0, _tiers.GetTier("a"));
        Assert.AreEqual(0, _effects.Snapshot().Count);
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("clock")));
    }

    [TestMethod]
    public void RunDaily_TheEffectRegistryThrows_IsLoggedAndDoesNotEscape()
    {
        var effects = Substitute.For<IWarEffectService>();
        effects.When(e => e.Apply(Arg.Any<WarEffect>())).Do(_ => throw new InvalidOperationException("registry"));
        var wotr = Substitute.For<IWarOfTheRingService>();
        var ledger = new WarLedgerService(_snapshots, _baselines, _tiers, effects, wotr, _alignment, _logger);
        var sut = new RallyService(_baselines, _tiers, effects, _configProvider, _settings, _alignment, ledger, _logger);
        _baselines.EnsureBaselines(new[] { K("a", towns: 1) });

        sut.RunDaily(new[] { K("a", towns: 0) }, Now);

        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("Rally") && m.Contains("registry")));
    }

    // ---- the console report ----

    [TestMethod]
    public void Report_ListsPointsBaselineLossTierAndEligibilityPerKingdom()
    {
        _baselines.EnsureBaselines(new[] { K("a", towns: 4), K("b", towns: 10), K("c", towns: 3) });
        _sut.RunDaily(new[] { K("a", towns: 3), K("b", towns: 10), K("c", towns: 3, atWar: false) }, Now);

        var rows = _sut.Report(new[] { K("a", towns: 3), K("b", towns: 10), K("c", towns: 3, atWar: false), K("d", towns: 2) });

        Assert.AreEqual(4, rows.Count);
        var a = rows.Single(r => r.KingdomId == "a");
        Assert.AreEqual(6, a.Points);
        Assert.AreEqual(8, a.Baseline);
        Assert.AreEqual(0.25f, a.Loss!.Value, 0.0001f);
        Assert.AreEqual(1, a.Tier);
        Assert.IsTrue(a.Eligible);
        Assert.AreEqual(0, rows.Single(r => r.KingdomId == "b").Tier);
        Assert.IsFalse(rows.Single(r => r.KingdomId == "c").Eligible);
        var d = rows.Single(r => r.KingdomId == "d");
        Assert.IsNull(d.Baseline);
        Assert.IsNull(d.Loss);
    }

    [TestMethod]
    public void Report_APlayerRuledKingdom_IsNotEligible()
    {
        var rows = _sut.Report(new[] { K("p", towns: 2, playerRuled: true) });

        Assert.IsFalse(rows[0].Eligible);
    }

    [TestMethod]
    public void Report_NullOrEmpty_IsEmpty()
    {
        Assert.AreEqual(0, _sut.Report(null!).Count);
        Assert.AreEqual(0, _sut.Report(new List<KingdomWarSnapshot>()).Count);
    }

    [TestMethod]
    public void Report_ANullKingdomInTheList_SkipsIt()
    {
        var rows = _sut.Report(new[] { K("a", towns: 2), null! });

        Assert.AreEqual(1, rows.Count);
        Assert.AreEqual("a", rows[0].KingdomId);
    }

    [TestMethod]
    public void Report_AKingdomWithAnEmptyId_SkipsIt()
    {
        var rows = _sut.Report(new[] { K("a", towns: 2), K("", towns: 1) });

        Assert.AreEqual(1, rows.Count);
        Assert.AreEqual("a", rows[0].KingdomId);
    }

    [DataTestMethod]
    [DataRow(true, true, true)]
    [DataRow(true, false, false)]
    [DataRow(false, true, false)]
    [DataRow(false, false, false)]
    public void IsActive_ConfigAndMcmSwitch_TrueOnlyWhenBothOn(bool config, bool mcm, bool expected)
    {
        _config.Enabled = config;
        _settings.WarRallyEnabled.Returns(mcm);

        Assert.AreEqual(expected, _sut.IsActive);
    }
}
