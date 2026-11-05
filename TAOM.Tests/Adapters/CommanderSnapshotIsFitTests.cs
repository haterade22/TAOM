using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Adapters;

namespace TAOM.Tests.Adapters;

[TestClass]
public class CommanderSnapshotIsFitTests
{
    [TestMethod]
    [DataRow(true, true, false, "p", true, true)]
    [DataRow(false, true, false, "p", true, false)]
    [DataRow(true, false, false, "p", true, false)]
    [DataRow(true, true, true, "p", true, false)]
    [DataRow(true, true, false, null, true, false)]
    [DataRow(true, true, false, "", true, false)]
    [DataRow(true, true, false, "p", false, false)]
    public void IsFit_NeedsExistingAliveFreeCommanderWithAnActiveParty(
        bool exists, bool alive, bool prisoner, string partyId, bool active, bool expected)
    {
        var snapshot = new CommanderSnapshot(exists, isAlive: alive, isPrisoner: prisoner, partyId: partyId, partyIsActive: active);

        Assert.AreEqual(expected, snapshot.IsFit);

        var tick = new CommanderTickSnapshot(exists, isAlive: alive, isPrisoner: prisoner, partyId: partyId, partyIsActive: active);
        Assert.AreEqual(expected, tick.IsFollowable);
    }

    [TestMethod]
    public void IsFit_MissingSnapshot_IsFalse() => Assert.IsFalse(CommanderSnapshot.Missing.IsFit);
}
