using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.WarChronicle.Effects;
using TAOM.Features.WarChronicle.Events;
using TAOM.Features.WarOfTheRingMomentum.Domain;

namespace TAOM.Tests.Features.WarChronicle.Events;

/// <summary>
/// The report <see cref="ChronicleOutcomeService.Apply"/> returns is what the event window lists, so it must name only
/// what was written: the outcome's own rows, the live kingdoms each reached, and a momentum award the momentum service took.
/// </summary>
[TestClass]
public class ChronicleOutcomeReportTests
{
    private ChronicleFixture _f = null!;

    [TestInitialize]
    public void SetUp() => _f = new ChronicleFixture();

    private ChronicleOutcomeService Service() =>
        new ChronicleOutcomeService(_f.Effects, _f.Alliance, _f.Alignment, _f.Momentum, _f.Ledger, _f.Text, _f.Logger);

    [TestMethod]
    public void Apply_Held_ReportsTheHeldRowsWithTheirValuesAndDurations()
    {
        var report = Service().Apply(ChronicleFixture.SiegeDef(), ChronicleEventState.Held, 1000d);

        Assert.AreEqual(2, report.Effects.Count);
        var volunteers = report.Effects[0];
        Assert.AreEqual("defender", volunteers.Target);
        CollectionAssert.AreEqual(new[] { "vlandia" }, volunteers.KingdomIds.ToArray());
        Assert.AreEqual(WarEffectKind.VolunteerRate, volunteers.Kind);
        Assert.AreEqual(0.15f, volunteers.Magnitude, 1e-6f);
        Assert.AreEqual(25, volunteers.DurationDays);
        var escape = report.Effects[1];
        Assert.AreEqual("attacker", escape.Target);
        CollectionAssert.AreEqual(new[] { "isengard", "empire" }, escape.KingdomIds.ToArray());
        Assert.AreEqual(WarEffectKind.PrisonerEscape, escape.Kind);
    }

    [TestMethod]
    public void Apply_Fell_ReportsTheFellRowsNotTheHeldOnes()
    {
        var report = Service().Apply(ChronicleFixture.SiegeDef(), ChronicleEventState.Fell, 1000d);

        Assert.AreEqual("attacker", report.Effects[0].Target);
        Assert.AreEqual(WarEffectKind.VolunteerRate, report.Effects[0].Kind);
        Assert.AreEqual("defender", report.Effects[1].Target);
        Assert.AreEqual(WarEffectKind.PrisonerEscape, report.Effects[1].Kind);
    }

    [TestMethod]
    public void Apply_Lapsed_ReportsNothing()
    {
        var report = Service().Apply(ChronicleFixture.SiegeDef(), ChronicleEventState.Lapsed, 1000d);

        Assert.IsTrue(report.IsEmpty);
    }

    [TestMethod]
    public void Apply_Skipped_ReportsNothing()
    {
        var report = Service().Apply(ChronicleFixture.Narrative("council", effects: ChronicleFixture.Effect("free")), ChronicleEventState.Skipped, 1000d);

        Assert.IsTrue(report.IsEmpty);
    }

    [TestMethod]
    public void Apply_ActivationOnlyNarrativeWithoutEffects_ReportsNothing()
    {
        var report = Service().Apply(ChronicleFixture.Narrative("council"), ChronicleEventState.Occurred, 1000d);

        Assert.IsTrue(report.IsEmpty);
    }

    [TestMethod]
    public void Apply_SideTarget_ReportsEveryLiveKingdomOfThatSide()
    {
        var report = Service().Apply(ChronicleFixture.Narrative("council", effects: ChronicleFixture.Effect("evil", WarEffectKind.VolunteerRate, -0.05f, 10)), ChronicleEventState.Occurred, 1000d);

        var row = report.Effects.Single();
        Assert.AreEqual("evil", row.Target);
        CollectionAssert.AreEquivalent(new[] { "isengard", "empire", "empire_s", "khuzait" }, row.KingdomIds.ToArray());
        Assert.AreEqual(-0.05f, row.Magnitude, 1e-6f);
        Assert.AreEqual(10, row.DurationDays);
    }

    [TestMethod]
    public void Apply_RowWhoseKingdomIsGone_IsLeftOutOfTheReport()
    {
        _f.World.Kingdoms.Remove("rivendell");
        var report = Service().Apply(ChronicleFixture.Narrative("council", effects: new[]
        {
            ChronicleFixture.Effect("rivendell"),
            ChronicleFixture.Effect("sturgia", WarEffectKind.PrisonerEscape, 0.2f, 15),
        }), ChronicleEventState.Occurred, 1000d);

        Assert.AreEqual("sturgia", report.Effects.Single().Target);
        CollectionAssert.AreEqual(new[] { "sturgia" }, report.Effects.Single().KingdomIds.ToArray());
    }

    [TestMethod]
    public void Apply_AttackersPartlyGone_ReportsOnlyTheLiveOnes()
    {
        _f.World.Kingdoms.Remove("empire");
        var report = Service().Apply(ChronicleFixture.SiegeDef(), ChronicleEventState.Held, 1000d);

        CollectionAssert.AreEqual(new[] { "isengard" }, report.Effects[1].KingdomIds.ToArray());
    }

    [TestMethod]
    public void Apply_MomentumAccepted_ReportsTheAward()
    {
        var definition = ChronicleFixture.Narrative("council");
        definition.Occurred!.Momentum = new ChronicleMomentumAward { Side = MomentumSide.Free, Units = 40 };

        var report = Service().Apply(definition, ChronicleEventState.Occurred, 1000d);

        Assert.IsNotNull(report.Momentum);
        Assert.AreEqual(MomentumSide.Free, report.Momentum!.Side);
        Assert.AreEqual(40, report.Momentum.Units);
        Assert.IsFalse(report.IsEmpty);
    }

    [TestMethod]
    public void Apply_MomentumRefused_ReportsNoAward()
    {
        _f.Momentum.TryAwardScripted(Arg.Any<MomentumSide>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<double>()).Returns(false);
        var definition = ChronicleFixture.Narrative("council");
        definition.Occurred!.Momentum = new ChronicleMomentumAward { Side = MomentumSide.Evil, Units = 40 };

        var report = Service().Apply(definition, ChronicleEventState.Occurred, 1000d);

        Assert.IsNull(report.Momentum);
        Assert.IsTrue(report.IsEmpty);
    }

    // ---- The announcement carries it -----------------------------------------------------------------

    private ChronicleAnnouncement? ShowNext()
    {
        ChronicleAnnouncement? shown = null;
        _f.Presenter.When(p => p.Show(Arg.Any<ChronicleAnnouncement>())).Do(c => shown = c.Arg<ChronicleAnnouncement>());
        _f.Queue.TryShowNext();
        return shown;
    }

    [TestMethod]
    public void Announcement_NarrativeOccurred_CarriesTheAppliedEffects()
    {
        _f.Events.Add(ChronicleFixture.Narrative("blackgate", offset: 20, effects: ChronicleFixture.Effect("free", WarEffectKind.VolunteerRate, 0.1f, 15)));
        var sut = _f.CreateService();
        _f.World.Day = 0;
        sut.LaunchChronicle();
        _f.World.Day = 20;
        sut.RunChronicleDay();

        var shown = ShowNext();

        Assert.AreEqual("blackgate", shown?.EventId);
        Assert.AreEqual(WarEffectKind.VolunteerRate, shown!.Outcome.Effects.Single().Kind);
        Assert.AreEqual(15, shown.Outcome.Effects.Single().DurationDays);
    }

    [TestMethod]
    public void Announcement_SiegeOpening_CarriesNoEffects()
    {
        _f.Events.Add(ChronicleFixture.SiegeDef());
        var sut = _f.CreateService();
        _f.World.Day = 0;
        sut.LaunchChronicle();
        _f.World.Day = 49;
        sut.RunChronicleDay();

        var shown = ShowNext();

        Assert.AreEqual("hornburg", shown?.EventId);
        Assert.IsTrue(shown!.Outcome.IsEmpty);
    }

    [TestMethod]
    public void Announcement_SiegeLapsed_CarriesNoEffects()
    {
        _f.Events.Add(ChronicleFixture.SiegeDef());
        var sut = _f.CreateService();
        _f.World.Day = 0;
        sut.LaunchChronicle();
        _f.World.Day = 49;
        sut.RunChronicleDay();
        ShowNext();
        _f.World.Day = 69;
        sut.RunChronicleDay();

        var shown = ShowNext();

        Assert.AreEqual(ChronicleEventState.Lapsed, _f.StateOf("hornburg"));
        Assert.IsTrue(shown!.Outcome.IsEmpty);
    }
}
