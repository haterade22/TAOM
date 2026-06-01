using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Tests.Features.CareerSystem;

[TestClass]
public class CareerQuestServiceTests
{
    private ICareerQuestConfigProvider _config;
    private ICareerRegistry _registry;
    private ICareerDataService _data;
    private CareerQuestService _sut;

    private static CareerQuestObjectiveDefinition Obj(CareerQuestObjectiveType t, int target, string param = "")
        => new CareerQuestObjectiveDefinition(t, target, param, "{=k}task");

    private static CareerQuestRewardDefinition Rew(CareerQuestRewardType t, int amount, string param = "")
        => new CareerQuestRewardDefinition(t, amount, param);

    private static CareerQuestDefinition Quest(string id, string careerId, int tier,
        IReadOnlyList<CareerQuestObjectiveDefinition> objs, IReadOnlyList<CareerQuestRewardDefinition> rewards = null)
        => new CareerQuestDefinition(id, careerId, tier, "{=k}t", "{=k}d",
            objs, rewards ?? new List<CareerQuestRewardDefinition>());

    [TestInitialize]
    public void Setup()
    {
        _config = Substitute.For<ICareerQuestConfigProvider>();
        _config.LoadQuests().Returns(new List<CareerQuestDefinition>());
        _registry = Substitute.For<ICareerRegistry>();
        _data = Substitute.For<ICareerDataService>();
        _sut = new CareerQuestService(_config, _registry, _data, Substitute.For<IModLogger>());
    }

    // ── GetQuestFor ──────────────────────────────────────────────
    [TestMethod]
    public void GetQuestFor_AuthoredCareerTier_ReturnsQuest()
    {
        var q = Quest("gondor_t2", "captain_of_osgiliath", 2, new List<CareerQuestObjectiveDefinition> { Obj(CareerQuestObjectiveType.WinBattles, 5) });
        _config.LoadQuests().Returns(new List<CareerQuestDefinition> { q });

        Assert.AreSame(q, _sut.GetQuestFor("captain_of_osgiliath", 2));
    }

    [TestMethod]
    public void GetQuestFor_UnauthoredCareerTier_ReturnsNull()
    {
        Assert.IsNull(_sut.GetQuestFor("ranger_of_ithilien", 3));
    }

    [TestMethod]
    public void GetQuestById_KnownId_ReturnsQuest()
    {
        var q = Quest("gondor_t2", "captain_of_osgiliath", 2, new List<CareerQuestObjectiveDefinition> { Obj(CareerQuestObjectiveType.WinBattles, 5) });
        _config.LoadQuests().Returns(new List<CareerQuestDefinition> { q });
        Assert.AreSame(q, _sut.GetQuestById("gondor_t2"));
    }

    [TestMethod]
    public void GetQuestById_UnknownId_ReturnsNull()
    {
        Assert.IsNull(_sut.GetQuestById("nope"));
    }

    // ── IsTierUnlocked (hybrid: level OR quest) ──────────────────
    [TestMethod]
    public void IsTierUnlocked_LevelMeetsThreshold_True()
    {
        _registry.IsTierAvailable(20, 3).Returns(true);
        Assert.IsTrue(_sut.IsTierUnlocked(20, 3, "hero1"));
    }

    [TestMethod]
    public void IsTierUnlocked_LevelTooLow_ButQuestUnlocked_True()
    {
        _registry.IsTierAvailable(5, 2).Returns(false);
        _data.IsTierUnlocked("hero1", 2).Returns(true);
        Assert.IsTrue(_sut.IsTierUnlocked(5, 2, "hero1"));
    }

    [TestMethod]
    public void IsTierUnlocked_LevelTooLow_NoQuestUnlock_False()
    {
        _registry.IsTierAvailable(5, 2).Returns(false);
        _data.IsTierUnlocked("hero1", 2).Returns(false);
        Assert.IsFalse(_sut.IsTierUnlocked(5, 2, "hero1"));
    }

    // ── ComputeProgress: one cell per objective type ─────────────
    [DataTestMethod]
    [DataRow(CareerQuestObjectiveType.WinBattles, 2, 1, 3)]
    [DataRow(CareerQuestObjectiveType.SettlementsCaptured, 4, 1, 5)]
    [DataRow(CareerQuestObjectiveType.TournamentsWon, 0, 1, 1)]
    [DataRow(CareerQuestObjectiveType.DefeatEnemyLords, 7, 2, 9)]
    [DataRow(CareerQuestObjectiveType.VisitSettlementType, 3, 1, 4)]
    public void ComputeProgress_CountObjective_Accumulates(CareerQuestObjectiveType type, int current, int observed, int expected)
    {
        Assert.AreEqual(expected, _sut.ComputeProgress(type, current, observed));
    }

    [DataTestMethod]
    [DataRow(CareerQuestObjectiveType.SkillThreshold, 80, 100, 100)]   // rises
    [DataRow(CareerQuestObjectiveType.SkillThreshold, 100, 80, 100)]   // banked — does not regress
    [DataRow(CareerQuestObjectiveType.RenownThreshold, 50, 120, 120)]
    [DataRow(CareerQuestObjectiveType.GoldAccumulated, 9000, 5000, 9000)]
    public void ComputeProgress_ThresholdObjective_BanksHighest(CareerQuestObjectiveType type, int current, int observed, int expected)
    {
        Assert.AreEqual(expected, _sut.ComputeProgress(type, current, observed));
    }

    [TestMethod]
    public void ComputeProgress_NegativeInputs_ClampedToZero()
    {
        Assert.AreEqual(0, _sut.ComputeProgress(CareerQuestObjectiveType.WinBattles, -3, -1));
    }

    // ── IsObjectiveSatisfied ─────────────────────────────────────
    [TestMethod]
    public void IsObjectiveSatisfied_ProgressAtTarget_True()
        => Assert.IsTrue(_sut.IsObjectiveSatisfied(Obj(CareerQuestObjectiveType.WinBattles, 5), 5));

    [TestMethod]
    public void IsObjectiveSatisfied_ProgressBelowTarget_False()
        => Assert.IsFalse(_sut.IsObjectiveSatisfied(Obj(CareerQuestObjectiveType.WinBattles, 5), 4));

    // ── AreAllObjectivesSatisfied ────────────────────────────────
    [TestMethod]
    public void AreAllObjectivesSatisfied_AllMet_True()
    {
        var q = Quest("q", "c", 1, new List<CareerQuestObjectiveDefinition>
        { Obj(CareerQuestObjectiveType.WinBattles, 5), Obj(CareerQuestObjectiveType.RenownThreshold, 100) });
        Assert.IsTrue(_sut.AreAllObjectivesSatisfied(q, new List<int> { 5, 150 }));
    }

    [TestMethod]
    public void AreAllObjectivesSatisfied_OneShort_False()
    {
        var q = Quest("q", "c", 1, new List<CareerQuestObjectiveDefinition>
        { Obj(CareerQuestObjectiveType.WinBattles, 5), Obj(CareerQuestObjectiveType.RenownThreshold, 100) });
        Assert.IsFalse(_sut.AreAllObjectivesSatisfied(q, new List<int> { 5, 90 }));
    }

    [TestMethod]
    public void AreAllObjectivesSatisfied_ProgressListTooShort_False()
    {
        var q = Quest("q", "c", 1, new List<CareerQuestObjectiveDefinition>
        { Obj(CareerQuestObjectiveType.WinBattles, 5), Obj(CareerQuestObjectiveType.RenownThreshold, 100) });
        Assert.IsFalse(_sut.AreAllObjectivesSatisfied(q, new List<int> { 5 }));
    }

    // ── ApplyRewards: one cell per reward type ───────────────────
    [TestMethod]
    public void ApplyRewards_UnlockTier_CallsDataServiceUnlockTier()
    {
        var q = Quest("q", "c", 2, new List<CareerQuestObjectiveDefinition> { Obj(CareerQuestObjectiveType.WinBattles, 1) },
            new List<CareerQuestRewardDefinition> { Rew(CareerQuestRewardType.UnlockTier, 2) });
        _sut.ApplyRewards("hero1", q, ValidHero());
        _data.Received(1).UnlockTier("hero1", 2);
    }

    [TestMethod]
    public void ApplyRewards_GrantAttributeFlag_CallsDataServiceSetFlag()
    {
        var q = Quest("q", "c", 1, new List<CareerQuestObjectiveDefinition> { Obj(CareerQuestObjectiveType.WinBattles, 1) },
            new List<CareerQuestRewardDefinition> { Rew(CareerQuestRewardType.GrantAttributeFlag, 0, "captain_of_osgiliath_proven") });
        _sut.ApplyRewards("hero1", q, ValidHero());
        _data.Received(1).SetFlag("hero1", "captain_of_osgiliath_proven");
    }

    [TestMethod]
    public void ApplyRewards_GrantItem_CallsAdapterAddItem()
    {
        var hero = ValidHero();
        var q = Quest("q", "c", 1, new List<CareerQuestObjectiveDefinition> { Obj(CareerQuestObjectiveType.WinBattles, 1) },
            new List<CareerQuestRewardDefinition> { Rew(CareerQuestRewardType.GrantItem, 1, "sk_gd_ith_sword") });
        _sut.ApplyRewards("hero1", q, hero);
        hero.Received(1).AddItemToInventory("sk_gd_ith_sword", 1);
    }

    [TestMethod]
    public void ApplyRewards_GrantRenown_CallsAdapterAddRenown()
    {
        var hero = ValidHero();
        var q = Quest("q", "c", 1, new List<CareerQuestObjectiveDefinition> { Obj(CareerQuestObjectiveType.WinBattles, 1) },
            new List<CareerQuestRewardDefinition> { Rew(CareerQuestRewardType.GrantRenown, 50) });
        _sut.ApplyRewards("hero1", q, hero);
        hero.Received(1).AddRenown(50);
    }

    [TestMethod]
    public void ApplyRewards_GrantInfluence_CallsAdapterAddInfluence()
    {
        var hero = ValidHero();
        var q = Quest("q", "c", 1, new List<CareerQuestObjectiveDefinition> { Obj(CareerQuestObjectiveType.WinBattles, 1) },
            new List<CareerQuestRewardDefinition> { Rew(CareerQuestRewardType.GrantInfluence, 20) });
        _sut.ApplyRewards("hero1", q, hero);
        hero.Received(1).AddInfluence(20);
    }

    [TestMethod]
    public void ApplyRewards_InvalidHero_StillUnlocksTier_ButSkipsEngineRewards()
    {
        var hero = Substitute.For<IQuestHeroAdapter>();
        hero.IsValid.Returns(false);
        var q = Quest("q", "c", 2, new List<CareerQuestObjectiveDefinition> { Obj(CareerQuestObjectiveType.WinBattles, 1) },
            new List<CareerQuestRewardDefinition>
            {
                Rew(CareerQuestRewardType.UnlockTier, 2),
                Rew(CareerQuestRewardType.GrantRenown, 50),
            });

        _sut.ApplyRewards("hero1", q, hero);

        _data.Received(1).UnlockTier("hero1", 2);          // career-data reward applies
        hero.DidNotReceive().AddRenown(Arg.Any<int>());    // engine reward skipped
    }

    [TestMethod]
    public void ApplyRewards_NullQuest_DoesNothing()
    {
        _sut.ApplyRewards("hero1", null, ValidHero());
        _data.DidNotReceive().UnlockTier(Arg.Any<string>(), Arg.Any<int>());
    }

    private static IQuestHeroAdapter ValidHero()
    {
        var hero = Substitute.For<IQuestHeroAdapter>();
        hero.IsValid.Returns(true);
        hero.StringId.Returns("hero1");
        return hero;
    }
}
