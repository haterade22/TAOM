using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.DwarfAttacks;
using static TAOM.Features.DwarfAttacks.DwarfAttacksConfig;

// The pure decisions behind the dwarves' head butt and drop kick (#1087): who gets the tree, when each attack may
// start, which one the nearest enemy's distance picks, when the clip reaches its impact, who it can hit, and what the
// victim takes. Every float is an engine value, so each rule is also pinned against NaN and infinity (fail closed).

namespace TAOM.Tests.Features.DwarfAttacks;

[TestClass]
public class DwarfAttackServiceTests
{
    private DwarfAttackService _service = null!;

    [TestInitialize]
    public void Setup() => _service = new DwarfAttackService();

    [TestMethod]
    public void IsDwarf_TheBattleMonster_IsTrue()
    {
        Assert.IsTrue(_service.IsDwarf("dwarf"));
    }

    [DataTestMethod]
    [DataRow("dwarf_child")]   // children never fight
    [DataRow("DWARF")]
    [DataRow("human")]
    [DataRow("")]
    [DataRow(null)]
    public void IsDwarf_AnyOtherMonster_IsFalse(string? monsterId)
    {
        Assert.IsFalse(_service.IsDwarf(monsterId));
    }

    // --- cooldown ---

    [DataTestMethod]
    [DataRow(DwarfAttack.HeadButt)]
    [DataRow(DwarfAttack.DropKick)]
    public void IsOffCooldown_NeverFired_IsTrue(DwarfAttack attack)
    {
        Assert.IsTrue(_service.IsOffCooldown(attack, null, 5f));
    }

    [TestMethod]
    public void IsOffCooldown_HeadButt_UsesItsOwnCooldownInclusive()
    {
        Assert.IsFalse(_service.IsOffCooldown(DwarfAttack.HeadButt, 100f, 100f + HeadButtCooldownSeconds - 0.01f));
        Assert.IsTrue(_service.IsOffCooldown(DwarfAttack.HeadButt, 100f, 100f + HeadButtCooldownSeconds));
    }

    [TestMethod]
    public void IsOffCooldown_DropKick_UsesItsOwnCooldownInclusive()
    {
        Assert.IsFalse(_service.IsOffCooldown(DwarfAttack.DropKick, 100f, 100f + DropKickCooldownSeconds - 0.01f));
        Assert.IsTrue(_service.IsOffCooldown(DwarfAttack.DropKick, 100f, 100f + DropKickCooldownSeconds));
    }

    [TestMethod]
    public void IsOffCooldown_StampInTheFuture_IsOnCooldown()
    {
        Assert.IsFalse(_service.IsOffCooldown(DwarfAttack.HeadButt, 50f, 10f));
    }

    [DataTestMethod]
    [DataRow(float.NaN, 10f)]
    [DataRow(1f, float.NaN)]
    [DataRow(float.PositiveInfinity, 10f)]
    [DataRow(1f, float.PositiveInfinity)]
    public void IsOffCooldown_NonFiniteTime_FailsClosed(float last, float now)
    {
        Assert.IsFalse(_service.IsOffCooldown(DwarfAttack.HeadButt, last, now));
    }

    // --- choice ---

    [TestMethod]
    public void ChooseAttack_EnemyInHeadButtRange_PicksTheHeadButt()
    {
        Assert.AreEqual(DwarfAttack.HeadButt, _service.ChooseAttack(HeadButtMaxRange, 1f, true, true));
        Assert.AreEqual(DwarfAttack.HeadButt, _service.ChooseAttack(0f, 1f, true, true));
    }

    [TestMethod]
    public void ChooseAttack_EnemyInDropKickRange_PicksTheDropKick()
    {
        Assert.AreEqual(DwarfAttack.DropKick, _service.ChooseAttack(DropKickMinRange, 1f, true, true));
        Assert.AreEqual(DwarfAttack.DropKick, _service.ChooseAttack(DropKickMaxRange, 1f, true, true));
    }

    [TestMethod]
    public void ChooseAttack_HeadButtOnCooldown_CloseEnemyGetsNothing()
    {
        // A drop kick needs its run-up: a close enemy never gets one in place of the butt.
        Assert.IsNull(_service.ChooseAttack(HeadButtMaxRange * 0.5f, 1f, headButtReady: false, dropKickReady: true));
    }

    [TestMethod]
    public void ChooseAttack_DropKickOnCooldown_MidEnemyGetsNothing()
    {
        Assert.IsNull(_service.ChooseAttack((DropKickMinRange + DropKickMaxRange) / 2f, 1f, headButtReady: true, dropKickReady: false));
    }

    [TestMethod]
    public void ChooseAttack_BetweenTheBands_PicksNothing()
    {
        float between = (HeadButtMaxRange + DropKickMinRange) / 2f;
        Assert.IsNull(_service.ChooseAttack(between, 1f, true, true));
    }

    [TestMethod]
    public void ChooseAttack_BeyondDropKickRange_PicksNothing()
    {
        Assert.IsNull(_service.ChooseAttack(DropKickMaxRange + 0.01f, 1f, true, true));
    }

    [TestMethod]
    public void ChooseAttack_EnemyNotInFront_PicksNothing()
    {
        Assert.IsNull(_service.ChooseAttack(0.5f, FacingDot, true, true));   // strictly above the cosine
        Assert.IsNull(_service.ChooseAttack(2.2f, -1f, true, true));        // the scan's "no enemy" value
    }

    [DataTestMethod]
    [DataRow(float.NaN, 1f)]
    [DataRow(1f, float.NaN)]
    [DataRow(float.PositiveInfinity, 1f)]
    [DataRow(-0.1f, 1f)]
    public void ChooseAttack_BadInput_PicksNothing(float distance, float facing)
    {
        Assert.IsNull(_service.ChooseAttack(distance, facing, true, true));
    }

    // --- impact ---

    [TestMethod]
    public void HasReachedImpact_EachAttack_UsesItsOwnFraction()
    {
        Assert.IsFalse(_service.HasReachedImpact(DwarfAttack.HeadButt, HeadButtImpactFraction - 0.01f));
        Assert.IsTrue(_service.HasReachedImpact(DwarfAttack.HeadButt, HeadButtImpactFraction));
        Assert.IsFalse(_service.HasReachedImpact(DwarfAttack.DropKick, DropKickImpactFraction - 0.01f));
        Assert.IsTrue(_service.HasReachedImpact(DwarfAttack.DropKick, DropKickImpactFraction));
    }

    [TestMethod]
    public void HasReachedImpact_NaN_IsFalse()
    {
        Assert.IsFalse(_service.HasReachedImpact(DwarfAttack.HeadButt, float.NaN));
    }

    // --- who can be hit ---

    [TestMethod]
    public void CanHit_InsideReachAndInFront_IsTrue()
    {
        Assert.IsTrue(_service.CanHit(DwarfAttack.HeadButt, HeadButtReach, 1f));
        Assert.IsTrue(_service.CanHit(DwarfAttack.DropKick, DropKickReach, 1f));
    }

    [TestMethod]
    public void CanHit_BeyondItsOwnReach_IsFalse()
    {
        Assert.IsFalse(_service.CanHit(DwarfAttack.HeadButt, HeadButtReach + 0.01f, 1f));
        Assert.IsFalse(_service.CanHit(DwarfAttack.DropKick, DropKickReach + 0.01f, 1f));
    }

    [TestMethod]
    public void CanHit_NotInFront_IsFalse()
    {
        Assert.IsFalse(_service.CanHit(DwarfAttack.HeadButt, 0.5f, HitFacingDot));
    }

    [DataTestMethod]
    [DataRow(float.NaN, 1f)]
    [DataRow(0.5f, float.NaN)]
    [DataRow(-0.1f, 1f)]
    public void CanHit_BadInput_IsFalse(float distance, float facing)
    {
        Assert.IsFalse(_service.CanHit(DwarfAttack.HeadButt, distance, facing));
    }

    [TestMethod]
    public void Reach_IsEachAttacksOwn()
    {
        Assert.AreEqual(HeadButtReach, _service.Reach(DwarfAttack.HeadButt));
        Assert.AreEqual(DropKickReach, _service.Reach(DwarfAttack.DropKick));
    }

    // --- the blow ---

    [TestMethod]
    public void DecideBlow_HeadButt_StaggersButNeverFloors()
    {
        DwarfAttackBlow? blow = _service.DecideBlow(DwarfAttack.HeadButt, shieldBlocked: false, damagePercent: 100);
        Assert.AreEqual(new DwarfAttackBlow(HeadButtDamage, KnockDown: false), blow);
    }

    [TestMethod]
    public void DecideBlow_DropKick_FloorsTheVictim()
    {
        DwarfAttackBlow? blow = _service.DecideBlow(DwarfAttack.DropKick, shieldBlocked: false, damagePercent: 100);
        Assert.AreEqual(new DwarfAttackBlow(DropKickDamage, KnockDown: true), blow);
    }

    [TestMethod]
    public void DecideBlow_DropKickIntoAShield_ScalesAndDoesNotFloor()
    {
        DwarfAttackBlow? blow = _service.DecideBlow(DwarfAttack.DropKick, shieldBlocked: true, damagePercent: 100);
        Assert.AreEqual(new DwarfAttackBlow((int)System.Math.Round(DropKickDamage * ShieldBlockedMultiplier), KnockDown: false), blow);
    }

    [TestMethod]
    public void DecideBlow_DamagePercent_ScalesTheDamage()
    {
        Assert.AreEqual(HeadButtDamage * 2, _service.DecideBlow(DwarfAttack.HeadButt, false, 200)!.Value.Damage);
        Assert.AreEqual((int)System.Math.Round(HeadButtDamage * 0.5), _service.DecideBlow(DwarfAttack.HeadButt, false, 50)!.Value.Damage);
    }

    [TestMethod]
    public void DecideBlow_DamagePercentAboveTheCap_IsCapped()
    {
        Assert.AreEqual(_service.DecideBlow(DwarfAttack.DropKick, false, MaxDamagePercent),
            _service.DecideBlow(DwarfAttack.DropKick, false, MaxDamagePercent + 500));
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(-50)]
    public void DecideBlow_NoDamage_IsNoBlow(int percent)
    {
        // Zero damage must not still floor someone: the slider at 0 turns the hit off.
        Assert.IsNull(_service.DecideBlow(DwarfAttack.DropKick, false, percent));
    }

    // --- readiness: an attack whose action did not resolve is never ready (deep review 2026-11-06) ---

    [DataTestMethod]
    [DataRow(DwarfAttack.HeadButt)]
    [DataRow(DwarfAttack.DropKick)]
    public void IsReady_ActionNotResolved_IsFalseEvenOffCooldown(DwarfAttack attack)
    {
        // A missing Armory action resolves to act_none, and SetActionChannel(0, act_none) clears the dwarf's channel.
        Assert.IsFalse(_service.IsReady(attack, actionResolved: false, lastFired: null, now: 100f));
    }

    [DataTestMethod]
    [DataRow(null, 100f, true)]
    [DataRow(99f, 100f, false)]
    [DataRow(0f, 100f, true)]
    public void IsReady_ActionResolved_FollowsTheCooldown(float? lastFired, float now, bool expected)
    {
        Assert.AreEqual(expected, _service.IsReady(DwarfAttack.HeadButt, actionResolved: true, lastFired, now));
    }

    // --- height: the engine's nearby query is flat, so a dwarf on a wall would hit men below it ---

    [DataTestMethod]
    [DataRow(0f)]
    [DataRow(MaxHeightDifference)]
    [DataRow(-MaxHeightDifference)]
    public void IsWithinHeight_SameLevel_IsTrue(float heightDifference)
    {
        Assert.IsTrue(_service.IsWithinHeight(heightDifference));
    }

    [DataTestMethod]
    [DataRow(MaxHeightDifference + 0.01f)]
    [DataRow(-3f)]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    public void IsWithinHeight_AnotherLevelOrNotFinite_IsFalse(float heightDifference)
    {
        Assert.IsFalse(_service.IsWithinHeight(heightDifference));
    }

    [TestMethod]
    public void MayAttack_AnAiDwarfOnFoot_IsTrue()
    {
        Assert.IsTrue(_service.MayAttack(isPlayersOwn: false, mounted: false, onMachine: false));
    }

    // The player's own dwarf never attacks, even after TAOM hands him to the AI (Battle Command seat, Fords of War,
    // the Long Defeat). A rider has no clip; a dwarf on a ballista or a ladder keeps to it.
    [DataTestMethod]
    [DataRow(true, false, false)]
    [DataRow(false, true, false)]
    [DataRow(false, false, true)]
    public void MayAttack_PlayersOwnMountedOrOnAMachine_IsFalse(bool isPlayersOwn, bool mounted, bool onMachine)
    {
        Assert.IsFalse(_service.MayAttack(isPlayersOwn, mounted, onMachine));
    }

    // Every idle dwarf scans once a period; the same period for all would put every scan of a battle in one frame.
    [TestMethod]
    public void IdleScanPeriod_SpreadsDwarvesAcrossAWindow()
    {
        var periods = Enumerable.Range(0, 300).Select(i => _service.IdleScanPeriod(i).TotalMilliseconds).ToList();
        Assert.AreEqual(IdleScanMinMilliseconds, periods.Min());
        Assert.AreEqual(IdleScanMinMilliseconds + IdleScanSpreadMilliseconds - 1, periods.Max());
        Assert.AreNotEqual(periods[0], periods[1]);
    }

    // A slider low enough that the blow rounds to nothing turns the hit off, like 0 %, so no hit is recorded.
    [DataTestMethod]
    [DataRow(DwarfAttack.DropKick, 1)]
    [DataRow(DwarfAttack.HeadButt, 2)]
    public void DecideBlow_RoundsToZero_IsNull(DwarfAttack attack, int damagePercent)
    {
        Assert.IsNull(_service.DecideBlow(attack, shieldBlocked: false, damagePercent));
    }
}
