using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CrashReport.BugReport;

namespace TAOM.Tests.Features.CrashReport;

/// <summary>
/// The next-start bug report offer is once per launch. The main-menu hook runs on every return to the main menu
/// (Module.SetInitialModuleScreenAsRootScreen), so a launch that showed the battle-load stall notice must use its turn
/// there, or the next return to the menu offers a report about the session the player was already told about.
/// </summary>
[TestClass]
[DoNotParallelize]
public class BugReportStartupTurnTests
{
    [TestInitialize]
    public void Reset() => BugReportStartup.ResetTurnForTests();

    [TestCleanup]
    public void Cleanup() => BugReportStartup.ResetTurnForTests();

    [TestMethod]
    public void FirstMenu_NoStallNotice_ChecksOnce()
    {
        Assert.IsTrue(BugReportStartup.TakeTurn(stallNoticeShown: false));
        Assert.IsFalse(BugReportStartup.TakeTurn(stallNoticeShown: false));
    }

    [TestMethod]
    public void StallNoticeShown_UsesTheTurn_NoOfferLaterInTheLaunch()
    {
        Assert.IsFalse(BugReportStartup.TakeTurn(stallNoticeShown: true));
        Assert.IsFalse(BugReportStartup.TakeTurn(stallNoticeShown: false));
    }
}
