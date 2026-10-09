using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Diplomacy;
using TAOM.Features.Diplomacy.Models;
using TAOM.Features.Execution;
using TAOM.Features.WarChronicle;
using TAOM.Features.WarChronicle.Effects;
using TAOM.Features.WarChronicle.Ledger;
using TAOM.Features.WarChronicle.Rally;

namespace TAOM.Tests.Features.WarChronicle;

[TestClass]
public class WarChronicleTickServiceTests
{
    private IKingdomWarSnapshotAdapter _snapshots = null!;
    private IWarEffectService _effects = null!;
    private WarBaselineService _baselines = null!;
    private RallyTierStore _tiers = null!;
    private IModLogger _logger = null!;
    private IPrisonerEscapeAdapter _escapeAdapter = null!;
    private IWarChronicleSettingsProvider _settings = null!;
    private IWarOfTheRingService _wotr = null!;
    private IAlignmentService _alignment = null!;
    private IRallyConfigProvider _rallyConfig = null!;
    private WarChronicleTickService _sut = null!;
    private readonly List<string> _logged = new List<string>();

    [TestInitialize]
    public void Setup()
    {
        _logged.Clear();
        _snapshots = Substitute.For<IKingdomWarSnapshotAdapter>();
        _snapshots.GetCampaignId().Returns("c1");
        _snapshots.GetElapsedDay().Returns(1);
        _snapshots.GetNowHours().Returns(500d);
        _snapshots.GetKingdoms().Returns(new List<KingdomWarSnapshot>
        {
            new KingdomWarSnapshot { Id = "empire_w", CultureId = "gondor", Towns = 3, Castles = 1, AtWar = true },
            new KingdomWarSnapshot { Id = "empire_s", CultureId = "mordor", Towns = 2, Castles = 2 },
        });
        _effects = Substitute.For<IWarEffectService>();
        _effects.GetMultiplier(Arg.Any<string>(), Arg.Any<WarEffectKind>()).Returns(1f);
        _logger = Substitute.For<IModLogger>();
        _logger.When(l => l.LogInfo(Arg.Any<string>())).Do(c => _logged.Add(c.Arg<string>()));
        _baselines = new WarBaselineService();
        _wotr = Substitute.For<IWarOfTheRingService>();
        _wotr.CurrentPhase.Returns(WarPhase.Peace);
        _alignment = Substitute.For<IAlignmentService>();
        _tiers = new RallyTierStore();
        _escapeAdapter = Substitute.For<IPrisonerEscapeAdapter>();
        _escapeAdapter.GetCapturedLords(Arg.Any<IReadOnlyCollection<string>>()).Returns(new List<PrisonerEscapeSnapshot>());
        _rallyConfig = Substitute.For<IRallyConfigProvider>();
        _rallyConfig.GetConfig().Returns(new RallyConfig());
        _settings = Substitute.For<IWarChronicleSettingsProvider>();
        _settings.WarRallyEnabled.Returns(true);
        // NSubstitute's default strength of 0 would bake every multiplier to 1 in a real registry.
        _settings.WarEffectStrength.Returns(1f);
        _sut = Build(_effects);
    }

    private WarChronicleTickService Build(IWarEffectService effects)
    {
        var ledger = new WarLedgerService(_snapshots, _baselines, _tiers, effects, _wotr, _alignment, _logger);
        var escapes = new WarEscapeDailyPass(_escapeAdapter, effects, new WarEscapeService(), _logger);
        var rally = new RallyService(_baselines, _tiers, effects, _rallyConfig, _settings, _alignment, ledger, _logger);
        return new WarChronicleTickService(_snapshots, effects, _baselines, ledger, escapes, rally, _logger);
    }

    [TestMethod]
    public void RunDaily_AKingdomHasLostTheWar_TheRallyWritesItsEffectsBeforeTheLedgerReadsTheTier()
    {
        _baselines.EnsureBaselines(new[] { new KingdomWarSnapshot { Id = "empire_w", Towns = 8, Castles = 1 } });

        _sut.RunDaily();

        _effects.Received().Apply(Arg.Is<WarEffect>(e =>
            e.SourceId == "rally:empire_w" && e.Kind == WarEffectKind.VolunteerRate && e.EndTimeHours == 548d));
        Assert.AreEqual(2, _tiers.GetTier("empire_w"), "10 of 17 points lost is tier 2");
        Assert.IsTrue(_logged.Any(l => l.Contains("t=kingdom") && l.Contains("k=empire_w") && l.Contains("tier=2")), "the ledger line carries the new tier");
        Assert.IsTrue(_logged.Any(l => l.Contains("ev=tier k=empire_w from=0 to=2")));
    }

    [TestMethod]
    public void RunDaily_TheFirstDayOfACampaign_TheBaselineIsTakenBeforeTheRallyMeasuresSoNothingFires()
    {
        _sut.RunDaily();

        Assert.AreEqual(0, _tiers.GetTier("empire_w"));
        _effects.DidNotReceive().Apply(Arg.Any<WarEffect>());
    }

    [TestMethod]
    public void RunDaily_TheRallyFails_StillWritesTheLedger()
    {
        _baselines.EnsureBaselines(new[] { new KingdomWarSnapshot { Id = "empire_w", Towns = 8, Castles = 1 } });
        _effects.When(e => e.Apply(Arg.Any<WarEffect>())).Do(_ => throw new InvalidOperationException("registry"));

        _sut.RunDaily();

        Assert.AreEqual(6, _logged.Count(l => l.StartsWith("[WarLedger] v=1 t=") && !l.Contains("t=event")));
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("Rally skipped")));
    }

    [TestMethod]
    public void RunDaily_ExpiresOnceWithTheCampaignClock()
    {
        // Expire re-bakes with the live strength, so the tick needs no second bake.
        _sut.RunDaily();

        _effects.Received(1).Expire(500d);
    }

    [TestMethod]
    public void RunDaily_AnEscapeBoostIsActive_RollsTheEscapesAfterTheExpiry()
    {
        _effects.Snapshot().Returns(new List<WarEffect>
        {
            new WarEffect("event", "empire_w", WarEffectKind.PrisonerEscape, 0.2f, 900d),
        });
        _effects.GetMultiplier("empire_w", WarEffectKind.PrisonerEscape).Returns(1.2f);

        _sut.RunDaily();

        Received.InOrder(() =>
        {
            _effects.Expire(500d);
            _escapeAdapter.GetCapturedLords(Arg.Is<IReadOnlyCollection<string>>(ids => ids.Contains("empire_w")));
        });
    }

    [TestMethod]
    public void RunDaily_RallyGrantsEscapeBoost_SameTickRollsIt()
    {
        _baselines.EnsureBaselines(new[] { new KingdomWarSnapshot { Id = "empire_w", Towns = 8, Castles = 1 } });
        _effects.Snapshot().Returns(new List<WarEffect>
        {
            new WarEffect("rally:empire_w", "empire_w", WarEffectKind.PrisonerEscape, 1f, 548d),
        });
        _effects.GetMultiplier("empire_w", WarEffectKind.PrisonerEscape).Returns(2f);

        _sut.RunDaily();

        Received.InOrder(() =>
        {
            _effects.Apply(Arg.Is<WarEffect>(e => e.SourceId == "rally:empire_w" && e.Kind == WarEffectKind.PrisonerEscape));
            _escapeAdapter.GetCapturedLords(Arg.Is<IReadOnlyCollection<string>>(ids => ids.Contains("empire_w")));
        });
    }

    [TestMethod]
    public void RunDaily_RallyTurnedOff_SameTickRollsNoBoost()
    {
        var registry = new WarEffectService(_settings, _logger);
        registry.Apply(new WarEffect("rally:empire_w", "empire_w", WarEffectKind.PrisonerEscape, 1f, 548d));
        _baselines.EnsureBaselines(new[] { new KingdomWarSnapshot { Id = "empire_w", Towns = 8, Castles = 1 } });
        _settings.WarRallyEnabled.Returns(false);
        var sut = Build(registry);

        sut.RunDaily();

        _escapeAdapter.DidNotReceiveWithAnyArgs().GetCapturedLords(default!);
        Assert.AreEqual(1f, registry.GetMultiplier("empire_w", WarEffectKind.PrisonerEscape));
    }

    [TestMethod]
    public void RunDaily_EmptyBaselinesAfterDayOne_LogsTheLateWarningOnce()
    {
        _snapshots.GetElapsedDay().Returns(5);

        _sut.RunDaily();
        _snapshots.GetKingdoms().Returns(new List<KingdomWarSnapshot>
        {
            new KingdomWarSnapshot { Id = "empire_w", Towns = 3 },
            new KingdomWarSnapshot { Id = "rebels_1", Towns = 1 },
        });
        _sut.RunDaily();

        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("Baselines taken on day 5") && m.Contains("2 kingdom(s)")));
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(1)]
    public void RunDaily_FirstTickOfANewCampaign_LogsNoLateWarning(int day)
    {
        _snapshots.GetElapsedDay().Returns(day);

        _sut.RunDaily();

        Assert.AreEqual(7, _baselines.GetBaseline("empire_w"));
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(m => m.Contains("Baselines taken")));
    }

    [TestMethod]
    public void RunDaily_RestoredBaselinesThenARebelKingdom_LogsNoLateWarning()
    {
        _baselines.RestoreFromSave(new Dictionary<string, int> { ["empire_w"] = 9, ["empire_s"] = 6 });
        _snapshots.GetElapsedDay().Returns(50);
        _snapshots.GetKingdoms().Returns(new List<KingdomWarSnapshot>
        {
            new KingdomWarSnapshot { Id = "empire_w", Towns = 3 },
            new KingdomWarSnapshot { Id = "rebels_1", Towns = 1 },
        });

        _sut.RunDaily();

        Assert.AreEqual(2, _baselines.GetBaseline("rebels_1"));
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(m => m.Contains("Baselines taken")));
    }

    [TestMethod]
    public void RunDaily_TheEscapePassFails_StillWritesTheLedger()
    {
        _effects.Snapshot().Returns(new List<WarEffect>
        {
            new WarEffect("event", "empire_w", WarEffectKind.PrisonerEscape, 0.2f, 900d),
        });
        _effects.GetMultiplier("empire_w", WarEffectKind.PrisonerEscape).Returns(1.2f);
        _escapeAdapter.GetCapturedLords(Arg.Any<IReadOnlyCollection<string>>())
            .Returns(_ => throw new InvalidOperationException("engine getter"));

        _sut.RunDaily();

        Assert.AreEqual(6, _logged.Count(l => l.StartsWith("[WarLedger] v=1 ")));
    }

    [TestMethod]
    public void RunDaily_TakesTheBaselinesBeforeTheLedgerSoDayOneHasABase()
    {
        _sut.RunDaily();

        Assert.AreEqual(7, _baselines.GetBaseline("empire_w"));
        var kingdomLines = _logged.Where(l => l.Contains("t=kingdom")).ToList();
        Assert.AreEqual(2, kingdomLines.Count);
        foreach (var line in kingdomLines)
            Assert.IsFalse(line.Contains("base=na"), line);
    }

    [TestMethod]
    public void RunDaily_WritesTheLedger_TwoKingdomsThreeSidesAndAShare()
    {
        _sut.RunDaily();

        Assert.AreEqual(6, _logged.Count(l => l.StartsWith("[WarLedger] v=1 ")));
    }

    [TestMethod]
    public void RunDaily_TheSnapshotReadThrows_StillExpiredAndDoesNotThrow()
    {
        _snapshots.GetKingdoms().Returns(_ => throw new InvalidOperationException("engine getter"));

        _sut.RunDaily();

        _effects.Received(1).Expire(500d);
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("engine getter")));
        Assert.AreEqual(0, _logged.Count);
    }

    [TestMethod]
    public void RunDaily_NoKingdoms_WritesNoLedgerLines()
    {
        _snapshots.GetKingdoms().Returns(new List<KingdomWarSnapshot>());

        _sut.RunDaily();

        Assert.AreEqual(0, _logged.Count);
    }
}
