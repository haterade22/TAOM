using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CreatureBandits;
using TaleWorlds.Core;

// Creature Bandits (#692): the pure decisions every patch and the reward model ask. A creature bandit is
// a riderless, non-humanoid agent whose Character is one of the hidden creature troops (the spawner sets
// Character on it); a normal mount's Character is null, and a normal troop is humanoid.

namespace TAOM.Tests.Features.CreatureBandits;

[TestClass]
public class CreatureBanditRulesTests
{
    private const string Brood = "taom_spider_brood_forest";

    [TestMethod]
    public void CreatureTroopIds_AreTheThreeSpiderBroodTroops()
    {
        CollectionAssert.AreEquivalent(
            new[] { "taom_spider_brood_pale", "taom_spider_brood_forest", "taom_spider_brood_brown" },
            CreatureBanditsConfig.CreatureTroopIds.ToList());
    }

    [TestMethod]
    public void IsCreatureTroop_CatalogueId_ReturnsTrue()
        => Assert.IsTrue(CreatureBanditRules.IsCreatureTroop(Brood));

    [TestMethod]
    public void IsCreatureTroop_SpiderRiderTroop_ReturnsFalse()
        => Assert.IsFalse(CreatureBanditRules.IsCreatureTroop("taom_spider_creature"));

    [TestMethod]
    public void IsCreatureTroop_DifferentCase_ReturnsFalse()
        => Assert.IsFalse(CreatureBanditRules.IsCreatureTroop(Brood.ToUpperInvariant()));

    [TestMethod]
    public void IsCreatureTroop_NullOrEmpty_ReturnsFalse()
    {
        Assert.IsFalse(CreatureBanditRules.IsCreatureTroop(null));
        Assert.IsFalse(CreatureBanditRules.IsCreatureTroop(string.Empty));
    }

    [TestMethod]
    public void IsCreatureBroodClan_TheSpiderClan_ReturnsTrue()
        => Assert.IsTrue(CreatureBanditRules.IsCreatureBroodClan("mirkwood_spiders"));

    [TestMethod]
    public void IsCreatureBroodClan_OtherBanditClansAndNull_ReturnFalse()
    {
        Assert.IsFalse(CreatureBanditRules.IsCreatureBroodClan("looters"));
        Assert.IsFalse(CreatureBanditRules.IsCreatureBroodClan("mirkwood_stalkers"));
        Assert.IsFalse(CreatureBanditRules.IsCreatureBroodClan(null));
    }

    [TestMethod]
    public void ShouldSpawnAsCreature_EnemyCreatureTroopInFieldBattle_ReturnsTrue()
        => Assert.IsTrue(CreatureBanditRules.ShouldSpawnAsCreature(Brood, isPlayerSide: false, isFieldBattle: true, hasCreatureItem: true));

    [TestMethod]
    public void ShouldSpawnAsCreature_PlayerSide_ReturnsFalse()
        => Assert.IsFalse(CreatureBanditRules.ShouldSpawnAsCreature(Brood, isPlayerSide: true, isFieldBattle: true, hasCreatureItem: true));

    [TestMethod]
    public void ShouldSpawnAsCreature_NotAFieldBattle_ReturnsFalse()
        => Assert.IsFalse(CreatureBanditRules.ShouldSpawnAsCreature(Brood, isPlayerSide: false, isFieldBattle: false, hasCreatureItem: true));

    [TestMethod]
    public void ShouldSpawnAsCreature_NoCreatureItem_ReturnsFalse()
        => Assert.IsFalse(CreatureBanditRules.ShouldSpawnAsCreature(Brood, isPlayerSide: false, isFieldBattle: true, hasCreatureItem: false));

    [TestMethod]
    public void ShouldSpawnAsCreature_OrdinaryTroop_ReturnsFalse()
        => Assert.IsFalse(CreatureBanditRules.ShouldSpawnAsCreature("taom_spider_creature", isPlayerSide: false, isFieldBattle: true, hasCreatureItem: true));

    [TestMethod]
    public void IsCreatureBandit_RiderlessNonHumanWithCreatureCharacter_ReturnsTrue()
        => Assert.IsTrue(CreatureBanditRules.IsCreatureBandit(Brood, isHuman: false, hasRider: false));

    [TestMethod]
    public void IsCreatureBandit_OrdinaryMountWithNoCharacter_ReturnsFalse()
        => Assert.IsFalse(CreatureBanditRules.IsCreatureBandit(null, isHuman: false, hasRider: false));

    [TestMethod]
    public void IsCreatureBandit_HumanoidAgentOfACreatureTroop_ReturnsFalse()
    {
        // The fallback path: a creature troop that spawned the vanilla way is a goblin husk on a spider,
        // so the humanoid rider carries the id but is not the creature.
        Assert.IsFalse(CreatureBanditRules.IsCreatureBandit(Brood, isHuman: true, hasRider: false));
    }

    [TestMethod]
    public void IsCreatureBandit_CreatureWithARider_ReturnsFalse()
        => Assert.IsFalse(CreatureBanditRules.IsCreatureBandit(Brood, isHuman: false, hasRider: true));

    // Route A (#692): the engine allocates an agent's native weapon state only when CanWieldWeapon is in its flags at
    // creation, and a soldier's target scorer reads it unchecked; a live CanWieldWeapon, though, sends the spider into
    // the unarmed melee lookup its action set cannot answer. So the flag is added for the native creation call only,
    // stripped before the build, and Mountable is cleared after the build so IsEnemy reads the creature's own team.
    private const AgentFlag SpiderMonsterFlags = AgentFlag.Mountable | AgentFlag.CanRear | AgentFlag.RunsAwayWhenHit
                                                 | AgentFlag.CanCharge | AgentFlag.CanWander;

    [TestMethod]
    public void CreationFlags_NonHumanoid_AddsOnlyCanWieldWeapon()
        => Assert.AreEqual(SpiderMonsterFlags | AgentFlag.CanWieldWeapon, CreatureBanditRules.CreationFlags(SpiderMonsterFlags));

    [TestMethod]
    public void CreationFlags_Humanoid_Unchanged()
    {
        const AgentFlag human = AgentFlag.IsHumanoid | AgentFlag.CanAttack | AgentFlag.CanDefend;
        Assert.AreEqual(human, CreatureBanditRules.CreationFlags(human));
    }

    [TestMethod]
    public void BuildFlags_StripsCanWieldWeapon_KeepsMountable()
        => Assert.AreEqual(SpiderMonsterFlags, CreatureBanditRules.BuildFlags(CreatureBanditRules.CreationFlags(SpiderMonsterFlags)));

    [TestMethod]
    public void CombatantFlags_ClearsMountableAndFlightFlags_LeavesCanCharge()
    {
        var built = CreatureBanditRules.BuildFlags(CreatureBanditRules.CreationFlags(SpiderMonsterFlags));
        Assert.AreEqual(AgentFlag.CanCharge, CreatureBanditRules.CombatantFlags(built));
    }

    [TestMethod]
    public void CombatantFlags_NeverAddsAttackHumanoidAlarmOrWield()
    {
        const AgentFlag forbidden = AgentFlag.CanAttack | AgentFlag.IsHumanoid | AgentFlag.CanGetAlarmed | AgentFlag.CanWieldWeapon;
        var built = CreatureBanditRules.BuildFlags(CreatureBanditRules.CreationFlags(SpiderMonsterFlags));
        Assert.AreEqual(AgentFlag.None, CreatureBanditRules.CombatantFlags(built) & forbidden);
    }

    [TestMethod]
    public void CombatantFlags_LeavesTheNativeAiResetBitsAlone()
    {
        // Native SetAgentFlags resets the agent's AI when CanWieldWeapon, CanDefend or CanAttack change (mask 0x4018),
        // and that reset walks the weapon state. The unmount must not flip any of them, on either path.
        const AgentFlag aiReset = AgentFlag.CanWieldWeapon | AgentFlag.CanDefend | AgentFlag.CanAttack;
        var built = CreatureBanditRules.BuildFlags(CreatureBanditRules.CreationFlags(SpiderMonsterFlags));
        Assert.AreEqual(built & aiReset, CreatureBanditRules.CombatantFlags(built) & aiReset);
        Assert.AreEqual(built & aiReset, CreatureBanditRules.WithoutFlightFlags(built) & aiReset);
    }

    [TestMethod]
    public void IsWeaponStateAllocated_ReadsTheWieldIndexPointer()
    {
        // The managed wield-index pointer is block+0xEE0, so a null block leaves 0xEE0; a cleared agent leaves 0.
        Assert.IsFalse(CreatureBanditRules.IsWeaponStateAllocated(0xEE0));
        Assert.IsFalse(CreatureBanditRules.IsWeaponStateAllocated(0));
        Assert.IsTrue(CreatureBanditRules.IsWeaponStateAllocated(0x1_2345_6EE0));
    }

    [TestMethod]
    public void RouteA_AllPreconditionsMet_Unmounts()
        => Assert.AreEqual(CreatureRouteA.On, CreatureBanditRules.RouteA(weaponState: true, weaponFlagLive: false, teamSet: true, pelvisBoneResolves: true));

    [TestMethod]
    public void RouteA_AnyPreconditionMissing_SkipsWithItsReason()
    {
        Assert.AreEqual(CreatureRouteA.SkippedNoWeaponState, CreatureBanditRules.RouteA(false, false, true, true));
        Assert.AreEqual(CreatureRouteA.SkippedWeaponFlagLive, CreatureBanditRules.RouteA(true, true, true, true));
        Assert.AreEqual(CreatureRouteA.SkippedNoTeam, CreatureBanditRules.RouteA(true, false, false, true));
        Assert.AreEqual(CreatureRouteA.SkippedNoPelvisBone, CreatureBanditRules.RouteA(true, false, true, false));
    }

    [TestMethod]
    public void ShouldBackstopRoutedRemoval_StillMountableCreatureRoutedWithNoAttacker_ReturnsTrue()
        => Assert.IsTrue(CreatureBanditRules.ShouldBackstopRoutedRemoval(isCreatureBandit: true, isMount: true, AgentState.Routed, hasAffector: false));

    [TestMethod]
    public void ShouldBackstopRoutedRemoval_RouteACreature_IsNoMount_SandBoxCountsItItself_ReturnsFalse()
    {
        // SandBox skips only an IsMount agent with no attacker (BattleAgentLogic.cs:147); route A cleared Mountable.
        Assert.IsFalse(CreatureBanditRules.ShouldBackstopRoutedRemoval(isCreatureBandit: true, isMount: false, AgentState.Routed, hasAffector: false));
    }

    [TestMethod]
    public void ShouldBackstopRoutedRemoval_CreatureRoutedByAnAttacker_ReturnsFalse()
    {
        // BattleAgentLogic.OnAgentRemoved already calls SetRouted when an affector is present.
        Assert.IsFalse(CreatureBanditRules.ShouldBackstopRoutedRemoval(isCreatureBandit: true, isMount: true, AgentState.Routed, hasAffector: true));
    }

    [TestMethod]
    public void ShouldBackstopRoutedRemoval_CreatureKilledOrUnconscious_ReturnsFalse()
    {
        Assert.IsFalse(CreatureBanditRules.ShouldBackstopRoutedRemoval(isCreatureBandit: true, isMount: true, AgentState.Killed, hasAffector: false));
        Assert.IsFalse(CreatureBanditRules.ShouldBackstopRoutedRemoval(isCreatureBandit: true, isMount: true, AgentState.Unconscious, hasAffector: false));
    }

    [TestMethod]
    public void ShouldBackstopRoutedRemoval_NotACreature_ReturnsFalse()
        => Assert.IsFalse(CreatureBanditRules.ShouldBackstopRoutedRemoval(isCreatureBandit: false, isMount: true, AgentState.Routed, hasAffector: false));

    [TestMethod]
    [DataRow(true, false, true, DisplayName = "battle: AI ticks, nobody teleports")]
    [DataRow(false, true, false, DisplayName = "order of battle: AI off, agents teleport")]
    [DataRow(false, false, false, DisplayName = "deployment before the teleport switch")]
    [DataRow(true, true, false, DisplayName = "enemy AI setup tick: a scripted move would teleport")]
    public void MayFight_OnlyOnceDeploymentIsOver(bool allowAiTicking, bool isTeleportingAgents, bool expected)
    {
        // DeploymentMissionController turns AI ticking off for the whole deployment, and while IsTeleportingAgents is
        // set Agent.SetScriptedPosition teleports (v1.5.3 Agent.cs:2462-2465): a hunt would land the creature on the
        // paused army.
        Assert.AreEqual(expected, CreatureBanditRules.MayFight(allowAiTicking, isTeleportingAgents));
    }

    [TestMethod]
    public void ScoreboardCasualty_MatchesVanillasColumnsPerRemoval()
    {
        // BattleObserverMissionLogic.OnAgentRemoved (v1.5.3 :54-70): killed, unconscious as wounded, routed.
        Assert.AreEqual((1, 0, 0), CreatureBanditRules.ScoreboardCasualty(AgentState.Killed));
        Assert.AreEqual((0, 1, 0), CreatureBanditRules.ScoreboardCasualty(AgentState.Unconscious));
        Assert.AreEqual((0, 0, 1), CreatureBanditRules.ScoreboardCasualty(AgentState.Routed));
        Assert.IsNull(CreatureBanditRules.ScoreboardCasualty(AgentState.Active), "vanilla reports no other state");
    }

    [TestMethod]
    [TestCategory("RequiresGame")]   // constructs an engine character and origin
    public void IsScoreboardRow_ACustomBattleConsoleOrigin_WithNoCombatant_Counts()
    {
        // Codex 2026-09-28 second pass: BasicBattleAgentOrigin's BattleCombatant is null (v1.5.3
        // BasicBattleAgentOrigin.cs:25), and the scoreboard groups a null combatant under "Party"
        // (SPScoreboardPartyVM). Vanilla's observer asks no more than a team, an origin and a troop.
        var troop = new BasicCharacterObject();
        var consoleOrigin = new TaleWorlds.MountAndBlade.BasicBattleAgentOrigin(troop);
        Assert.IsNull(((IAgentOriginBase)consoleOrigin).BattleCombatant, "the premise: a console origin has no combatant");

        Assert.IsTrue(CreatureBanditRules.IsScoreboardRow(teamValid: true, consoleOrigin, troop));
        Assert.IsFalse(CreatureBanditRules.IsScoreboardRow(teamValid: false, consoleOrigin, troop), "no team, no side to count on");
        Assert.IsFalse(CreatureBanditRules.IsScoreboardRow(teamValid: true, origin: null, troop));
        Assert.IsFalse(CreatureBanditRules.IsScoreboardRow(teamValid: true, consoleOrigin, character: null));
    }

    [TestMethod]
    [DataRow(false, true, true, false, AgentState.Killed, true, DisplayName = "a soldier kills a spider: credit him")]
    [DataRow(true, false, false, true, AgentState.Killed, true, DisplayName = "a spider kills a soldier: credit it")]
    [DataRow(true, false, false, true, AgentState.Unconscious, true, DisplayName = "a spider knocks a soldier out: credit it")]
    [DataRow(true, false, true, false, AgentState.Killed, false, DisplayName = "soldier kills soldier: vanilla already credits")]
    [DataRow(false, true, true, false, AgentState.Routed, false, DisplayName = "a rout is no kill")]
    [DataRow(false, false, false, true, AgentState.Killed, false, DisplayName = "a spider kills a loose horse: no troop")]
    [DataRow(false, true, false, false, AgentState.Killed, false, DisplayName = "no troop dealt the blow")]
    public void ScoreboardBridgeCreditsKill_OnlyWhereVanillasHumanGateDropsIt(bool victimHuman, bool victimCreature,
        bool affectorHuman, bool affectorCreature, AgentState state, bool expected)
    {
        // Vanilla credits a kill only when both agents are human (BattleObserverMissionLogic.cs:72-75); the bridge adds
        // the credits a creature bandit's part in the kill would otherwise lose, and never repeats vanilla's.
        Assert.AreEqual(expected, CreatureBanditRules.ScoreboardBridgeCreditsKill(victimHuman, victimCreature,
            affectorHuman, affectorCreature, state));
    }

    [TestMethod]
    [DataRow(true, false, false, true, DisplayName = "wandered off: patrol again")]
    [DataRow(true, true, false, false, DisplayName = "in a battle: leave it")]
    [DataRow(true, false, true, false, DisplayName = "already patrolling")]
    [DataRow(false, false, false, false, DisplayName = "no home to patrol")]
    public void NeedsPatrolOrder_OnlyAnIdleBroodWithAHome(bool hasHome, bool inBattle, bool isPatrolling, bool expected)
        => Assert.AreEqual(expected, CreatureBanditRules.NeedsPatrolOrder(hasHome, inBattle, isPatrolling));

    [TestMethod]
    public void WithoutFlightFlags_ClearsRunAwayWanderScareAndRear_KeepsTheRest()
    {
        // CanRear too: a blow carrying MakesRear rears a CanRear mount (Mission.cs:5644-5646), which interrupts a
        // bite, and nobody rides a creature bandit for a rear to throw off.
        const AgentFlag spiderFlags = AgentFlag.Mountable | AgentFlag.CanRear | AgentFlag.RunsAwayWhenHit
                                      | AgentFlag.CanCharge | AgentFlag.CanWander | AgentFlag.CanGetScared;

        Assert.AreEqual(AgentFlag.Mountable | AgentFlag.CanCharge, CreatureBanditRules.WithoutFlightFlags(spiderFlags));
    }

    [TestMethod]
    public void BroodsToSpawnToday_BelowTheCap_SpawnsOne()
        => Assert.AreEqual(1, CreatureBanditRules.BroodsToSpawnToday(existingBroods: 2, maxBroods: 4, enabled: true));

    [TestMethod]
    public void BroodsToSpawnToday_AtOrOverTheCap_SpawnsNone()
    {
        Assert.AreEqual(0, CreatureBanditRules.BroodsToSpawnToday(existingBroods: 4, maxBroods: 4, enabled: true));
        Assert.AreEqual(0, CreatureBanditRules.BroodsToSpawnToday(existingBroods: 9, maxBroods: 4, enabled: true));
    }

    [TestMethod]
    public void BroodsToSpawnToday_NoCap_SpawnsNone()
        => Assert.AreEqual(0, CreatureBanditRules.BroodsToSpawnToday(existingBroods: 0, maxBroods: 0, enabled: true));

    [TestMethod]
    public void BroodsToSpawnToday_SwitchedOff_SpawnsNone()
    {
        // The MCM switch stops new broods only; live broods keep their patrols, and the looter cap of 0 still
        // keeps vanilla from spawning the clan map-wide.
        Assert.AreEqual(0, CreatureBanditRules.BroodsToSpawnToday(existingBroods: 0, maxBroods: 4, enabled: false));
    }

    [TestMethod]
    public void MaxBroods_IsTwenty()
        => Assert.AreEqual(20, CreatureBanditsConfig.MaxBroods, "#694: up to twenty broods around Mirkwood and Dol Guldur");

    [TestMethod]
    public void TrollBanditTroopIds_AreTheCaveAndHillTwins()
        => CollectionAssert.AreEquivalent(new[] { "taom_troll_bandit_cave", "taom_troll_bandit_hill" },
            CreatureBanditsConfig.TrollBanditTroopIds.ToList());

    [TestMethod]
    public void IsTrollBanditTroop_TheTwinsOnly()
    {
        Assert.IsTrue(CreatureBanditRules.IsTrollBanditTroop("taom_troll_bandit_cave"));
        Assert.IsTrue(CreatureBanditRules.IsTrollBanditTroop("taom_troll_bandit_hill"));
        // Mordor's own trolls are a lord's troops: capture and recruitment stay vanilla for them.
        Assert.IsFalse(CreatureBanditRules.IsTrollBanditTroop("cave_troll"));
        Assert.IsFalse(CreatureBanditRules.IsTrollBanditTroop("hill_troll"));
        Assert.IsFalse(CreatureBanditRules.IsTrollBanditTroop(Brood));
        Assert.IsFalse(CreatureBanditRules.IsTrollBanditTroop("TAOM_TROLL_BANDIT_CAVE"));
        Assert.IsFalse(CreatureBanditRules.IsTrollBanditTroop(null));
    }

    [TestMethod]
    public void TrollTwins_AreNotCreatures()
    {
        // A troll is humanoid and fights as an ordinary troop: the battle swap, the creature tree and the map icon's
        // rider skip all key on IsCreatureTroop and must never take one.
        Assert.IsFalse(CreatureBanditRules.IsCreatureTroop("taom_troll_bandit_cave"));
        Assert.IsFalse(CreatureBanditRules.IsCreatureTroop("taom_troll_bandit_hill"));
    }

    [TestMethod]
    public void IsTrollBandClan_TheTrollClanOnly()
    {
        Assert.IsTrue(CreatureBanditRules.IsTrollBandClan("wild_trolls"));
        Assert.IsFalse(CreatureBanditRules.IsTrollBandClan("mirkwood_spiders"));
        Assert.IsFalse(CreatureBanditRules.IsTrollBandClan("looters"));
        Assert.IsFalse(CreatureBanditRules.IsTrollBandClan(null));
    }

    [TestMethod]
    public void IsCreatureBandClan_TheBroodAndTheTrollClans()
    {
        // The looter cap of 0 and the no-parley patch ask this: both clans are looter factions spawned by TAOM alone.
        Assert.IsTrue(CreatureBanditRules.IsCreatureBandClan("mirkwood_spiders"));
        Assert.IsTrue(CreatureBanditRules.IsCreatureBandClan("wild_trolls"));
        Assert.IsFalse(CreatureBanditRules.IsCreatureBandClan("looters"));
        Assert.IsFalse(CreatureBanditRules.IsCreatureBandClan("mirkwood_stalkers"));
        Assert.IsFalse(CreatureBanditRules.IsCreatureBandClan(null));
    }

    [TestMethod]
    public void IsNeverPrisoner_SpidersAndTrollTwins_NotMordorTrolls()
    {
        Assert.IsTrue(CreatureBanditRules.IsNeverPrisoner(Brood));
        Assert.IsTrue(CreatureBanditRules.IsNeverPrisoner("taom_troll_bandit_cave"));
        Assert.IsTrue(CreatureBanditRules.IsNeverPrisoner("taom_troll_bandit_hill"));
        Assert.IsFalse(CreatureBanditRules.IsNeverPrisoner("cave_troll"));
        Assert.IsFalse(CreatureBanditRules.IsNeverPrisoner("looter"));
        Assert.IsFalse(CreatureBanditRules.IsNeverPrisoner(null));
    }

    // KingdomsOwedATrollBand: one id list per case. Bands are listed by the kingdom their home settlement belongs to
    // now, null for a home outside any kingdom (a kingdomless clan's fief).
    [TestMethod]
    [DataRow("a,b,c", "", true, "a,b,c", DisplayName = "new campaign: every kingdom is owed one")]
    [DataRow("a,b,c", "a,b", true, "c", DisplayName = "one kingdom still without a band")]
    [DataRow("a,b,c", "a,b,c", true, "", DisplayName = "every kingdom has one")]
    [DataRow("a,b", "a,a", true, "", DisplayName = "a capture moved a band: the total is at the cap")]
    [DataRow("a,b", "a,~", true, "", DisplayName = "a band homed outside any kingdom still counts toward the cap")]
    [DataRow("a,b,c", "a,~", true, "b,c", DisplayName = "a kingdomless band covers no kingdom")]
    [DataRow("a", "a,b,c", true, "", DisplayName = "kingdoms eliminated after their bands spawned")]
    [DataRow("a,b,c", "", false, "", DisplayName = "switched off")]
    [DataRow("", "", true, "", DisplayName = "no living kingdom")]
    public void KingdomsOwedATrollBand_OneBandPerLivingKingdom(string living, string bands, bool enabled, string expected)
    {
        static string[] Ids(string csv) => csv.Length == 0 ? new string[0] : csv.Split(',');
        var bandKingdoms = Ids(bands).Select(id => id == "~" ? null : id).ToList();
        CollectionAssert.AreEquivalent(Ids(expected),
            CreatureBanditRules.KingdomsOwedATrollBand(Ids(living), bandKingdoms, enabled).ToList());
    }

    [TestMethod]
    public void WithoutRefusedWinners_NoneRefused_ReturnsNullSoTheEngineListStands()
    {
        var chances = new[] { new KeyValuePair<string, float>("lord", 0.75f), new KeyValuePair<string, float>("ally", 0.25f) };
        Assert.IsNull(CreatureBanditRules.WithoutRefusedWinners(chances, w => w == "trolls"));
    }

    [TestMethod]
    public void WithoutRefusedWinners_DropsTheBand_AndTheOthersShareItsChance()
    {
        // A freed prisoner goes to a winner drawn from these chances (MapEvent.FindWinnerPartyToGetCurrentLootObjectBasedOnChances):
        // left un-renormalised, the band's share would free the prisoner instead of handing it to the lord.
        var chances = new[] { new KeyValuePair<string, float>("trolls", 0.5f), new KeyValuePair<string, float>("lord", 0.25f),
            new KeyValuePair<string, float>("ally", 0.25f) };
        var kept = CreatureBanditRules.WithoutRefusedWinners(chances, w => w == "trolls")!;
        CollectionAssert.AreEqual(new[] { "lord", "ally" }, kept.Select(c => c.Key).ToList());
        Assert.AreEqual(0.5f, kept[0].Value, 1e-6f);
        Assert.AreEqual(0.5f, kept[1].Value, 1e-6f);
    }

    [TestMethod]
    public void WithoutRefusedWinners_OnlyBandsWon_NoOneTakesThePrisoner()
    {
        // #694 (Mike): bands stay trolls only. With no other winner the prisoner goes free, as vanilla does for any
        // prisoner no winner may take.
        var chances = new[] { new KeyValuePair<string, float>("trolls", 1f) };
        Assert.AreEqual(0, CreatureBanditRules.WithoutRefusedWinners(chances, w => w == "trolls")!.Count);
    }

    [TestMethod]
    public void WithoutFlightFlags_NoFlightFlags_ReturnsInputUnchanged()
    {
        const AgentFlag flags = AgentFlag.Mountable | AgentFlag.CanCharge;
        Assert.AreEqual(flags, CreatureBanditRules.WithoutFlightFlags(flags));
    }
}
