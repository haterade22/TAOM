using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.BattleLoadDiagnostics;

namespace TAOM.Tests.Features.BattleLoadDiagnostics;

/// <summary>
/// The binding rule: the window says "compiling" only while compile_shader lines are arriving in the
/// engine log. A public 1.4.8 report (#612) sat on a count that kept "falling" while nothing compiled.
/// </summary>
[TestClass]
public class RenderWaitNoticeDeciderTests
{
    private static void AssertDecision(RenderWaitNoticeDecision d, RenderWaitNoticeKind kind, int compiles = 0, int seconds = 0)
    {
        Assert.AreEqual(kind, d.Kind);
        if (kind == RenderWaitNoticeKind.Compiling) Assert.AreEqual(compiles, d.Compiles);
        if (kind == RenderWaitNoticeKind.NoProgress) Assert.AreEqual(seconds, d.Seconds);
    }

    [TestMethod]
    public void NotInRenderWait_IsHidden()
        => AssertDecision(RenderWaitNoticeDecider.Decide(false, false, 100, true, 40, 1), RenderWaitNoticeKind.Hidden);

    [TestMethod]
    public void WalkActive_IsHidden()
        => AssertDecision(RenderWaitNoticeDecider.Decide(true, true, 100, true, 40, 1), RenderWaitNoticeKind.Hidden);

    [TestMethod]
    public void SilentFourSecondsWithARecentCompile_IsHidden()
        => AssertDecision(RenderWaitNoticeDecider.Decide(true, false, 4, true, 40, 1), RenderWaitNoticeKind.Hidden);

    [TestMethod]
    public void EngineLogUnavailable_IsHidden()
        => AssertDecision(RenderWaitNoticeDecider.Decide(true, false, 120, false, 0, null), RenderWaitNoticeKind.Hidden);

    [TestMethod]
    public void SilentSixSecondsWithFreshCompiles_IsCompiling()
        => AssertDecision(RenderWaitNoticeDecider.Decide(true, false, 6, true, 40, 1), RenderWaitNoticeKind.Compiling, compiles: 40);

    [TestMethod]
    public void SilentThirtySecondsAndNoCompileYet_IsHidden()
        => AssertDecision(RenderWaitNoticeDecider.Decide(true, false, 30, true, 0, null), RenderWaitNoticeKind.Hidden);

    [TestMethod]
    public void SilentSixtyOneSecondsAndNoCompileAtAll_IsNoProgress()
        => AssertDecision(RenderWaitNoticeDecider.Decide(true, false, 61, true, 0, null), RenderWaitNoticeKind.NoProgress, seconds: 61);

    [TestMethod]
    public void SilentNinetySecondsLastCompileSeventyAgo_IsNoProgress()
        => AssertDecision(RenderWaitNoticeDecider.Decide(true, false, 90, true, 12, 70), RenderWaitNoticeKind.NoProgress, seconds: 70);

    [TestMethod]
    public void SilentNinetySecondsLastCompileThirtyAgo_IsHidden()
        => AssertDecision(RenderWaitNoticeDecider.Decide(true, false, 90, true, 12, 30), RenderWaitNoticeKind.Hidden);
}
