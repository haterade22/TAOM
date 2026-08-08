using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Enlistment;
using TAOM.Features.Enlistment.Presentation;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// First coverage of <see cref="ServiceVocabulary"/>. Nothing under TAOM.Tests referenced it
/// before the shown-and-disabled conversion, so every player-facing string it produces was
/// unpinned — including the ones the reassignment and enlist lines now depend on to explain why
/// they are greyed.
///
/// Assertions read <c>TextObject.Value</c> and <c>TextObject.Attributes</c>, never
/// <c>ToString()</c>: rendering a TextObject goes through MBTextManager and needs an initialised
/// Module, which a unit test does not have. That is not a workaround — Attributes IS the thing
/// under test here. The day count must be bound to the TextObject instance, because a conversation
/// hint is rendered late (HintViewModel.ExecuteBeginHint calls ToString() on hover), long after a
/// process-global text variable would have been overwritten by another sentence's condition.
/// </summary>
[TestClass]
public class ServiceVocabularyTests
{
    [TestMethod]
    public void EnlistUnavailableReason_AtWarWithYourKingdom_NamesTheWarSpecifically()
    {
        var hint = ServiceVocabulary.EnlistUnavailableReason(EnlistGateResult.AtWarWithYourKingdom);

        StringAssert.Contains(hint.Value, "{=taom_enlist_no_at_war}");
    }

    /// <summary>
    /// These verdicts HIDE the offer, so the default arm is unreachable while the visibility split
    /// holds. Pinned anyway: the failure mode of an unreachable switch arm is that someone adds a
    /// verdict, routes it to the greyed branch, and ships a hint that reads as a crash.
    /// </summary>
    [DataTestMethod]
    [DataRow(EnlistGateResult.AlreadyEnlisted)]
    [DataRow(EnlistGateResult.NotALord)]
    [DataRow(EnlistGateResult.UnderMercenaryContract)]
    [DataRow(EnlistGateResult.CommanderUnavailable)]
    [DataRow(EnlistGateResult.FeatureDisabled)]
    [DataRow(EnlistGateResult.Ok)]
    public void EnlistUnavailableReason_AnyOtherVerdict_FallsBackToGenericWithoutThrowing(EnlistGateResult verdict)
    {
        var hint = ServiceVocabulary.EnlistUnavailableReason(verdict);

        StringAssert.Contains(hint.Value, "{=taom_enlist_no_generic}");
    }

    [TestMethod]
    public void ReassignCooldownReason_SeveralDaysLeft_BindsTheCeilingToTheTextObject()
    {
        var hint = ServiceVocabulary.ReassignCooldownReason(3.2);

        StringAssert.Contains(hint.Value, "{=taom_enlist_reassign_no_cooldown}");
        StringAssert.Contains(hint.Value, "{DAYS}");
        Assert.IsNotNull(hint.Attributes, "The day count must ride on the TextObject, not on a process-global text variable.");
        Assert.AreEqual(4, hint.Attributes["DAYS"]);
    }

    [TestMethod]
    public void ReassignCooldownReason_PartOfADayLeft_UsesTheSingularWording()
    {
        var hint = ServiceVocabulary.ReassignCooldownReason(0.25);

        StringAssert.Contains(hint.Value, "{=taom_enlist_reassign_no_cooldown_one}");
    }

    [TestMethod]
    public void ReassignCooldownReason_ExactlyOneDayLeft_UsesTheSingularWording()
    {
        var hint = ServiceVocabulary.ReassignCooldownReason(1.0);

        StringAssert.Contains(hint.Value, "{=taom_enlist_reassign_no_cooldown_one}");
    }

    /// <summary>
    /// NaN and the infinities reach this method whenever the clock or the cooldown config is
    /// poisoned — AssignmentService.CooldownRemaining propagates a NaN "now" straight through.
    /// The hint must degrade to a wordless generic rather than print a nonsense day count.
    /// </summary>
    [DataTestMethod]
    [DataRow(double.NaN)]
    [DataRow(double.PositiveInfinity)]
    [DataRow(double.NegativeInfinity)]
    public void ReassignCooldownReason_NonFiniteRemainder_UsesGenericReasonAndBindsNoNumber(double remaining)
    {
        var hint = ServiceVocabulary.ReassignCooldownReason(remaining);

        StringAssert.Contains(hint.Value, "{=taom_enlist_reassign_no_generic}");
        Assert.IsNull(hint.Attributes, "A non-finite remainder must not bind a day count.");
    }

    [DataTestMethod]
    [DataRow(0.0)]
    [DataRow(-5.0)]
    public void ReassignCooldownReason_NothingLeftToWait_UsesGenericReason(double remaining)
    {
        var hint = ServiceVocabulary.ReassignCooldownReason(remaining);

        StringAssert.Contains(hint.Value, "{=taom_enlist_reassign_no_generic}");
    }

    /// <summary>
    /// A huge finite cooldown must clamp, not overflow. `(int)Math.Ceiling(1e30)` is int.MinValue
    /// on net472/x64, which would render the hint as a NEGATIVE wait — the float-to-int trap the
    /// architecture rules name separately from NaN.
    /// </summary>
    [TestMethod]
    public void ReassignCooldownReason_HugeFiniteRemainder_ClampsInsteadOfOverflowingNegative()
    {
        var hint = ServiceVocabulary.ReassignCooldownReason(1e30);

        Assert.IsNotNull(hint.Attributes);
        Assert.AreEqual(int.MaxValue, hint.Attributes["DAYS"]);
    }
}
