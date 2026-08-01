using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CultureConversion.Cheats;

namespace TAOM.Tests.Features.CultureConversion;

/// <summary>
/// Pins the `taom.requeue_settlement` report — a live regression guard for #333.
///
/// #333 was a timer restart: capturing a fief and then being granted it by the kingdom fires the
/// owner-changed path twice, and the second fire reset the conversion hold. The guard is
/// `if (record.HasPending &amp;&amp; record.PendingTargetCultureId == ownerCulture) return;`. This command
/// fires the path twice on demand and reports whether `PendingStartDays` moved, so the regression is
/// a one-line console check instead of a siege plus a day's wait.
///
/// **Reachability, stated so nobody mistakes these for observed outcomes:** with the guard working,
/// the live command can only produce UNCHANGED or "no conversion queued" — the two fires are
/// synchronous, so the owner culture is identical on both and whichever fire reaches `StartPending`
/// first makes the second one's guard true. The RESTARTED and second-fire-only branches exist to
/// catch a FUTURE regression in `CultureConversionService`; neither has been seen from the console,
/// and neither can be under the current implementation.
/// </summary>
[TestClass]
public class CultureConversionCheatsFormatTests
{
    [TestMethod]
    public void FormatRequeue_TimerHeld_ReportsUnchangedAndNamesTheIssue()
    {
        var report = CultureConversionCheats.FormatRequeue(
            settlementId: "town_G3", settlementName: "Minas Tirith",
            targetCultureId: "isengard", firstPending: 112.40, secondPending: 112.40);

        StringAssert.Contains(report, "UNCHANGED");
        StringAssert.Contains(report, "#333");
        Assert.IsFalse(report.Contains("RESTARTED"));
    }

    [TestMethod]
    public void FormatRequeue_TimerRestarted_ReportsRegressionLoudly()
    {
        var report = CultureConversionCheats.FormatRequeue(
            settlementId: "town_G3", settlementName: "Minas Tirith",
            targetCultureId: "isengard", firstPending: 112.40, secondPending: 112.41);

        StringAssert.Contains(report, "RESTARTED");
        StringAssert.Contains(report, "regressed");
    }

    /// <summary>
    /// No pending timer after the first fire is a legitimate outcome — the settlement's owner culture
    /// may already match, so no conversion is queued. That must not read as a passing regression test.
    /// </summary>
    [TestMethod]
    public void FormatRequeue_NoTimerQueued_SaysSoRatherThanClaimingTheGuardHeld()
    {
        var report = CultureConversionCheats.FormatRequeue(
            settlementId: "town_G3", settlementName: "Minas Tirith",
            targetCultureId: null, firstPending: null, secondPending: null);

        StringAssert.Contains(report, "no conversion was queued");
        Assert.IsFalse(report.Contains("UNCHANGED"));
    }

    [TestMethod]
    public void FormatRequeue_RendersSettlementIdAndName()
    {
        var report = CultureConversionCheats.FormatRequeue(
            settlementId: "town_G3", settlementName: "Minas Tirith",
            targetCultureId: "isengard", firstPending: 1.0, secondPending: 1.0);

        StringAssert.Contains(report, "town_G3");
        StringAssert.Contains(report, "Minas Tirith");
        StringAssert.Contains(report, "isengard");
    }

    /// <summary>
    /// A timer that appears only on the SECOND fire is neither "held" nor "restarted" — it means the
    /// first fire did not queue anything, which is its own signal worth distinguishing.
    /// </summary>
    [TestMethod]
    public void FormatRequeue_TimerAppearedOnlyOnSecondFire_IsDistinguishable()
    {
        var report = CultureConversionCheats.FormatRequeue(
            settlementId: "town_G3", settlementName: "Minas Tirith",
            targetCultureId: "isengard", firstPending: null, secondPending: 112.41);

        StringAssert.Contains(report, "only after the second");
    }
}
