using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.Refuge.Domain;
using TaleWorlds.CampaignSystem;

namespace TAOM.Tests.Features.Refuge;

/// <summary>
/// Direct coverage for <see cref="RefugeBookRepair"/>, the save-load repair that
/// RefugeService.LoadFrom runs. The clock is two counting delegates, so these tests pin WHEN the
/// repair reads it: a book whose rows are not building reads neither. Only the future-start reset
/// calls engine members (CampaignTime.Never and CampaignTime.Equals, which fail on the RefAsm
/// stubs), so only that method carries RequiresGame; the rest run in the no-game replay.
/// </summary>
[TestClass]
public class RefugeBookRepairTests
{
    private IModLogger _logger;
    private List<string> _warnings;
    private double _days;
    private CampaignTime _now;
    private int _daysCalls;
    private int _nowCalls;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _warnings = new List<string>();
        _logger.When(l => l.LogWarning(Arg.Any<string>())).Do(call => _warnings.Add(call.Arg<string>()));
        _days = 0.0;
        _now = default;
        _daysCalls = 0;
        _nowCalls = 0;
    }

    private double DaysSince(CampaignTime start)
    {
        _daysCalls++;
        return _days;
    }

    private CampaignTime Now()
    {
        _nowCalls++;
        return _now;
    }

    private void Repair(Dictionary<string, RefugeData> book) =>
        RefugeBookRepair.Repair(book, DaysSince, Now, _logger);

    private static Dictionary<string, RefugeData> Book(string key, RefugeData row) =>
        new Dictionary<string, RefugeData> { [key] = row };

    [TestMethod]
    public void Repair_NullRows_RemovedWithOneWarningNamingTheCount()
    {
        var healthy = new RefugeData { PartyId = "r1", Established = true };
        var book = new Dictionary<string, RefugeData>
        {
            ["r1"] = healthy,
            ["r_null"] = null,
            ["r_null2"] = null,
        };

        Repair(book);

        Assert.AreEqual(1, book.Count, "null rows are removed from the same dictionary");
        Assert.AreSame(healthy, book["r1"]);
        Assert.AreEqual(1, _warnings.Count);
        StringAssert.Contains(_warnings[0], "dropped 2 null book row(s)");
    }

    [TestMethod]
    public void Repair_DivergentInnerPartyId_CanonicalizedToTheKey()
    {
        var row = new RefugeData { PartyId = "r2", Established = true };

        Repair(Book("r1", row));

        Assert.AreEqual("r1", row.PartyId);
        Assert.AreEqual(1, _warnings.Count);
        StringAssert.Contains(_warnings[0], "canonicalized to the key");
    }

    [TestMethod]
    public void Repair_UnknownTier_ResetToRefuge()
    {
        var row = new RefugeData { PartyId = "r1", Tier = 99 };

        Repair(Book("r1", row));

        Assert.AreEqual(RefugeTier.Refuge, row.TierEnum);
        Assert.AreEqual(1, _warnings.Count);
        StringAssert.Contains(_warnings[0], "unknown tier 99");
    }

    [TestMethod]
    public void Repair_StrongholdTier_Kept()
    {
        var row = new RefugeData { PartyId = "r1", TierEnum = RefugeTier.Stronghold };

        Repair(Book("r1", row));

        Assert.AreEqual(RefugeTier.Stronghold, row.TierEnum);
        Assert.AreEqual(0, _warnings.Count);
    }

    [TestMethod]
    public void Repair_NegativeMilitia_FlooredAtZeroWithoutAWarning()
    {
        var row = new RefugeData { PartyId = "r1", MilitiaAdded = -5, MilitiaPreRallyCount = -3 };

        Repair(Book("r1", row));

        Assert.AreEqual(0, row.MilitiaAdded);
        Assert.AreEqual(0, row.MilitiaPreRallyCount);
        Assert.AreEqual(0, _warnings.Count);
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(float.NegativeInfinity)]
    public void Repair_NonFiniteTargetOnBuildingRow_FlooredToTheSharedMinimum(float target)
    {
        var row = new RefugeData { PartyId = "r1", Building = true, BuildTargetHours = target };

        Repair(Book("r1", row));

        Assert.AreEqual(RefugeBookRepair.MinBuildTargetHours, row.BuildTargetHours);
        Assert.AreEqual(0.1f, row.BuildTargetHours);
        Assert.AreEqual(1, _warnings.Count);
        StringAssert.Contains(_warnings[0], "non-finite BuildTargetHours");
    }

    [TestMethod]
    public void Repair_NonFiniteTargetOnIdleRow_LeftAlone()
    {
        var row = new RefugeData { PartyId = "r1", Building = false, BuildTargetHours = float.NaN };

        Repair(Book("r1", row));

        Assert.IsTrue(float.IsNaN(row.BuildTargetHours), "only a building row's target is repaired");
        Assert.AreEqual(0, _warnings.Count);
    }

    [DataTestMethod]
    [DataRow(0f)]
    [DataRow(-3f)]
    public void Repair_ZeroOrNegativeTargetOnBuildingRow_LeftAlone(float target)
    {
        // BuildProgress already resolves these as done; only non-finite targets are absorbing.
        var row = new RefugeData { PartyId = "r1", Building = true, BuildTargetHours = target };

        Repair(Book("r1", row));

        Assert.AreEqual(target, row.BuildTargetHours);
        Assert.AreEqual(0, _warnings.Count);
    }

    [TestMethod]
    public void Repair_IdleRows_NeverReadTheClock()
    {
        // RefugeServiceTests' HeroMergeProbeService loads idle rows through a service that
        // overrides neither clock seam; an eager read would reach CampaignTime.Now there.
        _days = -41.0;
        var book = new Dictionary<string, RefugeData>
        {
            ["r1"] = new RefugeData { PartyId = "r1", Established = true },
            ["r2"] = new RefugeData { PartyId = "r2", BuildTargetHours = float.NaN },
        };

        Repair(book);

        Assert.AreEqual(0, _daysCalls, "daysSince is read only for a building row");
        Assert.AreEqual(0, _nowCalls, "now is read only for a future start");
        Assert.AreEqual(0, _warnings.Count);
    }

    [DataTestMethod]
    [DataRow(2.0)]
    [DataRow(0.0)]
    public void Repair_BuildingRowStartNotInTheFuture_ReadsDaysSinceButNotNow(double days)
    {
        // 0.0 is the boundary: the future test is a strict "< 0.0".
        _days = days;
        var row = new RefugeData { PartyId = "r1", Building = true, BuildTargetHours = 6f };

        Repair(Book("r1", row));

        Assert.AreEqual(1, _daysCalls);
        Assert.AreEqual(0, _nowCalls);
        Assert.AreEqual(0, _warnings.Count);
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void Repair_BuildingRowStartedInTheFuture_ResetsToNow()
    {
        // Never (long.MaxValue ticks) reads no tick static. Hours and Days multiply by the statics
        // CampaignTime.Initialize sets, so with no campaign they give Zero for every finite argument.
        // Never and Equals are engine members, which is why this method needs the game.
        _days = -41.0;
        _now = CampaignTime.Never;
        var row = new RefugeData { PartyId = "r1", Established = true, Building = true, BuildTargetHours = 6f };

        Repair(Book("r1", row));

        Assert.IsTrue(row.BuildStartTime.Equals(CampaignTime.Never), "the reset lands on now()");
        Assert.AreEqual(1, _daysCalls);
        Assert.AreEqual(1, _nowCalls);
        Assert.AreEqual(1, _warnings.Count);
        StringAssert.Contains(_warnings[0], "future BuildStartTime");
    }

    [TestMethod]
    public void Repair_EveryDefectOnOneRow_WarnsInTheFixedOrder()
    {
        _days = -1.0;
        var row = new RefugeData
        {
            PartyId = "r2",
            Tier = 99,
            Building = true,
            BuildTargetHours = float.NaN,
            MilitiaAdded = -1,
            MilitiaPreRallyCount = -1,
        };

        Repair(Book("r1", row));

        Assert.AreEqual(4, _warnings.Count);
        StringAssert.Contains(_warnings[0], "canonicalized to the key");
        StringAssert.Contains(_warnings[1], "row 'r1' had unknown tier 99");
        StringAssert.Contains(_warnings[2], "row 'r1' had non-finite BuildTargetHours");
        StringAssert.Contains(_warnings[3], "row 'r1' had a future BuildStartTime");
        Assert.AreEqual("r1", row.PartyId);
        Assert.AreEqual(RefugeTier.Refuge, row.TierEnum);
        Assert.AreEqual(0.1f, row.BuildTargetHours);
        Assert.AreEqual(1, _nowCalls);
        Assert.AreEqual(0, row.MilitiaAdded);
        Assert.AreEqual(0, row.MilitiaPreRallyCount);
    }
}
