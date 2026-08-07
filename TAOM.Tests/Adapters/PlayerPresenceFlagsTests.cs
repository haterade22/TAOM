using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Adapters;

namespace TAOM.Tests.Adapters;

/// <summary>
/// <see cref="PlayerPresenceFlags"/> (pump path, struct) and <see cref="PlayerPresenceSnapshot"/>
/// (slow path, class) carry the same fields and duplicate the <c>LooksParked</c> predicate. That
/// duplication is deliberate and temporary — collapsing them changes null semantics across four
/// services, because callers written against a class use <c>GetPresence()?.X ?? true</c> and mocks
/// returning <c>null</c> would become <c>default</c>, silently flipping several guards.
///
/// This test is the guard until the collapse: if the two predicates ever disagree, the pump and the
/// reconciler would disagree about whether the player is parked, which is exactly the class of
/// "two sources of truth" bug the whole feature was rewritten to eliminate.
/// </summary>
[TestClass]
public class PlayerPresenceFlagsTests
{
    [DataTestMethod]
    [DataRow(false, false, false)]
    [DataRow(false, false, true)]
    [DataRow(false, true, false)]
    [DataRow(false, true, true)]
    [DataRow(true, false, false)]
    [DataRow(true, false, true)]
    [DataRow(true, true, false)]
    [DataRow(true, true, true)]
    public void LooksParked_MatchesPlayerPresenceSnapshotForEveryFlagCombination(
        bool mainPartyExists, bool isActive, bool isVisible)
    {
        var flags = new PlayerPresenceFlags(mainPartyExists, isActive: isActive, isVisible: isVisible);
        var snapshot = new PlayerPresenceSnapshot(mainPartyExists, isActive: isActive, isVisible: isVisible);

        Assert.AreEqual(
            snapshot.LooksParked,
            flags.LooksParked,
            $"LooksParked diverged for exists={mainPartyExists} active={isActive} visible={isVisible}");
    }

    [TestMethod]
    public void MissingMainParty_IsNeverParked()
    {
        Assert.IsFalse(new PlayerPresenceFlags(mainPartyExists: false).LooksParked);
    }

    [TestMethod]
    public void IsInSettlement_TracksTheSettlementId()
    {
        Assert.IsFalse(new PlayerPresenceFlags(true).IsInSettlement);
        Assert.IsTrue(new PlayerPresenceFlags(true, settlementId: "town_A1").IsInSettlement);
    }
}
