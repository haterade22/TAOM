using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.Messengers;

/// <summary>
/// Source pins on the conversation start (#898). They only read the file, so they run without the game, unlike the
/// RequiresGame class that holds the wiring checks.
/// </summary>
[TestClass]
public class MessengerCampaignBehaviorSourceTests
{
    private const string Path = "Main/Features/Messengers/MessengerCampaignBehavior.cs";

    [TestMethod]
    public void TheConversationStart_RecoversAndRethrows_WhenAnythingAfterOpenThrows()
    {
        var src = RepoPaths.ReadSource(Path, stripComments: true);

        StringAssert.Matches(src, new Regex(@"_conversation\.Open\(\);\s*try\s*\{"));
        StringAssert.Matches(src, new Regex(@"catch\s*\{\s*AbortConversation\(\);\s*throw;\s*\}"));
    }

    [TestMethod]
    public void TheNullMissionBranch_UsesTheSameRecovery()
    {
        var src = RepoPaths.ReadSource(Path, stripComments: true);

        StringAssert.Matches(src, new Regex(@"else\s*\{\s*_logger\.LogWarning\([^;]*\);\s*AbortConversation\(\);\s*\}"));
    }

    [TestMethod]
    public void AbortConversation_SchedulesTheSettlementCleanup_SoAFailedOpenFinishesTheEncounter()
    {
        var src = RepoPaths.ReadSource(Path, stripComments: true);

        StringAssert.Matches(src, new Regex(
            @"void AbortConversation\(\)\s*\{[^}]*_conversation\.Close\(\);\s*CampaignEvents\.TickEvent\.AddNonSerializedListener\(this,\s*CleanUpSettlementEncounter\);"));
    }

    [TestMethod]
    public void AbortConversation_KeepsTheOriginalPosition_SoTheCleanupCanRestoreIt()
    {
        var src = RepoPaths.ReadSource(Path, stripComments: true);

        var body = Regex.Match(src, @"void AbortConversation\(\)\s*\{[^}]*\}").Value;

        Assert.IsTrue(body.Length > 0, "AbortConversation not found");
        Assert.IsFalse(body.Contains("_originalPosition"), "AbortConversation must leave _originalPosition for CleanUpSettlementEncounter to restore");
    }
}
