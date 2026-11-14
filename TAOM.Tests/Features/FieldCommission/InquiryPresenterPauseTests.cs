using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Features.FieldCommission;

/// <summary>
/// Every Battlefield Promotion popup that waits for the player must hold the campaign clock.
/// v1.5.6 GauntletQueryManager.HandleQueryCreated (:244-259) registers an active-state disable
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

    [TestMethod]
    [DataRow("ShowPromotionOffer")]
    [DataRow("ShowNoCompanionRoom")]
    [DataRow("ShowRenamePrompt")]
    [DataRow("ShowDismissPicker")]
    [DataRow("ShowDismissConfirm")]
    public void PopupMethod_WaitsForPlayer_PausesTheCampaign(string method)
    {
        var body = MethodBody(method);

        StringAssert.Contains(body, "pauseGameActiveState: true",
            $"{method} must raise its inquiry with pauseGameActiveState: true; without it the "
            + "campaign map keeps running while the popup waits for an answer");
        Assert.IsFalse(body.Contains("pauseGameActiveState: false"),
            $"{method} raises an inquiry that leaves the campaign running");
    }

    private static string MethodBody(string method)
    {
        var src = ReadSource(AdapterPath, stripComments: true);
        int start = src.IndexOf("public void " + method + "(", StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, $"InquiryPresenterAdapter no longer declares {method}");
        int next = src.IndexOf("public void ", start + 1, StringComparison.Ordinal);
        return next < 0 ? src.Substring(start) : src.Substring(start, next - start);
    }
}
