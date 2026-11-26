using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LotrIssues;
using TAOM.Features.LotrIssues.Domain;

namespace TAOM.Tests.Features.LotrIssues;

[TestClass]
public class LotrQuestBandRulesTests
{
    private static LotrBandClanOption Opt(string id, float distance, bool creature = false)
        => new LotrBandClanOption(id, creature, distance);

    [TestMethod]
    public void PickClan_NearestHideoutWins()
        => Assert.AreEqual("rhun_raiders", LotrQuestBandRules.PickClan(new[] { Opt("dunland_raiders", 90f), Opt("rhun_raiders", 4f) }));

    [TestMethod]
    public void PickClan_SkipsCreatureClans()
        => Assert.AreEqual("harad_raiders", LotrQuestBandRules.PickClan(
            new[] { Opt("mirkwood_spiders", 1f, creature: true), Opt("harad_raiders", 50f) }));

    [TestMethod]
    public void PickClan_NoHideoutAnywhere_FallsBackToFirstEligible()
        => Assert.AreEqual("a_raiders", LotrQuestBandRules.PickClan(
            new[] { Opt("wild_trolls", float.PositiveInfinity, creature: true), Opt("a_raiders", float.PositiveInfinity), Opt("b_raiders", float.PositiveInfinity) }));

    [TestMethod]
    public void PickClan_NaNDistance_IsSkipped()
        => Assert.AreEqual("b_raiders", LotrQuestBandRules.PickClan(new[] { Opt("a_raiders", float.NaN), Opt("b_raiders", 30f) }));

    [TestMethod]
    public void PickClan_NegativeDistance_IsSkipped()
        => Assert.AreEqual("b_raiders", LotrQuestBandRules.PickClan(new[] { Opt("a_raiders", -1f), Opt("b_raiders", 30f) }));

    [TestMethod]
    public void PickClan_OnlyCreatureClans_ReturnsNull()
        => Assert.IsNull(LotrQuestBandRules.PickClan(new[] { Opt("wild_trolls", 1f, creature: true) }));

    [TestMethod]
    public void PickClan_NullOrEmpty_ReturnsNull()
    {
        Assert.IsNull(LotrQuestBandRules.PickClan(null));
        Assert.IsNull(LotrQuestBandRules.PickClan(new LotrBandClanOption[0]));
    }

    [DataTestMethod]
    [DataRow(true, false, true)]
    [DataRow(false, true, true)]
    [DataRow(false, false, false)]
    public void IsDefeatedByPlayer_MainPartyOrArmyLeader_True_StrangerFalse(bool mainParty, bool army, bool expected)
        => Assert.AreEqual(expected, LotrQuestBandRules.IsDefeatedByPlayer(mainParty, army));
}
