using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.PlayerSwitcher;
using TAOM.Features.PlayerSwitcher.Domain;

namespace TAOM.Tests.Features.PlayerSwitcher;

/// <summary>
/// Issue #514. The planner is a pure function: a chosen row plus the active policy becomes the
/// handover contract. It exists as its own type so the path choice is provable without touching
/// the engine, and so HeroSwitchService has data to dispatch on rather than a branch to re-derive.
/// </summary>
[TestClass]
public class SwitchPlannerTests
{
    private SwitchPlanner _sut = null!;

    [TestInitialize]
    public void Setup() => _sut = new SwitchPlanner();

    private static HeroPickRow Row(string id, bool hasClan, HeroPickerGroup group = HeroPickerGroup.ClanLeaders)
        => new HeroPickRow(id, "Name", group, race: 0, isFemale: false, isLeader: true, hasClan: hasClan);

    [TestMethod]
    public void AHeroWithAClan_TakesOverThatIdentity()
    {
        var plan = _sut.Plan(Row("dain", hasClan: true), PlayerSwitchPolicy.Default, "career_warrior");

        Assert.AreEqual(SwitchPath.AssumeIdentity, plan.Path);
        Assert.AreEqual("dain", plan.HeroId);
        Assert.IsTrue(plan.IsValid);
    }

    [TestMethod]
    public void AClanlessHero_IsAdoptedIntoThePlayerClan()
    {
        var plan = _sut.Plan(
            Row("drifter", hasClan: false, HeroPickerGroup.Wanderers),
            PlayerSwitchPolicy.Default,
            "career_warrior");

        Assert.AreEqual(SwitchPath.AdoptIntoPlayerClan, plan.Path,
            "the player keeps the clan they named and the banner they designed");
    }

    [TestMethod]
    public void TheChosenCareerIsCarriedIntoThePlan()
    {
        var plan = _sut.Plan(Row("dain", true), PlayerSwitchPolicy.Default, "career_smith");

        Assert.AreEqual("career_smith", plan.CareerId,
            "the career is the one character-creation choice that survives the handover");
    }

    [TestMethod]
    public void GoldTransferFollowsThePolicy()
    {
        var off = _sut.Plan(Row("dain", true), PlayerSwitchPolicy.Default, "c");
        var on = _sut.Plan(Row("dain", true), new PlayerSwitchPolicy(true, true, false, transferStartingGold: true), "c");

        Assert.IsFalse(off.TransferGold, "an established lord is already funded; off is the default");
        Assert.IsTrue(on.TransferGold);
    }

    [TestMethod]
    public void AnEmptyRow_ProducesNoPlan()
    {
        var plan = _sut.Plan(default, PlayerSwitchPolicy.Default, "c");

        Assert.IsFalse(plan.IsValid);
        Assert.AreEqual(string.Empty, plan.HeroId);
    }

    [TestMethod]
    public void WhenTheFeatureIsDisabled_NoPlanIsProduced()
    {
        var plan = _sut.Plan(Row("dain", true), PlayerSwitchPolicy.Disabled, "c");

        Assert.IsFalse(plan.IsValid);
    }

    [TestMethod]
    public void ANullCareerId_BecomesEmptyRatherThanNull()
    {
        var plan = _sut.Plan(Row("dain", true), PlayerSwitchPolicy.Default, null!);

        Assert.AreEqual(string.Empty, plan.CareerId, "downstream re-keying compares strings");
    }
}
