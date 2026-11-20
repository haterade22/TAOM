using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Elephant;

namespace TAOM.Tests.Features.Elephant;

/// <summary>
/// The per-seat numbers behind the crew status line (#1482). Totals alone misled two rounds of diagnosis: "340
/// teleports against 27 restarts" was read as clearing the teleport, and one drift sample of a sawtooth was read as
/// the navmesh carrying the crew. The probe answers by timing (was a rescue inside the reload that restarted) and
/// by the range over a whole period (how far behind its seat an archer got).
/// </summary>
[TestClass]
public class HowdahSeatProbeTests
{
    [TestMethod]
    public void ARestart_WithARescueSinceTheLastOne_IsCountedAgainstTheRescue()
    {
        var probe = new HowdahSeatProbe();
        probe.NoteProgress(0.4f);
        probe.NoteRescue();
        probe.NoteProgress(0.85f);
        probe.NoteProgress(0.02f);

        Assert.AreEqual(1, probe.Restarts);
        Assert.AreEqual(1, probe.RestartsAfterRescue);
    }

    [TestMethod]
    public void ARestart_WithNoRescueInsideIt_ClearsTheRescue()
    {
        var probe = new HowdahSeatProbe();
        probe.NoteRescue();
        probe.NoteProgress(0.85f);
        probe.NoteProgress(0.02f); // the first restart owns that rescue
        probe.NoteProgress(0.85f);
        probe.NoteProgress(0.03f); // nothing placed the archer during this one

        Assert.AreEqual(2, probe.Restarts);
        Assert.AreEqual(1, probe.RestartsAfterRescue);
        Assert.AreEqual(1, probe.Rescues);
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
    public void EndingAPeriod_ClearsTheRanges_AndKeepsTheTotals()
    {
        var probe = new HowdahSeatProbe();
        probe.NoteRescue();
        probe.NoteLag(forward: -0.4f, distance: 0.4f);

        string line = probe.FormatAndEndPeriod();

        StringAssert.Contains(line, "rs1");
        StringAssert.Contains(line, "lag-0.40..-0.40");
        Assert.AreEqual(1, probe.Rescues, "totals are per occupant, not per period");
        Assert.IsTrue(float.IsNaN(probe.LagForwardMin), "a new period starts with no lag sample");
    }

    [TestMethod]
    public void ARescueOfThePreviousOccupant_IsNotChargedToTheNextOnesRestart()
    {
        var probe = new HowdahSeatProbe();
        probe.NoteRescue();
        probe.Reset();
        probe.NoteProgress(0.9f);
        probe.NoteProgress(0.1f);

        Assert.AreEqual(1, probe.Restarts);
        Assert.AreEqual(0, probe.RestartsAfterRescue);
    }

    [TestMethod]
    public void TheStatusFields_KeepTheOrderTheLogReaderDependsOn()
    {
        var probe = new HowdahSeatProbe();
        probe.NoteRescue();
        probe.NoteProgress(0.85f);
        probe.NoteProgress(0.02f);
        probe.NoteLag(forward: -0.5f, distance: 0.6f);
        probe.NoteLag(forward: 0.1f, distance: 0.1f);

        Assert.AreEqual("rs1/re1(rs1)/max0.85/lag-0.50..0.10/off0.60", probe.FormatAndEndPeriod());
    }

    [TestMethod]
    public void ANewOccupant_StartsFromZero()
    {
        var probe = new HowdahSeatProbe();
        probe.NoteRescue();
        probe.NoteProgress(0.9f);
        probe.NoteProgress(0.1f);

        probe.Reset();

        Assert.AreEqual(0, probe.Rescues);
        Assert.AreEqual(0, probe.Restarts);
        Assert.AreEqual(0f, probe.MaxProgress, 1e-6f);
    }
}
