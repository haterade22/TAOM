using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Refuge.Domain;

namespace TAOM.Tests.Features.Refuge;

/// <summary>
/// Pure-data coverage for <see cref="RefugeData"/>: the BuildProgress degenerate-hours guard, the
/// tier int/enum round-trip and the persisted militia bookkeeping defaults. The elapsed-time
/// branch of BuildProgress dereferences CampaignTime.Now and is exercised through the service's
/// BuildProgressOf seam instead.
/// </summary>
[TestClass]
public class RefugeDataTests
{
    [TestMethod]
    public void BuildProgress_ZeroTargetHours_ReportsComplete()
    {
        var data = new RefugeData { BuildTargetHours = 0f };

        Assert.AreEqual(1f, data.BuildProgress(), 0.0001f);
    }

    [TestMethod]
    public void BuildProgress_NegativeTargetHours_ReportsComplete()
    {
        var data = new RefugeData { BuildTargetHours = -3f };

        Assert.AreEqual(1f, data.BuildProgress(), 0.0001f,
            "a poisoned negative target must not produce a negative or divide-through progress");
    }

    [TestMethod]
    public void BuildProgress_NaNTargetHours_ReportsComplete()
    {
        // NaN fails BOTH polarities of a comparison, so the old "<= 0 -> done" guard passed a
        // NaN through to the division and the refuge never finished: a permanent absorbing state
        // consuming a cap slot (Codex round 2 #4). The positive-requirement gate resolves it as
        // done; LoadFrom additionally repairs the persisted field.
        var data = new RefugeData { BuildTargetHours = float.NaN };

        Assert.AreEqual(1f, data.BuildProgress(), 0.0001f);
    }

    [TestMethod]
    public void TierEnum_RoundTripsThroughPersistedInt()
    {
        var data = new RefugeData { TierEnum = RefugeTier.Stronghold };

        Assert.AreEqual(1, data.Tier, "the save field carries the raw int");
        Assert.AreEqual(RefugeTier.Stronghold, data.TierEnum);

        data.Tier = 0;
        Assert.AreEqual(RefugeTier.Refuge, data.TierEnum);
    }

    [TestMethod]
    public void MilitiaBookkeeping_DefaultsToNothingRecorded()
    {
        var data = new RefugeData();

        Assert.AreEqual(0, data.MilitiaAdded);
        Assert.IsNull(data.MilitiaTroopId);
        Assert.AreEqual(0, data.MilitiaPreRallyCount,
            "save-compat: the field deserializes to 0 on a pre-fix save, which reproduces the "
            + "old min(recorded, present) stand-down for only the one battle already in flight");
    }

    [TestMethod]
    public void IsOrphanAdopted_OnlyForUnestablishedNonBuildingRows()
    {
        Assert.IsTrue(new RefugeData { Established = false, Building = false }.IsOrphanAdopted);
        Assert.IsFalse(new RefugeData { Established = false, Building = true }.IsOrphanAdopted,
            "a raising refuge is a normal state, not an orphan");
        Assert.IsFalse(new RefugeData { Established = true, Building = false }.IsOrphanAdopted);
        Assert.IsFalse(new RefugeData { Established = true, Building = true }.IsOrphanAdopted);
    }

    [TestMethod]
    public void IsReady_RequiresEstablishedAndNotBuilding()
    {
        Assert.IsFalse(new RefugeData { Established = false, Building = false }.IsReady);
        Assert.IsFalse(new RefugeData { Established = false, Building = true }.IsReady);
        Assert.IsTrue(new RefugeData { Established = true, Building = false }.IsReady);
        // The stronghold rebuild window: established but building again = not ready, so the
        // defense bonus and militia rally both drop until the rebuild completes.
        Assert.IsFalse(new RefugeData { Established = true, Building = true }.IsReady);
    }
}
