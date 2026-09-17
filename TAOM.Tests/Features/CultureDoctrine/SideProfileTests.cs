using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CultureDoctrine.Domain;

namespace TAOM.Tests.Features.CultureDoctrine;

/// <summary>
/// One battle side's doctrine inputs, folded from its <c>IBattleCombatant</c>s: the culture
/// fielding the most troops and the best Tactics skill on the side (the same maximum vanilla's
/// <c>MissionCombatantsLogic.EarlyStart</c> uses to gate its own registration).
/// </summary>
[TestClass]
public class SideProfileTests
{
    [TestMethod]
    public void From_SingleCombatant_TakesItsCultureTroopsAndSkill()
    {
        var profile = SideProfile.From(new[] { new SideCombatant("erebor", 120, 40) });

        Assert.AreEqual("erebor", profile.CultureId);
        Assert.AreEqual(120, profile.TroopCount);
        Assert.AreEqual(40, profile.TacticsSkill);
    }

    [TestMethod]
    public void From_MixedArmy_PicksTheCultureWithMostTroops()
    {
        var profile = SideProfile.From(new[]
        {
            new SideCombatant("gondor", 80, 10),
            new SideCombatant("vlandia", 150, 60),
            new SideCombatant("gondor", 90, 5),
        });

        // Gondor fields 170 across two parties against Rohan's 150: parties of one culture add up.
        Assert.AreEqual("gondor", profile.CultureId);
        Assert.AreEqual(320, profile.TroopCount);
    }

    [TestMethod]
    public void From_TacticsSkill_IsTheSideMaximumRegardlessOfCulture()
    {
        var profile = SideProfile.From(new[]
        {
            new SideCombatant("gondor", 300, 10),
            new SideCombatant("vlandia", 20, 90),
        });

        Assert.AreEqual("gondor", profile.CultureId);
        Assert.AreEqual(90, profile.TacticsSkill);
    }

    [TestMethod]
    public void From_TiedTroopCounts_KeepsTheFirstCultureListed()
    {
        var profile = SideProfile.From(new[]
        {
            new SideCombatant("mordor", 100, 0),
            new SideCombatant("isengard", 100, 0),
        });

        Assert.AreEqual("mordor", profile.CultureId, "combatant order is the engine's (leader first), so a tie goes to the leader");
    }

    [TestMethod]
    public void From_NullOrBlankCulture_IsIgnoredForTheMajorityButCountedInTroops()
    {
        var profile = SideProfile.From(new[]
        {
            new SideCombatant(null, 500, 0),
            new SideCombatant("", 50, 0),
            new SideCombatant("erebor", 10, 0),
        });

        Assert.AreEqual("erebor", profile.CultureId);
        Assert.AreEqual(560, profile.TroopCount);
    }

    [TestMethod]
    public void From_NoCombatants_HasNoCultureAndZeroes()
    {
        var profile = SideProfile.From(new SideCombatant[0]);

        Assert.IsNull(profile.CultureId);
        Assert.AreEqual(0, profile.TroopCount);
        Assert.AreEqual(0, profile.TacticsSkill);
    }

    [TestMethod]
    public void From_SkillFloor_RaisesTheSkillButNotTheCultureOrTroops()
    {
        var profile = SideProfile.From(new[] { new SideCombatant("erebor", 40, 10) }, tacticsSkillFloor: 60);

        Assert.AreEqual("erebor", profile.CultureId);
        Assert.AreEqual(40, profile.TroopCount);
        Assert.AreEqual(60, profile.TacticsSkill, "the side's best commander gates registration for every team on the side");
    }

    [TestMethod]
    public void From_SkillFloorBelowOwnSkill_KeepsOwnSkill()
    {
        Assert.AreEqual(90, SideProfile.From(new[] { new SideCombatant("erebor", 40, 90) }, tacticsSkillFloor: 20).TacticsSkill);
        Assert.AreEqual(0, SideProfile.From(new SideCombatant[0], tacticsSkillFloor: -5).TacticsSkill);
    }

    [TestMethod]
    public void From_NegativeCounts_AreTreatedAsZero()
    {
        var profile = SideProfile.From(new[] { new SideCombatant("erebor", -5, -3) });

        Assert.AreEqual("erebor", profile.CultureId);
        Assert.AreEqual(0, profile.TroopCount);
        Assert.AreEqual(0, profile.TacticsSkill);
    }
}
