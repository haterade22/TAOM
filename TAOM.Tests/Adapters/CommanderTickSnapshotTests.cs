using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Adapters;

namespace TAOM.Tests.Adapters;

/// <summary>
/// Contract pins for the value type the per-tick pump consumes. The pump branches on
/// <c>MapEventToken != 0</c> and on <c>IsFollowable</c>, so both need to mean exactly one thing.
/// </summary>
[TestClass]
public class CommanderTickSnapshotTests
{
    [TestMethod]
    public void Missing_HasExistsFalseAndZeroMapEventToken()
    {
        var missing = CommanderTickSnapshot.Missing;

        Assert.IsFalse(missing.Exists);
        Assert.AreEqual(0, missing.MapEventToken, "0 must mean 'no map event' — the pump branches on it");
        Assert.IsFalse(missing.IsFollowable);
        Assert.IsFalse(missing.HasParty);
    }

    [TestMethod]
    public void MapEventToken_ZeroMeansNoMapEvent()
    {
        var noEvent = new CommanderTickSnapshot(exists: true, isAlive: true, partyId: "p", partyIsActive: true);
        var inEvent = new CommanderTickSnapshot(exists: true, isAlive: true, partyId: "p", partyIsActive: true,
            partyIsInMapEvent: true, mapEventToken: 7);

        Assert.AreEqual(0, noEvent.MapEventToken);
        Assert.AreNotEqual(0, inEvent.MapEventToken);
    }

    [DataTestMethod]
    [DataRow(false, true, false, "p", true, DisplayName = "missing hero")]
    [DataRow(true, false, false, "p", true, DisplayName = "dead")]
    [DataRow(true, true, true, "p", true, DisplayName = "prisoner")]
    [DataRow(true, true, false, null, true, DisplayName = "no party")]
    [DataRow(true, true, false, "p", false, DisplayName = "inactive party")]
    public void IsFollowable_FalseWheneverAnyRequirementFails(
        bool exists, bool alive, bool prisoner, string partyId, bool partyActive)
    {
        var s = new CommanderTickSnapshot(exists, alive, prisoner, partyId, partyActive);

        Assert.IsFalse(s.IsFollowable);
    }

    [TestMethod]
    public void IsFollowable_TrueOnlyWhenAliveFreeAndFieldingAnActiveParty()
    {
        var s = new CommanderTickSnapshot(exists: true, isAlive: true, isPrisoner: false,
            partyId: "lord_party_1", partyIsActive: true);

        Assert.IsTrue(s.IsFollowable);
    }
}
