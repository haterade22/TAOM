using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CaravanTrade.Diagnostics;

namespace TAOM.Tests.Features.CaravanTrade;

/// <summary>
/// Pins the caravan-parking verdict against the engine's own gate order.
///
/// Why this exists: a caravan parked in a town is the single most-reported caravan symptom, and
/// vanilla offers no signal at all for it — every one of the seven ways a caravan gets stuck is a
/// silent early-return. The gates are ranked here in the SAME order the engine evaluates them
/// (<c>CaravansCampaignBehavior.HourlyTickParty</c> :617-683, plus
/// <c>MobileParty.CheckExitingSettlementParallel</c> :4085-4104 for the physical exit), because a
/// report that named the second-blocking gate would send the reader after the wrong bug.
///
/// The wounded ladder is reproduced verbatim from :633-659, including the <c>(double)</c> casts.
/// Every threshold there is written as a strict <c>&gt;</c> against a <c>double</c> literal, which
/// reads as an exclusive boundary — but <c>num</c> is a <c>float</c>, and widening it puts the
/// nearest float to each threshold just ABOVE the literal (0.4f widens to 0.40000000596…, which
/// beats the double 0.4). So every boundary behaves as INCLUSIVE in practice, and a caravan at
/// exactly 40% wounded scores zero and never leaves. The boundary rows below pin that.
/// </summary>
[TestClass]
public class CaravanGateDiagnosticsServiceTests
{
    private CaravanGateDiagnosticsService _sut;

    [TestInitialize]
    public void Setup() => _sut = new CaravanGateDiagnosticsService();

    /// <summary>A healthy caravan parked in a peaceful town: nothing blocks it, it leaves in ~3 hours.</summary>
    private static CaravanGateSnapshot Healthy() => new CaravanGateSnapshot
    {
        CaravanId = "caravan_1",
        CaravanName = "Aldamir's Caravan",
        CurrentSettlementId = "town_G3",
        TargetSettlementId = "town_G3",
        IsInFortification = true,
        IsPartyTradeActive = true,
        TotalManCount = 20,
        TotalWounded = 0,
        PartyTradeGold = 9000,
        CargoItemCount = 12,
    };

    [TestMethod]
    public void Evaluate_HealthyCaravanInTown_ReportsNotBlocked()
    {
        var verdict = _sut.Evaluate(Healthy());

        Assert.AreEqual(CaravanGate.NotBlocked, verdict.Gate);
        Assert.AreEqual(1f, verdict.LeaveChancePerThink, 0.0001f);
    }

    // ---- Precedence: the engine checks these before anything else ----

    /// <summary>
    /// `Ai.IsDisabled` is the first clause of `CheckExitingSettlementParallel` — the party can never
    /// physically leave regardless of what the caravan behavior decides, so it outranks every gate.
    /// </summary>
    [TestMethod]
    public void Evaluate_AiDisabled_OutranksEveryOtherGate()
    {
        var snapshot = Healthy();
        snapshot.IsAiDisabled = true;
        snapshot.IsAlerted = true;
        snapshot.TotalWounded = 20;

        Assert.AreEqual(CaravanGate.AiDisabled, _sut.Evaluate(snapshot).Gate);
    }

    /// <summary>
    /// `ShortTermBehavior == Hold` is the SECOND clause of the same exit guard as `Ai.IsDisabled`.
    /// `CaravansCampaignBehavior.OnSiegeEventStarted` (:317-326) applies `SetMoveModeHold()` to every
    /// caravan inside a settlement that comes under siege, and nothing clears it when the siege
    /// lifts — only a fresh movement order does. Before this gate existed the census reported such a
    /// caravan as `NotBlocked`, which is the worst possible answer from a tool whose only job is
    /// naming the blocker.
    /// </summary>
    [TestMethod]
    public void Evaluate_HeldInPlace_ReportsHeldInPlace()
    {
        var snapshot = Healthy();
        snapshot.IsOnHold = true;

        Assert.AreEqual(CaravanGate.HeldInPlace, _sut.Evaluate(snapshot).Gate);
    }

    /// <summary>
    /// Hold survives the siege that set it, so it must still be reported once `IsUnderSiege` is
    /// false — otherwise the caravan silently reads as free the moment the siege lifts.
    /// </summary>
    [TestMethod]
    public void Evaluate_HeldInPlaceAfterTheSiegeLifted_StillReportsHeldInPlace()
    {
        var snapshot = Healthy();
        snapshot.IsOnHold = true;
        snapshot.IsCurrentSettlementUnderSiege = false;

        Assert.AreEqual(CaravanGate.HeldInPlace, _sut.Evaluate(snapshot).Gate);
    }

    /// <summary>Both exit-guard clauses at once — `AiDisabled` is the stronger statement.</summary>
    [TestMethod]
    public void Evaluate_AiDisabledAndHeld_PrefersAiDisabled()
    {
        var snapshot = Healthy();
        snapshot.IsAiDisabled = true;
        snapshot.IsOnHold = true;

        Assert.AreEqual(CaravanGate.AiDisabled, _sut.Evaluate(snapshot).Gate);
    }

    [TestMethod]
    public void Evaluate_InMapEvent_ReportsInMapEvent()
    {
        var snapshot = Healthy();
        snapshot.HasMapEvent = true;

        Assert.AreEqual(CaravanGate.InMapEvent, _sut.Evaluate(snapshot).Gate);
    }

    /// <summary>
    /// `IsPartyTradeActive` is only ever set by `InitializePartyTrade`, and
    /// `CaravanPartyComponent.ConvertPartyToCaravanParty` does not call it — a converted caravan can
    /// be permanently inert. Worth naming explicitly rather than folding into a generic "inactive".
    /// </summary>
    [TestMethod]
    public void Evaluate_PartyTradeInactive_ReportsTradeInactive()
    {
        var snapshot = Healthy();
        snapshot.IsPartyTradeActive = false;

        Assert.AreEqual(CaravanGate.TradeInactive, _sut.Evaluate(snapshot).Gate);
    }

    /// <summary>
    /// Set by five quest behaviors. A quest that fails to clear it freezes the caravan for the rest
    /// of the campaign, which is a quest bug — the report must say so rather than blame the economy.
    /// </summary>
    [TestMethod]
    public void Evaluate_DecisionsSuppressed_ReportsDecisionsSuppressed()
    {
        var snapshot = Healthy();
        snapshot.DoNotMakeNewDecisions = true;

        Assert.AreEqual(CaravanGate.DecisionsSuppressed, _sut.Evaluate(snapshot).Gate);
    }

    // ---- The in-fortification gates (HourlyTickParty:631) ----

    [TestMethod]
    public void Evaluate_CurrentSettlementUnderSiege_ReportsHoldingForSiege()
    {
        var snapshot = Healthy();
        snapshot.IsCurrentSettlementUnderSiege = true;

        Assert.AreEqual(CaravanGate.HoldingForSiege, _sut.Evaluate(snapshot).Gate);
    }

    /// <summary>
    /// A naval caravan escapes the siege gate when the blockade is not active — the one asymmetry in
    /// :631. TAOM parks naval travel (#296), but the gate is engine behavior and the report must not
    /// mislabel a naval caravan that is genuinely free to go.
    /// </summary>
    [TestMethod]
    public void Evaluate_NavalCaravanUnderSiegeWithoutBlockade_IsNotHeldBySiege()
    {
        var snapshot = Healthy();
        snapshot.IsCurrentSettlementUnderSiege = true;
        snapshot.IsBlockadeActive = false;
        snapshot.HasNavalNavigationCapability = true;

        // Asserting the exact verdict, not merely "not siege" — the weaker form would pass even if
        // the gate returned something nonsensical.
        Assert.AreEqual(CaravanGate.NotBlocked, _sut.Evaluate(snapshot).Gate);
    }

    [TestMethod]
    public void Evaluate_Fleeing_ReportsFleeing()
    {
        var snapshot = Healthy();
        snapshot.IsFleeing = true;

        Assert.AreEqual(CaravanGate.Fleeing, _sut.Evaluate(snapshot).Gate);
    }

    /// <summary>
    /// `IsAlerted` is set only in the flee branch of `MobilePartyAi.GetBehaviors` and recomputed each
    /// think, so a caravan sheltering from a nearby battle stays alerted indefinitely. This is the
    /// gate that fits "battle next door, caravans sitting in the city".
    /// </summary>
    [TestMethod]
    public void Evaluate_Alerted_ReportsAlerted()
    {
        var snapshot = Healthy();
        snapshot.IsAlerted = true;

        Assert.AreEqual(CaravanGate.Alerted, _sut.Evaluate(snapshot).Gate);
    }

    // ---- The wounded ladder (HourlyTickParty:633-659) ----

    [TestMethod]
    public void Evaluate_WoundedAbove40Percent_ReportsCannotLeave()
    {
        var snapshot = Healthy();
        snapshot.TotalManCount = 20;
        snapshot.TotalWounded = 9; // 45%

        var verdict = _sut.Evaluate(snapshot);

        Assert.AreEqual(CaravanGate.WoundedCannotLeave, verdict.Gate);
        Assert.AreEqual(0f, verdict.LeaveChancePerThink, 0.0001f);
    }

    /// <summary>
    /// The engine writes the threshold as a strict `(double)num &gt; 0.4`, which reads as "40% exactly
    /// is still fine". It is not. `num` is a <c>float</c> and the comparison widens it to
    /// <c>double</c>: the nearest float to 0.4 is 0.40000000596…, which IS greater than the double
    /// 0.40000000000000002. So an exact 8/20 lands in the `num2 = 0f` band and the caravan never
    /// leaves. The same widening pushes every other exact boundary up into the harsher band too
    /// (see the ladder test below) — verified against IEEE-754 single→double semantics, not assumed.
    ///
    /// This matters for the report: a caravan sitting at exactly 40% wounded is permanently stuck,
    /// and describing the threshold as "over 40%" would send the reader looking for another cause.
    /// </summary>
    [TestMethod]
    public void Evaluate_WoundedExactly40Percent_WidensAboveThresholdAndCannotLeave()
    {
        var snapshot = Healthy();
        snapshot.TotalManCount = 20;
        snapshot.TotalWounded = 8; // exactly 40% — widens to 0.40000000596 > 0.4

        var verdict = _sut.Evaluate(snapshot);

        Assert.AreEqual(CaravanGate.WoundedCannotLeave, verdict.Gate);
        Assert.AreEqual(0f, verdict.LeaveChancePerThink, 0.0001f);
    }

    /// <summary>
    /// The band between "barely scratched" and "cannot leave" — a caravan that WILL leave, slowly.
    /// It had no coverage at all until the review caught it, despite being the most common wounded
    /// state in practice and the one whose verdict text quotes a recovery rate at the reader.
    /// </summary>
    [TestMethod]
    public void Evaluate_MildlyWounded_ReportsWoundedUnlikelyToLeave()
    {
        var snapshot = Healthy();
        snapshot.TotalManCount = 1000;
        snapshot.TotalWounded = 300; // 30% -> the 0.1 band

        var verdict = _sut.Evaluate(snapshot);

        Assert.AreEqual(CaravanGate.WoundedUnlikelyToLeave, verdict.Gate);
        Assert.AreEqual(0.1f, verdict.LeaveChancePerThink, 0.0001f);
        Assert.AreEqual(0.1f / 3f, verdict.EffectiveHourlyLeaveChance, 0.0001f);
    }

    /// <summary>
    /// A caravan that is both slow AND trapped: the wound heals on its own, the zero trade score
    /// never does. Reporting the temporary condition would send the reader away to wait it out.
    /// </summary>
    [TestMethod]
    public void Evaluate_WoundedAndTrapped_ReportsTheTrapNotTheWound()
    {
        var snapshot = Healthy();
        snapshot.TotalManCount = 1000;
        snapshot.TotalWounded = 300;
        snapshot.PartyTradeGold = 0;
        snapshot.CargoItemCount = 0;

        Assert.AreEqual(CaravanGate.NoViableDestination, _sut.Evaluate(snapshot).Gate);
    }

    /// <summary>
    /// The naval siege exemption is conditional on the blockade being inactive. With it active the
    /// caravan is held like any other — the earlier test only asserted "not siege", which would pass
    /// even if the gate returned something nonsensical.
    /// </summary>
    [TestMethod]
    public void Evaluate_NavalCaravanUnderActiveBlockade_IsStillHeldBySiege()
    {
        var snapshot = Healthy();
        snapshot.IsCurrentSettlementUnderSiege = true;
        snapshot.IsBlockadeActive = true;
        snapshot.HasNavalNavigationCapability = true;

        Assert.AreEqual(CaravanGate.HoldingForSiege, _sut.Evaluate(snapshot).Gate);
    }

    /// <summary>
    /// Interior values pin the bands; the exact-boundary rows pin the float-widening behavior
    /// described above. Both are needed — the interior rows would still pass against a naive
    /// implementation that compared in <c>float</c> and got every boundary wrong.
    /// </summary>
    [DataTestMethod]
    // Interior values — comfortably inside each band.
    [DataRow(20, 0, 1.0f)]     // 0%
    [DataRow(1000, 20, 1.0f)]  // 2.0%
    [DataRow(1000, 30, 0.4f)]  // 3.0%
    [DataRow(1000, 60, 0.3f)]  // 6.0%
    [DataRow(1000, 150, 0.2f)] // 15%
    [DataRow(1000, 300, 0.1f)] // 30%
    [DataRow(1000, 500, 0f)]   // 50%
    // Exact boundaries — each widens UP into the harsher band.
    [DataRow(1000, 25, 0.4f)]  // 2.5%  -> 0.02500000037 > 0.025
    [DataRow(20, 1, 0.3f)]     // 5%    -> 0.05000000075 > 0.05
    [DataRow(20, 2, 0.2f)]     // 10%   -> 0.10000000149 > 0.1
    [DataRow(20, 4, 0.1f)]     // 20%   -> 0.20000000298 > 0.2
    [DataRow(20, 8, 0f)]       // 40%   -> 0.40000000596 > 0.4
    public void Evaluate_WoundedFraction_MatchesEngineLadder(int manCount, int wounded, float expectedChance)
    {
        var snapshot = Healthy();
        snapshot.TotalManCount = manCount;
        snapshot.TotalWounded = wounded;

        Assert.AreEqual(expectedChance, _sut.Evaluate(snapshot).LeaveChancePerThink, 0.0001f);
    }

    /// <summary>
    /// The engine computes `TotalManCount &gt; 0 ? wounded/total : 1f` — a caravan with no men reads
    /// as 100% wounded and never leaves. Division-by-zero here would be a NaN flowing into a
    /// comparison ladder where every test returns false, silently landing on the 1.0 band and
    /// reporting a stuck caravan as healthy.
    /// </summary>
    [TestMethod]
    public void Evaluate_ZeroManCount_TreatedAsFullyWoundedAndCannotLeave()
    {
        var snapshot = Healthy();
        snapshot.TotalManCount = 0;
        snapshot.TotalWounded = 0;

        var verdict = _sut.Evaluate(snapshot);

        Assert.AreEqual(CaravanGate.WoundedCannotLeave, verdict.Gate);
        Assert.AreEqual(0f, verdict.LeaveChancePerThink, 0.0001f);
    }

    // ---- The zero-trade-score trap (FindNextDestinationForCaravan:928 + GetTradeScoreForTown:1022) ----

    /// <summary>
    /// The buy score is capped at `(int)(0.5 * PartyTradeGold)`, so a caravan under 2 gold scores 0
    /// on that half for EVERY town in the world. With no cargo the sell half is 0 too, every town
    /// scores exactly 0, the `&gt; 0f` test never passes, and `ThinkNextDestination` returns null —
    /// which sets no AI action at all, leaving the caravan parked permanently.
    /// </summary>
    [TestMethod]
    public void Evaluate_BrokeAndEmpty_ReportsNoViableDestination()
    {
        var snapshot = Healthy();
        snapshot.PartyTradeGold = 0;
        snapshot.CargoItemCount = 0;

        Assert.AreEqual(CaravanGate.NoViableDestination, _sut.Evaluate(snapshot).Gate);
    }

    [TestMethod]
    public void Evaluate_BrokeAtOneGold_StillReportsNoViableDestination()
    {
        // (int)(0.5f * 1) == 0 — one denar buys nothing, so the buy score is still zero everywhere.
        var snapshot = Healthy();
        snapshot.PartyTradeGold = 1;
        snapshot.CargoItemCount = 0;

        Assert.AreEqual(CaravanGate.NoViableDestination, _sut.Evaluate(snapshot).Gate);
    }

    /// <summary>
    /// Cargo keeps the sell half of the score alive, so a broke caravan carrying goods can still
    /// find a town — it is not in the trap. Flagging it would send the reader after a non-bug.
    /// </summary>
    [TestMethod]
    public void Evaluate_BrokeButCarryingCargo_IsNotFlaggedAsTrapped()
    {
        var snapshot = Healthy();
        snapshot.PartyTradeGold = 0;
        snapshot.CargoItemCount = 8;

        Assert.AreEqual(CaravanGate.NotBlocked, _sut.Evaluate(snapshot).Gate);
    }

    [TestMethod]
    public void Evaluate_FundedButEmpty_IsNotFlaggedAsTrapped()
    {
        var snapshot = Healthy();
        snapshot.PartyTradeGold = 9000;
        snapshot.CargoItemCount = 0;

        Assert.AreEqual(CaravanGate.NotBlocked, _sut.Evaluate(snapshot).Gate);
    }

    // ---- Scope ----

    /// <summary>
    /// The wounded/alert/siege ladder lives inside the `CurrentSettlement.IsFortification` branch. A
    /// caravan on the open map re-decides through a different path entirely, so applying those gates
    /// to it would invent a blocker that does not exist.
    /// </summary>
    [TestMethod]
    public void Evaluate_NotInFortification_IgnoresTheFortificationOnlyGates()
    {
        var snapshot = Healthy();
        snapshot.IsInFortification = false;
        snapshot.TotalWounded = 20;
        snapshot.IsAlerted = true;

        Assert.AreEqual(CaravanGate.NotBlocked, _sut.Evaluate(snapshot).Gate);
    }

    /// <summary>
    /// The early-return gates at :625 apply everywhere, fortification or not.
    /// </summary>
    [TestMethod]
    public void Evaluate_NotInFortificationButDecisionsSuppressed_StillReportsTheGate()
    {
        var snapshot = Healthy();
        snapshot.IsInFortification = false;
        snapshot.DoNotMakeNewDecisions = true;

        Assert.AreEqual(CaravanGate.DecisionsSuppressed, _sut.Evaluate(snapshot).Gate);
    }

    // ---- Reporting ----

    [TestMethod]
    public void Evaluate_AnyGate_ExplanationNamesTheEngineSite()
    {
        var snapshot = Healthy();
        snapshot.TotalWounded = 20;

        var verdict = _sut.Evaluate(snapshot);

        Assert.IsFalse(string.IsNullOrWhiteSpace(verdict.Explanation));
        StringAssert.Contains(verdict.Explanation, "wounded");
    }

    /// <summary>
    /// The non-quest caravan rolls `randomFloat &lt; 1/3` BEFORE the wounded roll, so the effective
    /// hourly departure chance is a third of the band value. Reporting the band alone would overstate
    /// how fast a slow caravan recovers by 3x.
    /// </summary>
    [TestMethod]
    public void Evaluate_HealthyCaravan_EffectiveHourlyChanceIsOneThirdOfTheBand()
    {
        var verdict = _sut.Evaluate(Healthy());

        Assert.AreEqual(1f / 3f, verdict.EffectiveHourlyLeaveChance, 0.0001f);
    }

    /// <summary>A quest caravan skips the 1-in-3 roll entirely (`IsCurrentlyUsedByAQuest ||`).</summary>
    [TestMethod]
    public void Evaluate_QuestCaravan_SkipsTheOneInThreeRoll()
    {
        var snapshot = Healthy();
        snapshot.IsCurrentlyUsedByAQuest = true;

        Assert.AreEqual(1f, _sut.Evaluate(snapshot).EffectiveHourlyLeaveChance, 0.0001f);
    }
}
