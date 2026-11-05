using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Tests.Features.SeatOfSeeing;

/// <summary>Who the player may take after the fall (build-spec section 9): TAOM's own eligibility rule, one test per refusal and one for each edge of the health test.</summary>
[TestClass]
public class SeatTakeRuleTests
{
    // A soldier who passes every test; each test below changes one fact.
    private static SeatSoldierFacts Soldier() => new(
        Human: true, Active: true, OnPlayerTeam: true, UsingObject: false, Retreating: false, FadingOut: false, HealthShare: 1f,
        Hero: false, HeroOfPlayersParty: false, PlatformCrew: false, InFormation: true);

    [TestMethod]
    public void Check_AHealthyPlayerSoldier_IsEligible()
    {
        Assert.AreEqual(SeatTakeRefusal.None, SeatTakeRule.Check(Soldier()));
    }

    [TestMethod]
    public void Check_NotHuman_IsRefused() => Assert.AreEqual(SeatTakeRefusal.NotHuman, SeatTakeRule.Check(Soldier() with { Human = false }));

    [TestMethod]
    public void Check_NotActive_IsRefused() => Assert.AreEqual(SeatTakeRefusal.NotActive, SeatTakeRule.Check(Soldier() with { Active = false }));

    [TestMethod]
    public void Check_OnAnotherTeam_IsRefused() => Assert.AreEqual(SeatTakeRefusal.OtherTeam, SeatTakeRule.Check(Soldier() with { OnPlayerTeam = false }));

    [TestMethod]
    public void Check_UsingAnObject_IsRefused() => Assert.AreEqual(SeatTakeRefusal.UsingObject, SeatTakeRule.Check(Soldier() with { UsingObject = true }));

    [TestMethod]
    public void Check_Retreating_IsRefused() => Assert.AreEqual(SeatTakeRefusal.Retreating, SeatTakeRule.Check(Soldier() with { Retreating = true }));

    [TestMethod]
    public void Check_FadingOut_IsRefused() => Assert.AreEqual(SeatTakeRefusal.FadingOut, SeatTakeRule.Check(Soldier() with { FadingOut = true }));

    [TestMethod]
    public void Check_PlatformCrew_IsRefused() => Assert.AreEqual(SeatTakeRefusal.PlatformCrew, SeatTakeRule.Check(Soldier() with { PlatformCrew = true }));

    [TestMethod]
    public void Check_ASoldierOfNoFormation_IsRefused() => Assert.AreEqual(SeatTakeRefusal.NoFormation, SeatTakeRule.Check(Soldier() with { InFormation = false }));

    [DataTestMethod]
    [DataRow(0.25f, true)]
    [DataRow(0.2499f, false)]
    [DataRow(1f, true)]
    [DataRow(0f, false)]
    public void Check_HealthShare_NeedsAQuarterOrMore(float share, bool eligible)
    {
        var refusal = SeatTakeRule.Check(Soldier() with { HealthShare = share });

        Assert.AreEqual(eligible ? SeatTakeRefusal.None : SeatTakeRefusal.Wounded, refusal, "share " + share);
    }

    [TestMethod]
    public void Check_AHealthShareThatIsNotANumber_IsRefusedAsWounded()
    {
        Assert.AreEqual(SeatTakeRefusal.Wounded, SeatTakeRule.Check(Soldier() with { HealthShare = float.NaN }));
        Assert.AreEqual(SeatTakeRefusal.Wounded, SeatTakeRule.Check(Soldier() with { HealthShare = float.NegativeInfinity }));
    }

    [TestMethod]
    public void Check_AHeroOfTheirOwnParty_IsEligible()
    {
        Assert.AreEqual(SeatTakeRefusal.None, SeatTakeRule.Check(Soldier() with { Hero = true, HeroOfPlayersParty = true }));
    }

    // With no campaign (custom battle) the adapter reports HeroOfPlayersParty false for everyone, and vanilla refuses every hero (Mission.cs:6395).
    [TestMethod]
    public void Check_AHeroOfAnotherParty_OrAnyHeroWithNoCampaign_IsRefused()
    {
        Assert.AreEqual(SeatTakeRefusal.Hero, SeatTakeRule.Check(Soldier() with { Hero = true, HeroOfPlayersParty = false }));
    }

    [TestMethod]
    public void Check_TheFirstFailedTestNamesTheRefusal()
    {
        var wreck = new SeatSoldierFacts(Human: false, Active: false, OnPlayerTeam: false, UsingObject: true, Retreating: true, FadingOut: true,
            HealthShare: 0f, Hero: true, HeroOfPlayersParty: false, PlatformCrew: true, InFormation: false);

        Assert.AreEqual(SeatTakeRefusal.NotHuman, SeatTakeRule.Check(wreck));
        Assert.AreEqual(SeatTakeRefusal.NotActive, SeatTakeRule.Check(wreck with { Human = true }));
        Assert.AreEqual(SeatTakeRefusal.OtherTeam, SeatTakeRule.Check(wreck with { Human = true, Active = true }));
        Assert.AreEqual(SeatTakeRefusal.PlatformCrew, SeatTakeRule.Check(wreck with { Human = true, Active = true, OnPlayerTeam = true, UsingObject = false, Retreating = false, FadingOut = false }));
    }

    [DataTestMethod]
    [DataRow((int)SeatTakeRefusal.NotHuman, "not-human")]
    [DataRow((int)SeatTakeRefusal.NotActive, "not-active")]
    [DataRow((int)SeatTakeRefusal.OtherTeam, "other-team")]
    [DataRow((int)SeatTakeRefusal.UsingObject, "using-object")]
    [DataRow((int)SeatTakeRefusal.Retreating, "retreating")]
    [DataRow((int)SeatTakeRefusal.FadingOut, "fading-out")]
    [DataRow((int)SeatTakeRefusal.Wounded, "wounded")]
    [DataRow((int)SeatTakeRefusal.Hero, "hero")]
    [DataRow((int)SeatTakeRefusal.PlatformCrew, "platform-crew")]
    [DataRow((int)SeatTakeRefusal.NoFormation, "no-formation")]
    public void Reason_IsTheWordTheSkipLineCarries(int refusal, string word)
    {
        Assert.AreEqual(word, SeatTakeRule.Reason((SeatTakeRefusal)refusal));
    }

    [TestMethod]
    public void Nearest_PicksTheClosestEligibleCandidate()
    {
        var list = new List<SeatTakeCandidate>
        {
            new(9f, SeatTakeRefusal.None),
            new(4f, SeatTakeRefusal.Wounded),
            new(16f, SeatTakeRefusal.None),
            new(1f, SeatTakeRefusal.None),
        };

        Assert.AreEqual(3, SeatTakeRule.Nearest(list));
    }

    [TestMethod]
    public void Nearest_SkipsARefusedCandidateEvenWhenItIsTheClosest()
    {
        var list = new List<SeatTakeCandidate> { new(1f, SeatTakeRefusal.Retreating), new(25f, SeatTakeRefusal.None) };

        Assert.AreEqual(1, SeatTakeRule.Nearest(list));
    }

    [TestMethod]
    public void Nearest_NobodyEligible_IsMinusOne()
    {
        Assert.AreEqual(-1, SeatTakeRule.Nearest(new List<SeatTakeCandidate> { new(1f, SeatTakeRefusal.Hero) }));
        Assert.AreEqual(-1, SeatTakeRule.Nearest(new List<SeatTakeCandidate>()));
    }

    [TestMethod]
    public void Nearest_ADistanceThatIsNotANumber_IsNeverChosen()
    {
        var list = new List<SeatTakeCandidate> { new(float.NaN, SeatTakeRefusal.None), new(400f, SeatTakeRefusal.None) };

        Assert.AreEqual(1, SeatTakeRule.Nearest(list));
        Assert.AreEqual(-1, SeatTakeRule.Nearest(new List<SeatTakeCandidate> { new(float.NaN, SeatTakeRefusal.None) }));
    }
}
