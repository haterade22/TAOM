using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Stamina;
using TAOM.Features.Stamina.Domain;
using TaleWorlds.MountAndBlade;

namespace TAOM.Tests.Features.Stamina;

[TestClass]
public class StaminaServiceTests
{
    private const float Delta = 0.0001f;
    private static readonly StaminaEconomy E = StaminaEconomy.Default;

    private static StaminaCell Cell(float max = 100f, float current = 100f) =>
        new StaminaCell(0f) { Max = max, Current = current };

    private static StaminaSample Idle(float now, float dt) => new StaminaSample(now, dt, running: false, blocking: false, bowHeldSeconds: 0f);

    [TestMethod]
    public void ActionTypeNumbers_MatchTheEngineEnum()
    {
        // The rules hold the engine's action types as numbers. An engine update that moves one must fail here.
        Assert.AreEqual((int)Agent.ActionCodeType.DefendFist, StaminaService.DefendFirst);
        Assert.AreEqual((int)Agent.ActionCodeType.DefendLeftStaff, StaminaService.DefendLast);
        Assert.AreEqual((int)Agent.ActionCodeType.ReadyRanged, StaminaService.ReadyRanged);
        Assert.AreEqual((int)Agent.ActionCodeType.ReleaseRanged, StaminaService.ReleaseRanged);
        Assert.AreEqual((int)Agent.ActionCodeType.ReleaseThrowing, StaminaService.ReleaseThrowing);
        Assert.AreEqual((int)Agent.ActionCodeType.ReleaseMelee, StaminaService.ReleaseMelee);
        Assert.AreEqual((int)Agent.ActionCodeType.JumpStart, StaminaService.JumpStart);
        Assert.AreEqual((int)Agent.ActionCodeType.Kick, StaminaService.Kick);
        Assert.AreEqual((int)Agent.ActionCodeType.WeaponBash, StaminaService.WeaponBash);
    }

    [DataTestMethod]
    [DataRow(0, false, 100f, DisplayName = "tier 0 recruit")]
    [DataRow(1, false, 110f)]
    [DataRow(5, false, 150f)]
    [DataRow(7, false, 170f)]
    [DataRow(7, true, 200f, DisplayName = "a hero is tier 7 plus the bonus")]
    [DataRow(-3, false, 100f, DisplayName = "a tier below 0 counts as 0")]
    [DataRow(40, false, 170f, DisplayName = "a tier above 7 counts as 7")]
    public void MaxPool_ByTierAndHero(int tier, bool hero, float expected)
    {
        Assert.AreEqual(expected, StaminaService.MaxPool(E, tier, hero), Delta);
    }

    [DataTestMethod]
    [DataRow(0f, 5f, DisplayName = "bare hand: the minimum")]
    [DataRow(0.3f, 5f, DisplayName = "a dagger stays at the minimum")]
    [DataRow(1.2f, 6.4f, DisplayName = "a one-handed sword")]
    [DataRow(2.5f, 9f, DisplayName = "a two-handed sword")]
    [DataRow(9f, 14f, DisplayName = "a troll's club stops at the maximum")]
    [DataRow(float.NaN, 5f)]
    [DataRow(float.PositiveInfinity, 5f)]
    [DataRow(-2f, 5f)]
    public void SwingCost_ByWeaponWeight(float kg, float expected)
    {
        Assert.AreEqual(expected, StaminaService.SwingCost(E, kg), Delta);
    }

    [TestMethod]
    public void ActionCost_EachAction_IsItsOwnValue()
    {
        Assert.AreEqual(6.4f, StaminaService.ActionCost(E, StaminaAction.Swing, 1.2f), Delta);
        Assert.AreEqual(E.BowShot, StaminaService.ActionCost(E, StaminaAction.BowShot, 0f));
        Assert.AreEqual(E.CrossbowShot, StaminaService.ActionCost(E, StaminaAction.CrossbowShot, 0f));
        Assert.AreEqual(E.Throw, StaminaService.ActionCost(E, StaminaAction.Throw, 0f));
        Assert.AreEqual(E.Jump, StaminaService.ActionCost(E, StaminaAction.Jump, 0f));
        Assert.AreEqual(E.Kick, StaminaService.ActionCost(E, StaminaAction.Kick, 0f));
        Assert.AreEqual(E.Bash, StaminaService.ActionCost(E, StaminaAction.Bash, 0f));
        Assert.AreEqual(0f, StaminaService.ActionCost(E, StaminaAction.None, 5f));
    }

    [TestMethod]
    public void BlockCost_ByHowTheBlowWasStopped()
    {
        Assert.AreEqual(E.Parry, StaminaService.BlockCost(E, StaminaBlock.WeaponParry, 80f));
        Assert.AreEqual(E.MissileBlock, StaminaService.BlockCost(E, StaminaBlock.ShieldMissile, 80f));
        Assert.AreEqual(3f + 0.05f * 40f, StaminaService.BlockCost(E, StaminaBlock.ShieldMelee, 40f), Delta);
        Assert.AreEqual(E.ShieldMax, StaminaService.BlockCost(E, StaminaBlock.ShieldMelee, 5000f), "a huge blow on a shield stops at the maximum");
        Assert.AreEqual(E.ShieldBase, StaminaService.BlockCost(E, StaminaBlock.ShieldMelee, float.NaN));
        Assert.AreEqual(E.ShieldBase, StaminaService.BlockCost(E, StaminaBlock.ShieldMelee, -10f));
    }

    [DataTestMethod]
    [DataRow(19, 20, false, StaminaAction.Swing, DisplayName = "ready to release: a swing, hit or miss")]
    [DataRow(15, 16, false, StaminaAction.BowShot)]
    [DataRow(15, 16, true, StaminaAction.CrossbowShot)]
    [DataRow(35, 17, false, StaminaAction.Throw)]
    [DataRow(35, 24, false, StaminaAction.Jump)]
    [DataRow(35, 28, false, StaminaAction.Kick)]
    [DataRow(35, 31, false, StaminaAction.Bash)]
    [DataRow(20, 20, false, StaminaAction.None, DisplayName = "a release still running is not a second swing")]
    [DataRow(20, 19, false, StaminaAction.None, DisplayName = "readying a swing costs nothing; a feint is free")]
    [DataRow(35, 18, false, StaminaAction.None, DisplayName = "a reload costs nothing")]
    [DataRow(24, 25, false, StaminaAction.None, DisplayName = "the jump is charged once, at its start")]
    [DataRow(28, 30, false, StaminaAction.None, DisplayName = "the kick is charged once")]
    public void Classify_OnEntryToAnAction(int previous, int next, bool crossbow, StaminaAction expected)
    {
        Assert.AreEqual(expected, StaminaService.Classify(previous, next, crossbow));
    }

    [TestMethod]
    public void IsBlocking_EveryDefendType_AndNothingElse()
    {
        Assert.IsFalse(StaminaService.IsBlocking(0));
        for (var type = 1; type <= 14; type++)
            Assert.IsTrue(StaminaService.IsBlocking(type), "type " + type);
        Assert.IsFalse(StaminaService.IsBlocking(15));
        Assert.IsTrue(StaminaService.IsDrawn(15));
        Assert.IsFalse(StaminaService.IsDrawn(16));
    }

    [TestMethod]
    public void Charge_OwnAction_CostsAndPausesRegeneration()
    {
        var cell = Cell();

        StaminaService.Charge(cell, 6.4f, now: 10f, E);

        Assert.AreEqual(93.6f, cell.Current, Delta);
        Assert.AreEqual(10f + E.RegenPauseSeconds, cell.RegenPausedUntil, Delta);
        Assert.IsTrue(cell.EverFought);
    }

    [TestMethod]
    public void Charge_MoreThanIsLeft_StopsAtZero()
    {
        var cell = Cell(current: 3f);

        StaminaService.Charge(cell, 9f, now: 0f, E);

        Assert.AreEqual(0f, cell.Current);
    }

    [TestMethod]
    public void Charge_UsesTheSoldiersCostFactor()
    {
        var cell = Cell();
        cell.CostFactor = 1.5f;

        StaminaService.Charge(cell, 10f, now: 0f, E);

        Assert.AreEqual(85f, cell.Current, Delta);
    }

    [TestMethod]
    public void Charge_WhileExhausted_CostsNothingAndDoesNotPause()
    {
        var cell = Cell(current: 10f);
        cell.Publish(StaminaBand.Exhausted, 0.1f);

        StaminaService.Charge(cell, 9f, now: 50f, E);

        Assert.AreEqual(10f, cell.Current);
        Assert.AreEqual(0f, cell.RegenPausedUntil);
    }

    [DataTestMethod]
    [DataRow(0f)]
    [DataRow(-4f)]
    [DataRow(float.NaN)]
    public void Charge_CostNotAPositiveNumber_ChangesNothing(float cost)
    {
        var cell = Cell();

        StaminaService.Charge(cell, cost, now: 0f, E);

        Assert.AreEqual(100f, cell.Current);
        Assert.IsFalse(cell.EverFought);
    }

    [TestMethod]
    public void Step_AtRest_RegeneratesAShareOfThePoolPerSecond()
    {
        var cell = Cell(max: 200f, current: 100f);

        StaminaService.Step(cell, Idle(now: 10f, dt: 2f), E);

        Assert.AreEqual(100f + 0.05f * 200f * 2f, cell.Current, Delta);
    }

    [TestMethod]
    public void Step_AtRest_NeverPassesThePool()
    {
        var cell = Cell(current: 99f);

        StaminaService.Step(cell, Idle(now: 10f, dt: 5f), E);

        Assert.AreEqual(100f, cell.Current);
    }

    [TestMethod]
    public void Step_InsideThePauseAfterAnOwnAction_DoesNotRegenerate_ThenDoes()
    {
        var cell = Cell(current: 50f);
        StaminaService.Charge(cell, 10f, now: 10f, E);

        StaminaService.Step(cell, Idle(now: 11f, dt: 1f), E);
        Assert.AreEqual(40f, cell.Current, Delta, "still paused");

        StaminaService.Step(cell, Idle(now: 11.5f, dt: 0.5f), E);
        Assert.AreEqual(40f + 0.05f * 100f * 0.5f, cell.Current, Delta, "the pause is over at 11.5");
    }

    [TestMethod]
    public void Step_AStoppedBlow_CostsButDoesNotPauseRegeneration()
    {
        // The rule the whole economy rests on: with a pause after every block, the opponent decides the pauses and
        // every soldier in contact reaches zero whatever he does.
        var cell = Cell(current: 50f);
        cell.AddPending(6f);

        StaminaService.Step(cell, Idle(now: 20f, dt: 1f), E);

        Assert.AreEqual(50f - 6f + 5f, cell.Current, Delta);
        Assert.AreEqual(0f, cell.RegenPausedUntil);
        Assert.IsTrue(cell.EverFought);
    }

    [TestMethod]
    public void Step_HoldingABlock_RegeneratesAtHalfRate()
    {
        var cell = Cell(current: 50f);

        StaminaService.Step(cell, new StaminaSample(10f, 2f, running: false, blocking: true, bowHeldSeconds: 0f), E);

        Assert.AreEqual(50f + 0.05f * 100f * 2f * 0.5f, cell.Current, Delta);
    }

    [TestMethod]
    public void Step_Running_StopsRegenerationAndDrains()
    {
        var cell = Cell(current: 100f);

        StaminaService.Step(cell, new StaminaSample(10f, 5f, running: true, blocking: false, bowHeldSeconds: 0f), E);

        Assert.AreEqual(100f - 0.02f * 100f * 5f, cell.Current, Delta);
    }

    [TestMethod]
    public void Step_RunningForALongTime_StopsAtTheFloor()
    {
        var cell = Cell(current: 100f);

        for (var second = 1; second <= 120; second++)
            StaminaService.Step(cell, new StaminaSample(second, 1f, running: true, blocking: false, bowHeldSeconds: 0f), E);

        Assert.AreEqual(50f, cell.Current, Delta, "running alone never takes a soldier below half");
    }

    [TestMethod]
    public void Step_RunningBelowTheFloor_NeitherDrainsNorRegenerates()
    {
        var cell = Cell(current: 30f);

        StaminaService.Step(cell, new StaminaSample(10f, 4f, running: true, blocking: false, bowHeldSeconds: 0f), E);

        Assert.AreEqual(30f, cell.Current, Delta);
    }

    [TestMethod]
    public void Step_ExhaustedSoldierWhoMovesFast_StillRegenerates_AndFaster()
    {
        // An exhausted soldier ordered to charge must not read as running, or he never recovers.
        var cell = Cell(current: 0f);
        cell.Publish(StaminaBand.Exhausted, 0f);

        StaminaService.Step(cell, new StaminaSample(10f, 2f, running: true, blocking: false, bowHeldSeconds: 0f), E);

        Assert.AreEqual(0.05f * 100f * 2f * E.ExhaustedRegen, cell.Current, Delta);
    }

    [TestMethod]
    public void Step_ExhaustedSoldier_PaysNothingForBlowsThatLandOnHisGuard()
    {
        var cell = Cell(current: 5f);
        cell.Publish(StaminaBand.Exhausted, 0.05f);
        cell.AddPending(6f);

        StaminaService.Step(cell, Idle(now: 10f, dt: 0f), E);

        Assert.AreEqual(5f, cell.Current, Delta);
        Assert.AreEqual(0f, cell.TakePending(), "the cost is dropped, not kept for later");
    }

    [TestMethod]
    public void Step_PlayerHoldsADrawnBow_CostsPerSecondAndPausesRegeneration()
    {
        var cell = Cell(current: 80f);

        StaminaService.Step(cell, new StaminaSample(10f, 1f, running: false, blocking: false, bowHeldSeconds: 1f), E);

        Assert.AreEqual(80f - E.BowHoldPerSecond, cell.Current, Delta);
        Assert.AreEqual(10f + E.RegenPauseSeconds, cell.RegenPausedUntil, Delta);
    }

    [DataTestMethod]
    [DataRow(0f, DisplayName = "a paused mission")]
    [DataRow(float.NaN)]
    [DataRow(-1f)]
    public void Step_IntervalNotAPositiveNumber_MovesNothing(float dt)
    {
        var cell = Cell(current: 40f);

        StaminaService.Step(cell, Idle(now: 10f, dt), E);

        Assert.AreEqual(40f, cell.Current);
    }

    [TestMethod]
    public void Step_StaminaNotANumber_EndsAtZero()
    {
        var cell = Cell();
        cell.Current = float.NaN;

        StaminaService.Step(cell, Idle(now: 10f, dt: 0f), E);

        Assert.AreEqual(0f, cell.Current);
    }

    [TestMethod]
    public void Step_UsesTheSoldiersRegenerationFactor()
    {
        var cell = Cell(current: 50f);
        cell.RegenFactor = 1.4f;

        StaminaService.Step(cell, Idle(now: 10f, dt: 1f), E);

        Assert.AreEqual(50f + 5f * 1.4f, cell.Current, Delta);
    }

    [DataTestMethod]
    [DataRow(StaminaBand.Fresh, 0.61f, StaminaBand.Fresh)]
    [DataRow(StaminaBand.Fresh, 0.59f, StaminaBand.Winded)]
    [DataRow(StaminaBand.Fresh, 0.29f, StaminaBand.Tired, DisplayName = "a big cost can skip Winded")]
    [DataRow(StaminaBand.Winded, 0.61f, StaminaBand.Winded, DisplayName = "no flapping at the Winded edge")]
    [DataRow(StaminaBand.Winded, 0.64f, StaminaBand.Winded)]
    [DataRow(StaminaBand.Winded, 0.66f, StaminaBand.Fresh, DisplayName = "past the Winded edge plus the hysteresis (0.65)")]
    [DataRow(StaminaBand.Winded, 0.29f, StaminaBand.Tired)]
    [DataRow(StaminaBand.Tired, 0.31f, StaminaBand.Tired, DisplayName = "no flapping at the Tired edge")]
    [DataRow(StaminaBand.Tired, 0.34f, StaminaBand.Tired)]
    [DataRow(StaminaBand.Tired, 0.36f, StaminaBand.Winded, DisplayName = "past the Tired edge plus the hysteresis (0.35)")]
    [DataRow(StaminaBand.Tired, 0.7f, StaminaBand.Fresh)]
    [DataRow(StaminaBand.Tired, 0.01f, StaminaBand.Tired, DisplayName = "one point left is not Exhausted")]
    [DataRow(StaminaBand.Tired, 0f, StaminaBand.Exhausted)]
    [DataRow(StaminaBand.Fresh, 0f, StaminaBand.Exhausted)]
    [DataRow(StaminaBand.Winded, float.NaN, StaminaBand.Exhausted, DisplayName = "a share that is not a number fails closed")]
    [DataRow(StaminaBand.Exhausted, 0.29f, StaminaBand.Exhausted, DisplayName = "the lock holds until the recovery level")]
    [DataRow(StaminaBand.Exhausted, 0.30f, StaminaBand.Tired, DisplayName = "recovered: into Tired, still cautious")]
    [DataRow(StaminaBand.Exhausted, 0.9f, StaminaBand.Tired)]
    [DataRow(StaminaBand.Exhausted, float.NaN, StaminaBand.Exhausted)]
    public void NextBand_FromEachBand(StaminaBand current, float fraction, StaminaBand expected)
    {
        Assert.AreEqual(expected, StaminaService.NextBand(E, current, fraction));
    }

    [TestMethod]
    public void Skip_DropsTheStoppedBlowsOfASoldierTheRulesDoNotStep()
    {
        // A forced band, an MCM opt-out, another peer's soldier, a soldier at a siege engine. Without the drop, every
        // blow he stopped meanwhile would land together at the look that steps him again.
        var cell = Cell(current: 100f);
        cell.AddPending(60f);

        StaminaService.Skip(cell);
        StaminaService.Step(cell, Idle(now: 20f, dt: 0f), E);

        Assert.AreEqual(100f, cell.Current, Delta);
        Assert.IsFalse(cell.EverFought);
    }

    [TestMethod]
    public void FractionOf_NoPoolYet_ReadsFull()
    {
        Assert.AreEqual(1f, StaminaService.FractionOf(new StaminaCell(0f)));
        Assert.AreEqual(0.25f, StaminaService.FractionOf(Cell(max: 200f, current: 50f)), Delta);
    }

    [TestMethod]
    public void AddPending_FromEightThreads_SumsExactly()
    {
        // Stopped blows are reported from engine callbacks on any thread while the tick takes the sum.
        var cell = new StaminaCell(0f);

        Parallel.For(0, 8, _ =>
        {
            for (var i = 0; i < 10_000; i++)
                cell.AddPending(0.25f);
        });

        Assert.AreEqual(8 * 10_000 * 0.25f, cell.TakePending(), 0.01f);
        Assert.AreEqual(0f, cell.TakePending());
    }

    [DataTestMethod]
    [DataRow(0f)]
    [DataRow(-3f)]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    public void AddPending_CostNotAPositiveFiniteNumber_AddsNothing(float cost)
    {
        var cell = new StaminaCell(0f);

        cell.AddPending(cost);

        Assert.AreEqual(0f, cell.TakePending());
    }
}
