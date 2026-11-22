using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Diagnostics;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat;

/// <summary>
/// The hunters line (#1337): what the standing heroes and their formation did in a cleared wave. Judged on the group (all in it, no one else in it, off AI control) and, once the group
/// has held the field's own order at the wall for the settle time, on every standing hero being near that place, on the ground and in height. Kills and the director's running totals are
/// reported, never judged.
/// </summary>
[TestClass]
public class LongDefeatHeroChecksTests
{
    private static LdHeroFacts Hero(string id, int kills = 0, int formation = HeroGroup.FormationIndex, float health = 1f, float distance = 3f, float height = 0.5f, string weapon = "none") =>
        new(id, kills, formation, health, distance, height, Weapon: weapon);

    private static LdHeroesRead Read(IReadOnlyList<LdHeroFacts> heroes, bool held = true, string order = "Move", bool own = true, int? units = null, float settled = 600f) =>
        new(heroes, held, order, own, units ?? heroes.Count, settled);

    private static CheckVerdict Hunters(int wave, LdHeroesRead read, LdHuntersTotals? totals = null) =>
        LongDefeatHeroChecks.Hunters(wave, read, totals ?? LdHuntersTotals.None);

    [TestMethod]
    public void NoneStanding_IsSkipped()
    {
        var verdict = Hunters(4, Read(new List<LdHeroFacts>(), held: false, order: "Stop", own: false, units: 0));

        Assert.AreEqual(CheckResult.Skip, verdict.Result);
        Assert.AreEqual("w=4 reason=none-standing", verdict.Detail);
    }

    [TestMethod]
    public void AllInTheGroup_HeldAloneAndNearTheWall_Passes_WithTheExactDetail()
    {
        var facts = new[]
        {
            Hero("aragorn", 5, health: 0.5f, weapon: "TwoHandedSword"),
            Hero("gimli", 2, health: 1f, weapon: "TwoHandedAxe"),
            Hero("legolas", 0, health: 0.256f, weapon: "Bow"),
        };

        var verdict = Hunters(3, Read(facts), new LdHuntersTotals(1, 4, 2));

        Assert.AreEqual(CheckResult.Pass, verdict.Result);
        Assert.AreEqual("w=3 standing=3 in_group=3 units=3 held=1 order=Move own=1 settled=600 near=3 kills=7 by=aragorn:5,gimli:2,legolas:0 health=aragorn:0.50,gimli:1.00,legolas:0.26"
            + " weapons=aragorn:TwoHandedSword,gimli:TwoHandedAxe,legolas:Bow retaken=1 moved_out=4 moved_back=2", verdict.Detail);
    }

    [TestMethod]
    public void TheWeaponClass_IsPrinted_AndAPassingReadStillPasses()
    {
        var verdict = Hunters(2, Read(new[] { Hero("aragorn", 1), Hero("gimli", 0, weapon: "OneHandedSword") }));

        Assert.AreEqual(CheckResult.Pass, verdict.Result);
        StringAssert.Contains(verdict.Detail, " health=aragorn:1.00,gimli:1.00 weapons=aragorn:none,gimli:OneHandedSword retaken=0");
    }

    [TestMethod]
    public void OneInAnotherFormation_Fails()
    {
        var facts = new[] { Hero("aragorn", 1), Hero("gimli", 0, formation: 0) };

        var verdict = Hunters(2, Read(facts, units: 1));

        Assert.AreEqual(CheckResult.Fail, verdict.Result);
        StringAssert.Contains(verdict.Detail, "standing=2 in_group=1 ");
    }

    [TestMethod]
    public void AHeroWithNoFormation_Fails()
    {
        Assert.AreEqual(CheckResult.Fail, Hunters(1, Read(new[] { Hero("gimli", 0, formation: -1) }, units: 0)).Result);
    }

    [TestMethod]
    public void OtherSoldiersInTheGroup_Fail_WhenUnitsExceedTheHeroesInIt()
    {
        var facts = new[] { Hero("aragorn"), Hero("gimli") };

        var verdict = Hunters(2, Read(facts, units: 9));

        Assert.AreEqual(CheckResult.Fail, verdict.Result);
        StringAssert.Contains(verdict.Detail, "standing=2 in_group=2 units=9 held=1 ");
    }

    [TestMethod]
    public void AGroupTheAIDrives_Fails_WithHeldZero()
    {
        var verdict = Hunters(2, Read(new[] { Hero("aragorn"), Hero("gimli") }, held: false, order: "Charge", own: false));

        Assert.AreEqual(CheckResult.Fail, verdict.Result);
        StringAssert.Contains(verdict.Detail, "units=2 held=0 order=Charge own=0 settled=600 ");
    }

    [TestMethod]
    public void UnderTheFieldsOwnOrder_AHeroWhoNeverReachedTheWall_Fails()
    {
        var facts = new[] { Hero("aragorn", distance: 5f), Hero("gimli", distance: 20.5f) };

        var verdict = Hunters(2, Read(facts));

        Assert.AreEqual(CheckResult.Fail, verdict.Result);
        StringAssert.Contains(verdict.Detail, " own=1 settled=600 near=1 ");
    }

    [TestMethod]
    public void AHeroAtTheFootOfTheWall_IsNotNear()
    {
        var verdict = Hunters(2, Read(new[] { Hero("aragorn"), Hero("gimli", distance: 3f, height: HeroGroup.NearHeightMetres + 0.5f) }));

        Assert.AreEqual(CheckResult.Fail, verdict.Result);
        StringAssert.Contains(verdict.Detail, " near=1 ");
    }

    [TestMethod]
    public void AHeroExactlyAtTheNearHeight_IsNear()
    {
        Assert.AreEqual(CheckResult.Pass, Hunters(1, Read(new[] { Hero("gimli", height: HeroGroup.NearHeightMetres) })).Result);
    }

    [TestMethod]
    public void ANaNHeight_IsNotNear()
    {
        Assert.AreEqual(CheckResult.Fail, Hunters(1, Read(new[] { Hero("gimli", height: float.NaN) })).Result);
    }

    [TestMethod]
    public void AGroupPostedLessThanTheSettleTimeAgo_IsNotJudgedOnDistance()
    {
        var verdict = Hunters(2, Read(new[] { Hero("aragorn", distance: 80f) }, settled: HeroGroup.SettleSeconds - 0.1f));

        Assert.AreEqual(CheckResult.Pass, verdict.Result);
        StringAssert.Contains(verdict.Detail, " own=1 settled=59 near=0 ");
    }

    [TestMethod]
    public void AHeroThePlayerControls_CountsAsNear_WhereverHeStands()
    {
        // #1366: in a campaign a fallen player can take over a free hero (IsHero is false there). He stays in the heroes' formation, and he is where the player wants him.
        var driven = new LdHeroFacts("aragorn", 0, HeroGroup.FormationIndex, 1f, 80f, 9f, PlayerControlled: true);

        var verdict = Hunters(2, Read(new[] { driven, Hero("gimli") }));

        Assert.AreEqual(CheckResult.Pass, verdict.Result, verdict.Detail);
        StringAssert.Contains(verdict.Detail, " own=1 settled=600 near=2 ");
    }

    [TestMethod]
    public void AtTheSettleTime_DistanceIsJudged()
    {
        var verdict = Hunters(2, Read(new[] { Hero("aragorn", distance: 80f) }, settled: HeroGroup.SettleSeconds));

        Assert.AreEqual(CheckResult.Fail, verdict.Result);
        StringAssert.Contains(verdict.Detail, " own=1 settled=60 near=0 ");
    }

    [TestMethod]
    public void ANaNSettleTime_PrintsZero_AndIsNotJudgedOnDistance()
    {
        var verdict = Hunters(2, Read(new[] { Hero("aragorn", distance: 80f) }, settled: float.NaN));

        Assert.AreEqual(CheckResult.Pass, verdict.Result);
        StringAssert.Contains(verdict.Detail, " settled=0 ");
    }

    [TestMethod]
    public void AHeroExactlyAtTheNearDistance_IsNear()
    {
        Assert.AreEqual(CheckResult.Pass, Hunters(1, Read(new[] { Hero("gimli", distance: HeroGroup.NearMetres) })).Result);
    }

    [TestMethod]
    public void UnderThePlayersOwnOrder_DistanceIsNeverJudged()
    {
        var facts = new[] { Hero("aragorn", distance: 80f), Hero("gimli", distance: float.NaN) };

        var verdict = Hunters(2, Read(facts, order: "Move", own: false));

        Assert.AreEqual(CheckResult.Pass, verdict.Result);
        StringAssert.Contains(verdict.Detail, " order=Move own=0 settled=600 near=0 ");
    }

    [TestMethod]
    public void ANaNDistance_IsNotNear()
    {
        var verdict = Hunters(1, Read(new[] { Hero("gimli", distance: float.NaN) }));

        Assert.AreEqual(CheckResult.Fail, verdict.Result);
        StringAssert.Contains(verdict.Detail, " own=1 settled=600 near=0 ");
    }

    [TestMethod]
    public void NoKills_DoNotFail()
    {
        Assert.AreEqual(CheckResult.Pass, Hunters(1, Read(new[] { Hero("legolas", 0) })).Result);
    }

    [TestMethod]
    public void TheRunningTotals_AreReported_AndNeverJudged()
    {
        var verdict = Hunters(1, Read(new[] { Hero("legolas") }), new LdHuntersTotals(7, 30, 9));

        Assert.AreEqual(CheckResult.Pass, verdict.Result);
        StringAssert.EndsWith(verdict.Detail, " retaken=7 moved_out=30 moved_back=9");
    }

    [TestMethod]
    public void ANaNShare_DoesNotThrow_AndPrintsAStableValue()
    {
        var first = Hunters(1, Read(new[] { Hero("gimli", health: float.NaN) })).Detail;
        var second = Hunters(1, Read(new[] { Hero("gimli", health: float.NaN) })).Detail;

        Assert.AreEqual(first, second);
        StringAssert.Contains(first, "health=gimli:");
    }

    [TestMethod]
    public void TheTotals_AddEachHold_CountingARegainOnce()
    {
        var totals = LdHuntersTotals.None.Add(new LdHuntersHold(true, 2, 1)).Add(new LdHuntersHold(false, 3, 0)).Add(LdHuntersHold.Nothing);

        Assert.AreEqual(new LdHuntersTotals(1, 5, 1), totals);
    }

    [TestMethod]
    public void AHold_HasActed_OnlyWhenItRegainedOrMovedSomeone()
    {
        Assert.IsFalse(LdHuntersHold.Nothing.Acted);
        Assert.IsTrue(new LdHuntersHold(true, 0, 0).Acted);
        Assert.IsTrue(new LdHuntersHold(false, 1, 0).Acted);
        Assert.IsTrue(new LdHuntersHold(false, 0, 1).Acted);
    }

    private static readonly HeroSpawn Aragorn = new("aragorn", "taom_hero_aragorn", 3f);

    private static LdHeroReport GoodSpawn() => new(true, null, true, false, HeroGroup.FormationIndex, 3f, "strider_sword", Held: true, Strangers: 0, Order: "Move");

    [TestMethod]
    public void Spawned_AnEnemyHeroUnderAiInAClassFormation_PassesAndEndsSideEnemy()
    {
        var verdict = LongDefeatHeroChecks.Spawned(Aragorn, GoodSpawn() with { Enemy = true, FormationIndex = 0, Held = false, Strangers = 99 });

        Assert.AreEqual(CheckResult.Pass, verdict.Result, verdict.Detail);
        StringAssert.EndsWith(verdict.Detail, " side=enemy");
    }

    [TestMethod]
    public void Spawned_AnEnemyHeroInFormation7OrHeld_Fails()
    {
        Assert.AreEqual(CheckResult.Fail, LongDefeatHeroChecks.Spawned(Aragorn, GoodSpawn() with { Enemy = true, Held = false }).Result, "formation 7");
        Assert.AreEqual(CheckResult.Fail, LongDefeatHeroChecks.Spawned(Aragorn, GoodSpawn() with { Enemy = true, FormationIndex = 0, Held = true }).Result, "held");
    }

    [TestMethod]
    public void Spawned_ADefenceHero_DetailHasNoSide()
    {
        Assert.IsFalse(LongDefeatHeroChecks.Spawned(Aragorn, GoodSpawn()).Detail.Contains("side="));
    }

    [TestMethod]
    public void AGoodSpawn_Passes_WithTheExactDetail()
    {
        var verdict = LongDefeatHeroChecks.Spawned(Aragorn, GoodSpawn());

        Assert.AreEqual(CheckResult.Pass, verdict.Result);
        Assert.AreEqual("hero=aragorn troop=taom_hero_aragorn is_hero=1 campaign=0 mounted=0 formation=7 held=1 strangers=0 order=Move health_factor=3.00 wanted_factor=3.00 wielded=strider_sword", verdict.Detail);
    }

    [TestMethod]
    public void ASpawn_TheEngineDoesNotTreatAsAHero_Fails()
    {
        Assert.AreEqual(CheckResult.Fail, LongDefeatHeroChecks.Spawned(Aragorn, GoodSpawn() with { IsHero = false }).Result);
    }

    [TestMethod]
    public void ACampaignCharacterWithNoHero_Passes_WithCampaign1()
    {
        var verdict = LongDefeatHeroChecks.Spawned(Aragorn, GoodSpawn() with { IsHero = false, CampaignCharacter = true });

        Assert.AreEqual(CheckResult.Pass, verdict.Result);
        StringAssert.Contains(verdict.Detail, "is_hero=0 campaign=1 mounted=0");
    }

    [TestMethod]
    public void ACampaignHero_Fails()
    {
        Assert.AreEqual(CheckResult.Fail, LongDefeatHeroChecks.Spawned(Aragorn, GoodSpawn() with { CampaignCharacter = true }).Result);
    }

    [TestMethod]
    public void AMountedSpawn_Fails()
    {
        Assert.AreEqual(CheckResult.Fail, LongDefeatHeroChecks.Spawned(Aragorn, GoodSpawn() with { Mounted = true }).Result);
    }

    [TestMethod]
    public void ASpawnInAnotherFormation_Fails()
    {
        Assert.AreEqual(CheckResult.Fail, LongDefeatHeroChecks.Spawned(Aragorn, GoodSpawn() with { FormationIndex = 0 }).Result);
    }

    [TestMethod]
    public void AHealthFactorOffByMoreThanAHundredth_Fails_AndWithinItPasses()
    {
        Assert.AreEqual(CheckResult.Fail, LongDefeatHeroChecks.Spawned(Aragorn, GoodSpawn() with { HealthFactor = 3.02f }).Result);
        Assert.AreEqual(CheckResult.Fail, LongDefeatHeroChecks.Spawned(Aragorn, GoodSpawn() with { HealthFactor = 2.98f }).Result);
        Assert.AreEqual(CheckResult.Pass, LongDefeatHeroChecks.Spawned(Aragorn, GoodSpawn() with { HealthFactor = 3.005f }).Result);
    }

    [TestMethod]
    public void ANaNHealthFactor_Fails()
    {
        Assert.AreEqual(CheckResult.Fail, LongDefeatHeroChecks.Spawned(Aragorn, GoodSpawn() with { HealthFactor = float.NaN }).Result);
    }

    [TestMethod]
    public void ASpawnWhoseFormationTheAIStillDrives_Fails_WithHeldZero()
    {
        var verdict = LongDefeatHeroChecks.Spawned(Aragorn, GoodSpawn() with { Held = false });

        Assert.AreEqual(CheckResult.Fail, verdict.Result);
        StringAssert.Contains(verdict.Detail, " formation=7 held=0 ");
    }

    [TestMethod]
    public void ASpawnThatLeavesAStrangerInTheGroup_Fails_WithTheCount()
    {
        var verdict = LongDefeatHeroChecks.Spawned(Aragorn, GoodSpawn() with { Strangers = 2, Order = "Stop" });

        Assert.AreEqual(CheckResult.Fail, verdict.Result);
        StringAssert.Contains(verdict.Detail, " held=1 strangers=2 order=Stop ");
    }

    [TestMethod]
    public void Spawned_ALooseHero_PassesInHisClassFormationUnderTheAi()
    {
        // #1105 slice 2: on a field run a hero of the player's side stands in his class formation under the team AI.
        var hero = new HeroSpawn("boromir", "taom_hero_boromir", 8f);
        var report = new LdHeroReport(true, null, false, false, 0, 8f, "sword", Held: false, Strangers: 0, Order: "Charge", CampaignCharacter: true, Loose: true);

        var verdict = LongDefeatHeroChecks.Spawned(hero, report);

        Assert.AreEqual(CheckResult.Pass, verdict.Result, verdict.Detail);
        StringAssert.EndsWith(verdict.Detail, "group=class");
        Assert.IsFalse(verdict.Detail.Contains("side=enemy"));
    }

    [TestMethod]
    public void Spawned_ALooseHero_FailsInTheHeroesOwnFormation()
    {
        var hero = new HeroSpawn("boromir", "taom_hero_boromir", 8f);
        var report = new LdHeroReport(true, null, false, false, HeroGroup.FormationIndex, 8f, "sword", Held: true, Strangers: 0, Order: "Move", CampaignCharacter: true, Loose: true);

        Assert.AreEqual(CheckResult.Fail, LongDefeatHeroChecks.Spawned(hero, report).Result);
    }
}
