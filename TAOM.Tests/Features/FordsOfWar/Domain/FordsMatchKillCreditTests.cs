using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar.Domain;
using static TAOM.Tests.Features.FordsOfWar.Domain.FordsMatchFixture;

namespace TAOM.Tests.Features.FordsOfWar.Domain;

/// <summary>A killer that died first (mutual kills, an arrow from a dead archer) is still paid, from a bounded death record.</summary>
[TestClass]
public class FordsMatchKillCreditTests
{
    [TestMethod]
    public void OnUnitKilled_MutualKills_PayBothOwners()
    {
        var match = Create();
        match.RegisterUnit(1, Unit(10, MatchSide.West, WestSlot, value: 200));
        match.RegisterUnit(2, Unit(20, MatchSide.East, EastSlot, value: 200));

        match.OnUnitKilled(Key(10), Key(20));
        match.OnUnitKilled(Key(20), Key(10));

        Assert.AreEqual(1070, match.GetView(WestSlot).Gold);
        Assert.AreEqual(1070, match.GetView(EastSlot).Gold);
    }

    [TestMethod]
    public void OnUnitKilled_KillerAlreadyDead_StillPaysItsOwner()
    {
        var match = Create();
        match.RegisterUnit(1, Unit(10, MatchSide.West, WestSlot));
        match.RegisterUnit(2, Unit(20, MatchSide.East, EastSlot, value: 200));
        match.OnUnitKilled(Key(10), null);

        match.OnUnitKilled(Key(20), Key(10));

        Assert.AreEqual(1070, match.GetView(WestSlot).Gold);
    }

    [TestMethod]
    public void OnUnitKilled_DeadKillerOnTheVictimsSide_PaysNothing()
    {
        var match = Create();
        match.RegisterUnit(1, Unit(10, MatchSide.West, WestSlot));
        match.RegisterUnit(2, Unit(11, MatchSide.West, WestSlot, value: 200));
        match.OnUnitKilled(Key(10), null);

        match.OnUnitKilled(Key(11), Key(10));

        Assert.AreEqual(1000, match.GetView(WestSlot).Gold);
    }

    [TestMethod]
    public void OnUnitKilled_KillerDiedWithinTheRecordCap_StillPaid()
    {
        var match = KillerThenDeaths(2047);

        match.OnUnitKilled(Key(20), Key(10));

        Assert.AreEqual(1070, match.GetView(WestSlot).Gold);
    }

    [TestMethod]
    public void OnUnitKilled_KillerEvictedFromTheRecord_PaysNothing()
    {
        var match = KillerThenDeaths(2048);

        match.OnUnitKilled(Key(20), Key(10));

        Assert.AreEqual(1000, match.GetView(WestSlot).Gold);
    }

    /// <summary>West unit 10 dies first, then <paramref name="others"/> more West units die; East unit 20 is still alive.</summary>
    private static FordsMatch KillerThenDeaths(int others)
    {
        var match = Create();
        match.RegisterUnit(1, Unit(10, MatchSide.West, WestSlot));
        match.RegisterUnit(2, Unit(20, MatchSide.East, EastSlot, value: 200));
        match.OnUnitKilled(Key(10), null);
        for (var i = 0; i < others; i++)
        {
            match.RegisterUnit(3, Unit(100 + i, MatchSide.West, WestSlot));
            match.OnUnitKilled(Key(100 + i), null);
        }

        return match;
    }
}
