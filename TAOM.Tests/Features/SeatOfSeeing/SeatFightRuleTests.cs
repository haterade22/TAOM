using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Tests.Features.SeatOfSeeing;

/// <summary>The hero hand-off's decisions (build-spec section 8): what is wanted, what is owed, why a hand-off is refused and when the retreat check runs.</summary>
[TestClass]
public class SeatFightRuleTests
{
    [DataTestMethod]
    [DataRow((int)SeatState.Off, false, true, false)]
    [DataRow((int)SeatState.Rising, false, true, true)]
    [DataRow((int)SeatState.Seated, false, true, true)]
    [DataRow((int)SeatState.Seated, true, true, false)]
    [DataRow((int)SeatState.Seated, false, false, false)]
    [DataRow((int)SeatState.Rising, true, false, false)]
    public void Wanted_NeedsTheSeatUpNoFaultAndTheChoiceOfThisEntry(int state, bool faulted, bool chosen, bool wanted)
    {
        Assert.AreEqual(wanted, SeatFightRule.Wanted((SeatState)state, faulted, chosen));
    }

    [DataTestMethod]
    [DataRow(true, false, (int)SeatFightAction.Begin)]
    [DataRow(false, true, (int)SeatFightAction.End)]
    [DataRow(true, true, (int)SeatFightAction.None)]
    [DataRow(false, false, (int)SeatFightAction.None)]
    public void Owed_IsWhateverMakesActualMatchWanted(bool wanted, bool actual, int action)
    {
        Assert.AreEqual((SeatFightAction)action, SeatFightRule.Owed(wanted, actual));
    }

    // An entry and a leave with no tick between them: the hero was never handed over, so nothing is owed on the leave.
    [TestMethod]
    public void Owed_AnEntryAndALeaveWithNoTickBetween_OwesNothing()
    {
        var wantedAtEntry = SeatFightRule.Wanted(SeatState.Rising, faulted: false, chosen: true);
        var wantedAfterLeave = SeatFightRule.Wanted(SeatState.Off, faulted: false, chosen: true);
        var actual = false;

        Assert.IsTrue(wantedAtEntry);
        Assert.AreEqual(SeatFightAction.None, SeatFightRule.Owed(wantedAfterLeave, actual));
    }

    [TestMethod]
    public void BeginRefusal_AHeroWhoCanBeHandedOver_IsNone()
    {
        var facts = new SeatFightFacts(MissionEnded: false, MissionEnding: false, HeroActive: true, HeroIsPlayerControlled: true);

        Assert.AreEqual(SeatFightRefusal.None, SeatFightRule.BeginRefusal(facts));
    }

    [DataTestMethod]
    [DataRow(true, false, true, true, (int)SeatFightRefusal.MissionEnded)]
    [DataRow(false, true, true, true, (int)SeatFightRefusal.MissionEnded)]
    [DataRow(false, false, false, true, (int)SeatFightRefusal.NoHero)]
    [DataRow(false, false, true, false, (int)SeatFightRefusal.NotPlayerControlled)]
    public void BeginRefusal_EachGuard_NamesItsReason(bool ended, bool ending, bool active, bool player, int refusal)
    {
        var facts = new SeatFightFacts(ended, ending, active, player);

        Assert.AreEqual((SeatFightRefusal)refusal, SeatFightRule.BeginRefusal(facts));
    }

    [TestMethod]
    public void BeginRefusal_AMissionEndingBeatsAMissingHero()
    {
        var facts = new SeatFightFacts(MissionEnded: false, MissionEnding: true, HeroActive: false, HeroIsPlayerControlled: false);

        Assert.AreEqual(SeatFightRefusal.MissionEnded, SeatFightRule.BeginRefusal(facts));
    }

    [DataTestMethod]
    [DataRow((int)SeatFightRefusal.MissionEnded, "mission-ended")]
    [DataRow((int)SeatFightRefusal.NoHero, "no-hero")]
    [DataRow((int)SeatFightRefusal.NotPlayerControlled, "not-player")]
    public void Reason_IsTheWordTheCheckLineCarries(int refusal, string word)
    {
        Assert.AreEqual(word, SeatFightRule.Reason((SeatFightRefusal)refusal));
    }

    [TestMethod]
    public void RetreatCheckDue_BeforeTheFirstCheck_IsDue()
    {
        Assert.IsTrue(SeatFightRule.RetreatCheckDue(now: 12.5f, last: float.NaN));
    }

    [DataTestMethod]
    [DataRow(10.0f, 10.999f, false)]
    [DataRow(11.0f, 10.0f, true)]
    [DataRow(30.0f, 10.0f, true)]
    [DataRow(10.0f, 10.0f, false)]
    public void RetreatCheckDue_IsOncePerRealSecond(float now, float last, bool due)
    {
        Assert.AreEqual(due, SeatFightRule.RetreatCheckDue(now, last), "now " + now + " last " + last);
    }

    [TestMethod]
    public void RetreatCheckDue_ANowThatIsNotANumber_IsNeverDue()
    {
        Assert.IsFalse(SeatFightRule.RetreatCheckDue(float.NaN, 10f));
        Assert.IsFalse(SeatFightRule.RetreatCheckDue(float.PositiveInfinity, 10f));
        Assert.IsFalse(SeatFightRule.RetreatCheckDue(float.NaN, float.NaN));
    }
}
