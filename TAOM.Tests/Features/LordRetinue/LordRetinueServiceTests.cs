using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.LordRetinue;

namespace TAOM.Tests.Features.LordRetinue;

[TestClass]
public class LordRetinueServiceTests
{
    private const RetinueRole AllRoles =
        RetinueRole.Scout | RetinueRole.Surgeon | RetinueRole.Engineer | RetinueRole.Quartermaster;

    private ILordRetinueSettingsProvider _settings;
    private LordRetinueService _service;

    [TestInitialize]
    public void Setup()
    {
        _settings = Substitute.For<ILordRetinueSettingsProvider>();
        _settings.IsEnabled.Returns(true);
        _settings.MaxExtraHeroes.Returns(1);
        _service = new LordRetinueService(_settings);
    }

    private static RetinueSkills Skills(int scout = 0, int medicine = 0, int engineering = 0, int steward = 0)
        => new(scout, medicine, engineering, steward);

    private static RetinueParty Party(
        int extra = 0, bool clanBelowPartyLimit = false, RetinueRole filled = RetinueRole.None,
        RetinueSkills? leader = null, params string[] retinue)
        => new(extra, clanBelowPartyLimit, filled, leader ?? Skills(), retinue);

    // An eligible hero: idle, adult, alive and in the party's settlement, with every excluding fact false.
    private static RetinueHero Hero(string id = "h", int scout = 0, int medicine = 0, int engineering = 0, int steward = 0)
        => new(id, Skills(scout, medicine, engineering, steward))
        {
            InPartySettlement = true,
            IsActive = true,
            IsAlive = true,
            IsAdult = true,
            CanMoveToSettlement = true,
            CanHavePartyRole = true,
        };

    // ShouldEvaluate

    [TestMethod]
    public void ShouldEvaluate_Disabled_ReturnsFalse()
    {
        _settings.IsEnabled.Returns(false);
        Assert.IsFalse(_service.ShouldEvaluate(Party()));
    }

    [TestMethod]
    public void ShouldEvaluate_NullParty_ReturnsFalse()
    {
        Assert.IsFalse(_service.ShouldEvaluate(null!));
    }

    [TestMethod]
    public void ShouldEvaluate_PartyAtCap_ReturnsFalse()
    {
        Assert.IsFalse(_service.ShouldEvaluate(Party(extra: 1)));
    }

    [TestMethod]
    public void ShouldEvaluate_PartyAboveCap_ReturnsFalse()
    {
        Assert.IsFalse(_service.ShouldEvaluate(Party(extra: 3)));
    }

    [TestMethod]
    public void ShouldEvaluate_CapIsZero_ReturnsFalse()
    {
        _settings.MaxExtraHeroes.Returns(0);
        Assert.IsFalse(_service.ShouldEvaluate(Party()));
    }

    [TestMethod]
    public void ShouldEvaluate_BelowCap_ReturnsTrue()
    {
        Assert.IsTrue(_service.ShouldEvaluate(Party(extra: 0)));
    }

    [TestMethod]
    public void ShouldEvaluate_ClanBelowPartyLimit_ReturnsFalse()
    {
        Assert.IsFalse(_service.ShouldEvaluate(Party(clanBelowPartyLimit: true)));
    }

    [TestMethod]
    public void ShouldEvaluate_AllFourRolesFilled_ReturnsFalse()
    {
        Assert.IsFalse(_service.ShouldEvaluate(Party(filled: AllRoles)));
    }

    [TestMethod]
    public void ShouldEvaluate_ThreeOfFourRolesFilled_ReturnsTrue()
    {
        var filled = RetinueRole.Scout | RetinueRole.Surgeon | RetinueRole.Engineer;
        Assert.IsTrue(_service.ShouldEvaluate(Party(filled: filled)));
    }

    // ShouldRelease

    [TestMethod]
    public void ShouldRelease_RetinueAndClanBelowPartyLimit_ReturnsTrue()
    {
        Assert.IsTrue(_service.ShouldRelease(Party(clanBelowPartyLimit: true, retinue: "a")));
    }

    [TestMethod]
    public void ShouldRelease_RetinueAndFeatureDisabled_ReturnsTrue()
    {
        _settings.IsEnabled.Returns(false);
        Assert.IsTrue(_service.ShouldRelease(Party(retinue: "a")));
    }

    [TestMethod]
    public void ShouldRelease_RetinueAtPartyLimitAndEnabled_ReturnsFalse()
    {
        Assert.IsFalse(_service.ShouldRelease(Party(retinue: "a")));
    }

    [TestMethod]
    public void ShouldRelease_NoRetinue_ReturnsFalse()
    {
        Assert.IsFalse(_service.ShouldRelease(Party(clanBelowPartyLimit: true)));
    }

    [TestMethod]
    public void ShouldRelease_NullParty_ReturnsFalse()
    {
        Assert.IsFalse(_service.ShouldRelease(null!));
    }

    // IsEligible: one test per skip condition

    [TestMethod]
    public void IsEligible_IdleAdultInTheSettlement_ReturnsTrue()
    {
        Assert.IsTrue(_service.IsEligible(Hero()));
    }

    [TestMethod]
    public void IsEligible_NullHero_ReturnsFalse()
    {
        Assert.IsFalse(_service.IsEligible(null!));
    }

    [DataTestMethod]
    [DataRow(nameof(RetinueHero.InPartySettlement))]
    [DataRow(nameof(RetinueHero.IsActive))]
    [DataRow(nameof(RetinueHero.IsAlive))]
    [DataRow(nameof(RetinueHero.IsAdult))]
    [DataRow(nameof(RetinueHero.CanMoveToSettlement))]
    [DataRow(nameof(RetinueHero.CanHavePartyRole))]
    public void IsEligible_RequiredFactIsFalse_ReturnsFalse(string fact)
    {
        var hero = Hero();
        typeof(RetinueHero).GetProperty(fact)!.SetValue(hero, false);
        Assert.IsFalse(_service.IsEligible(hero), fact);
    }

    [DataTestMethod]
    [DataRow(nameof(RetinueHero.HasParty))]
    [DataRow(nameof(RetinueHero.IsPrisoner))]
    [DataRow(nameof(RetinueHero.IsPlayerControlled))]
    [DataRow(nameof(RetinueHero.IsTemplate))]
    [DataRow(nameof(RetinueHero.IsNotable))]
    [DataRow(nameof(RetinueHero.IsWanderer))]
    [DataRow(nameof(RetinueHero.IsWounded))]
    [DataRow(nameof(RetinueHero.IsNoncombatant))]
    [DataRow(nameof(RetinueHero.IsGovernor))]
    [DataRow(nameof(RetinueHero.IsClanLeader))]
    public void IsEligible_ExcludingFactIsTrue_ReturnsFalse(string fact)
    {
        var hero = Hero();
        typeof(RetinueHero).GetProperty(fact)!.SetValue(hero, true);
        Assert.IsFalse(_service.IsEligible(hero), fact);
    }

    // Choose

    [TestMethod]
    public void Choose_NoHeroes_ReturnsNull()
    {
        Assert.IsNull(_service.Choose(Party(), new List<RetinueHero>()));
    }

    [TestMethod]
    public void Choose_NullArguments_ReturnNull()
    {
        Assert.IsNull(_service.Choose(null!, new[] { Hero("a", scout: 50) }));
        Assert.IsNull(_service.Choose(Party(), null!));
    }

    [TestMethod]
    public void Choose_OnlyIneligibleHeroes_ReturnsNull()
    {
        var wounded = Hero("a", medicine: 200);
        wounded.IsWounded = true;
        Assert.IsNull(_service.Choose(Party(), new[] { wounded }));
    }

    [TestMethod]
    public void Choose_SkipsTheIneligibleAndTakesTheNextHero()
    {
        var wounded = Hero("a", medicine: 200);
        wounded.IsWounded = true;
        var pick = _service.Choose(Party(), new[] { wounded, Hero("b", medicine: 40) });
        Assert.AreEqual("b", pick!.HeroId);
    }

    [TestMethod]
    public void Choose_PicksTheSkillTheLeaderLacks()
    {
        var pick = _service.Choose(Party(), new[] { Hero("a", scout: 50), Hero("b", medicine: 120) });
        Assert.AreEqual("b", pick!.HeroId);
        Assert.AreEqual(RetinueRole.Surgeon, pick.Role);
    }

    [TestMethod]
    public void Choose_IgnoresFilledRoles()
    {
        var pick = _service.Choose(Party(filled: RetinueRole.Surgeon), new[] { Hero("a", medicine: 200, scout: 40) });
        Assert.AreEqual(RetinueRole.Scout, pick!.Role);
    }

    [TestMethod]
    public void Choose_MapsEachSkillToItsRole()
    {
        Assert.AreEqual(RetinueRole.Scout, _service.Choose(Party(), new[] { Hero("a", scout: 10) })!.Role);
        Assert.AreEqual(RetinueRole.Surgeon, _service.Choose(Party(), new[] { Hero("a", medicine: 10) })!.Role);
        Assert.AreEqual(RetinueRole.Engineer, _service.Choose(Party(), new[] { Hero("a", engineering: 10) })!.Role);
        Assert.AreEqual(RetinueRole.Quartermaster, _service.Choose(Party(), new[] { Hero("a", steward: 10) })!.Role);
    }

    [TestMethod]
    public void Choose_HeroNoBetterThanTheLeader_ReturnsNull()
    {
        var leader = Skills(scout: 100, medicine: 100, engineering: 100, steward: 100);
        var hero = Hero("a", scout: 100, medicine: 60, engineering: 30, steward: 99);
        Assert.IsNull(_service.Choose(Party(leader: leader), new[] { hero }));
    }

    [TestMethod]
    public void Choose_HeroBetterAtOneRoleOnly_TakesThatRole()
    {
        var leader = Skills(scout: 100, medicine: 100, engineering: 100, steward: 100);
        var hero = Hero("a", scout: 60, medicine: 101, engineering: 30, steward: 20);
        var pick = _service.Choose(Party(leader: leader), new[] { hero });
        Assert.AreEqual(RetinueRole.Surgeon, pick!.Role);
    }

    [TestMethod]
    public void Choose_RanksByGainOverTheLeader_NotByRawSkill()
    {
        var leader = Skills(scout: 20, medicine: 100);
        var hero = Hero("a", scout: 60, medicine: 110);
        var pick = _service.Choose(Party(leader: leader), new[] { hero });
        Assert.AreEqual(RetinueRole.Scout, pick!.Role);
    }

    [TestMethod]
    public void Choose_LargestGainAcrossHeroesWins()
    {
        var leader = Skills(medicine: 50);
        var pick = _service.Choose(Party(leader: leader), new[] { Hero("a", medicine: 70), Hero("b", medicine: 120) });
        Assert.AreEqual("b", pick!.HeroId);
    }

    [TestMethod]
    public void Choose_TieOnGain_KeepsTheFirstHero()
    {
        var pick = _service.Choose(Party(), new[] { Hero("first", scout: 30), Hero("second", scout: 30) });
        Assert.AreEqual("first", pick!.HeroId);
    }

    [TestMethod]
    public void Choose_EqualGainInTwoRoles_TakesTheEarlierRole()
    {
        var pick = _service.Choose(Party(), new[] { Hero("a", medicine: 30, scout: 30) });
        Assert.AreEqual(RetinueRole.Scout, pick!.Role);
    }

    [TestMethod]
    public void Choose_AllSkillsZero_ReturnsNull()
    {
        Assert.IsNull(_service.Choose(Party(), new[] { Hero("a") }));
    }

    [TestMethod]
    public void Choose_NeverMutatesItsInputs()
    {
        var heroes = new[] { Hero("a", scout: 30), Hero("b", medicine: 40) };
        _service.Choose(Party(), heroes);
        Assert.AreEqual(2, heroes.Length);
        Assert.IsTrue(heroes.All(h => h.InPartySettlement));
    }
}
