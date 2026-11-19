using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Elephant;

namespace TAOM.Tests.Features.Elephant;

/// <summary>
/// The per-seat numbers behind the crew status line (#1482). Totals alone misled two rounds of diagnosis: "340
/// teleports against 27 restarts" was read as clearing the teleport, and one drift sample of a sawtooth was read as
/// the navmesh carrying the crew. The probe answers by timing (was a teleport inside the reload that restarted) and
/// by the range over a whole period (how far behind its seat an archer got).
/// </summary>
[TestClass]
public class HowdahSeatProbeTests
{
    [TestMethod]
    public void ARestart_WithATeleportSinceTheLastOne_IsCountedAgainstTheTeleport()
    {
        var probe = new HowdahSeatProbe();
        probe.NoteProgress(0.4f);
        probe.NoteTeleport();
        probe.NoteProgress(0.85f);
        probe.NoteProgress(0.02f);

        Assert.AreEqual(1, probe.Restarts);
        Assert.AreEqual(1, probe.RestartsAfterTeleport);
    }

    [TestMethod]
    public void ARestart_WithNoTeleportInsideIt_ClearsTheTeleport()
    {
        var probe = new HowdahSeatProbe();
        probe.NoteTeleport();
        probe.NoteProgress(0.85f);
        probe.NoteProgress(0.02f); // the first restart owns that teleport
        probe.NoteProgress(0.85f);
        probe.NoteProgress(0.03f); // nothing teleported during this one

        Assert.AreEqual(2, probe.Restarts);
        Assert.AreEqual(1, probe.RestartsAfterTeleport);
        Assert.AreEqual(1, probe.Teleports);
    }

    [TestMethod]
    public void ProgressThatOnlyRises_IsNotARestart_AndTheMaximumIsKept()
    {
        var probe = new HowdahSeatProbe();
        probe.NoteProgress(0.2f);
        probe.NoteProgress(0.6f);
        probe.NoteProgress(1.0f);
        probe.NoteProgress(0.995f); // inside the 0.01 jitter band

        Assert.AreEqual(0, probe.Restarts);
        Assert.AreEqual(1.0f, probe.MaxProgress, 1e-6f);
    }

    [TestMethod]
    public void ANonFiniteProgress_IsIgnored()
    {
        var probe = new HowdahSeatProbe();
        probe.NoteProgress(0.9f);
        probe.NoteProgress(float.NaN);
        probe.NoteProgress(float.PositiveInfinity);
        probe.NoteProgress(0.95f);

        Assert.AreEqual(0, probe.Restarts, "NaN must not read as a fall back to zero");
        Assert.AreEqual(0.95f, probe.MaxProgress, 1e-6f);
    }

    [TestMethod]
    public void TheLagRange_CoversTheWholePeriod_NotTheLastSample()
    {
        var probe = new HowdahSeatProbe();
        probe.NoteLag(forward: -0.10f, distance: 0.10f);
        probe.NoteLag(forward: -0.70f, distance: 0.72f);
        probe.NoteLag(forward: 0.05f, distance: 0.05f);
        probe.NoteLag(forward: float.NaN, distance: float.NaN);

        Assert.AreEqual(-0.70f, probe.LagForwardMin, 1e-6f);
        Assert.AreEqual(0.05f, probe.LagForwardMax, 1e-6f);
        Assert.AreEqual(0.72f, probe.LagDistanceMax, 1e-6f);
    }

    [TestMethod]
    public void TheTickOrder_IsCountedPerSeatTick()
    {
        var probe = new HowdahSeatProbe();
        probe.NoteSeatTick(seenDistance: 0.2f, platformTickedFirst: false);
        probe.NoteSeatTick(seenDistance: 0.5f, platformTickedFirst: false);
        probe.NoteSeatTick(seenDistance: 0.1f, platformTickedFirst: true);

        Assert.AreEqual(3, probe.SeatTicks);
        Assert.AreEqual(2, probe.SeatFirstTicks);
        Assert.AreEqual(0.5f, probe.SeenDistanceMax, 1e-6f);
    }

    [TestMethod]
    public void EndingAPeriod_ClearsTheRanges_AndKeepsTheTotals()
    {
        var probe = new HowdahSeatProbe();
        probe.NoteTeleport();
        probe.NoteRescue();
        probe.NoteLag(forward: -0.4f, distance: 0.4f);
        probe.NoteSeatTick(seenDistance: 0.4f, platformTickedFirst: false);

        string line = probe.FormatAndEndPeriod();

        StringAssert.Contains(line, "tp1");
        StringAssert.Contains(line, "rs1");
        StringAssert.Contains(line, "lag-0.40..-0.40");
        Assert.AreEqual(1, probe.Teleports, "totals are per occupant, not per period");
        Assert.AreEqual(1, probe.Rescues);
        Assert.IsTrue(float.IsNaN(probe.LagForwardMin), "a new period starts with no lag sample");
        Assert.AreEqual(0, probe.SeatTicks);
    }

    [TestMethod]
    public void ARescue_CountsAgainstTheRestartItFellInside()
    {
        var probe = new HowdahSeatProbe();
        probe.NoteProgress(0.5f);
        probe.NoteRescue();
        probe.NoteProgress(0.1f);

        Assert.AreEqual(1, probe.RestartsAfterTeleport, "a rescue is a teleport too");
        Assert.AreEqual(0, probe.Teleports, "but it is counted apart from the seat's ordinary correction");
    }

    [TestMethod]
    public void ATeleportOfThePreviousOccupant_IsNotChargedToTheNextOnesRestart()
    {
        var probe = new HowdahSeatProbe();
        probe.NoteTeleport();
        probe.Reset();
        probe.NoteProgress(0.9f);
        probe.NoteProgress(0.1f);

        Assert.AreEqual(1, probe.Restarts);
        Assert.AreEqual(0, probe.RestartsAfterTeleport);
    }

    [TestMethod]
    public void ASeatTickWithANonFiniteDistance_IsStillATick()
    {
        var probe = new HowdahSeatProbe();
        probe.NoteSeatTick(seenDistance: float.NaN, platformTickedFirst: false);

        Assert.AreEqual(1, probe.SeatTicks);
        Assert.AreEqual(1, probe.SeatFirstTicks);
        Assert.IsTrue(float.IsNaN(probe.SeenDistanceMax));
    }

    [TestMethod]
    public void TheStatusFields_KeepTheOrderTheLogReaderDependsOn()
    {
        var probe = new HowdahSeatProbe();
        probe.NoteTeleport();
        probe.NoteTeleport();
        probe.NoteRescue();
        probe.NoteProgress(0.85f);
        probe.NoteProgress(0.02f);
        probe.NoteLag(forward: -0.5f, distance: 0.6f);
        probe.NoteLag(forward: 0.1f, distance: 0.1f);
        probe.NoteSeatTick(seenDistance: 0.7f, platformTickedFirst: false);
        probe.NoteSeatTick(seenDistance: 0.2f, platformTickedFirst: true);

        Assert.AreEqual("tp2/rs1/re1(tp1)/max0.85/lag-0.50..0.10/off0.60/seen0.70/seatFirst1of2", probe.FormatAndEndPeriod());
    }

    [TestMethod]
    public void ANewOccupant_StartsFromZero()
    {
        var probe = new HowdahSeatProbe();
        probe.NoteTeleport();
        probe.NoteProgress(0.9f);
        probe.NoteProgress(0.1f);

        probe.Reset();

        Assert.AreEqual(0, probe.Teleports);
        Assert.AreEqual(0, probe.Restarts);
        Assert.AreEqual(0f, probe.MaxProgress, 1e-6f);
    }
}
