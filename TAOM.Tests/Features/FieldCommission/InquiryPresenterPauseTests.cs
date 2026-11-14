using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Features.FieldCommission;

/// <summary>
/// Every Battlefield Promotion popup that waits for the player must hold the campaign clock.
/// v1.5.7 GauntletQueryManager.HandleQueryCreated (:244-259) registers an active-state disable
/// request and pauses the engine only when the inquiry was raised with
/// pauseGameActiveState: true, and CloseQuery (:261-273) releases both, so the map resumes at the
/// speed it had. The rename prompt was raised with false, so after "Promote" the party kept
/// moving on the map while the player typed a name (player report, 14 Nov 2026).
/// Source-text pins on the adapter: the engine call cannot run on the test stubs.
/// </summary>
[TestClass]
public class InquiryPresenterPauseTests
{
    private const string AdapterPath = "Main/Adapters/InquiryPresenterAdapter.cs";

    // The adapter raises five inquiries today (offer, no-room notice, rename prompt, discharge picker, discharge
    // confirm). The floor makes an empty scan (a renamed call, a moved file) fail instead of pass.
    private const int InquiriesToday = 5;

    [TestMethod]
    public void EveryInquiryCall_PausesTheCampaign()
    {
        var src = ReadSource(AdapterPath, stripComments: true);

        int inquiries = Regex.Matches(src, @"\bShow(Text|MultiSelection)?Inquiry\(").Count;
        int paused = Regex.Matches(src, @"pauseGameActiveState:\s*true").Count;

        Assert.IsTrue(inquiries >= InquiriesToday,
            $"found {inquiries} inquiry calls in {AdapterPath}, expected at least {InquiriesToday}: "
            + "the scan is empty or a popup was removed; update the pin");
        Assert.AreEqual(inquiries, paused,
            $"{inquiries} inquiry calls but {paused} with pauseGameActiveState: true; an inquiry raised "
            + "with the default flag leaves the campaign map running while the popup waits for an answer");
        Assert.IsFalse(src.Contains("pauseGameActiveState: false"),
            "an inquiry is raised with pauseGameActiveState: false");
    }
}
