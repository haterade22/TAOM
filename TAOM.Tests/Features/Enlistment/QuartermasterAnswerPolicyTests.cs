using System;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Enlistment.Equipment;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// #829: the quartermaster used to answer "you have drawn your due already" for every REFUSAL (the
/// issued line outranked it). The policy maps each issuance result to its own answer, the behavior's
/// authority branch sets the co-op client's host-only reply, and the dialog source must give each
/// conditioned answer its own line plus one catch-all line.
/// </summary>
[TestClass]
public class QuartermasterAnswerPolicyTests
{
    private const string BehaviorPath = "Main/Features/Enlistment/Hooks/EnlistmentQuartermasterBehavior.cs";
    private const string CatchAllId = "taom_enlist_qm_refused_load";

    [TestMethod]
    public void For_Issued_ReturnsIssued() =>
        Assert.AreEqual(QuartermasterAnswer.Issued, QuartermasterAnswerPolicy.For(EquipmentIssueResult.Issued));

    [TestMethod]
    public void For_AlreadyIssuedForRank_ReturnsAlreadyDrawn() =>
        Assert.AreEqual(QuartermasterAnswer.AlreadyDrawn,
            QuartermasterAnswerPolicy.For(EquipmentIssueResult.AlreadyIssuedForRank));

    [TestMethod]
    public void For_NoRosterFound_ReturnsEmptyStores() =>
        Assert.AreEqual(QuartermasterAnswer.EmptyStores,
            QuartermasterAnswerPolicy.For(EquipmentIssueResult.NoRosterFound));

    [TestMethod]
    public void For_NoValidItems_ReturnsEmptyStores() =>
        Assert.AreEqual(QuartermasterAnswer.EmptyStores,
            QuartermasterAnswerPolicy.For(EquipmentIssueResult.NoValidItems));

    [TestMethod]
    public void For_PartyUnavailable_ReturnsCannotLoad() =>
        Assert.AreEqual(QuartermasterAnswer.CannotLoad,
            QuartermasterAnswerPolicy.For(EquipmentIssueResult.PartyUnavailable));

    [TestMethod]
    public void For_UndefinedResult_ReturnsCannotLoad() =>
        Assert.AreEqual(QuartermasterAnswer.CannotLoad,
            QuartermasterAnswerPolicy.For((EquipmentIssueResult)99));

    [TestMethod]
    public void Behavior_ClientBranch_SetsHostOnlyAndSkipsTheIssuer()
    {
        var src = RepoPaths.ReadSource(BehaviorPath, stripComments: true);
        int start = src.IndexOf("if (!_coopSession.IsAuthority)", StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, "the authority check is gone from OnKitRequested");
        int end = src.IndexOf("return;", start, StringComparison.Ordinal);
        Assert.IsTrue(end > start, "could not find the return that ends the client branch");
        var branch = src.Substring(start, end - start);

        StringAssert.Contains(branch, "_lastAnswer = QuartermasterAnswer.HostOnly;");
        Assert.IsFalse(branch.Contains("IssueForRank"), "a co-op client must never reach the issuer");
        Assert.IsTrue(src.IndexOf("IssueForRank", StringComparison.Ordinal) > end,
            "the issuer must be reached only after the client branch returns");
    }

    [TestMethod]
    public void Behavior_AuthorityPath_SetsTheAnswerFromThePolicy()
    {
        var src = RepoPaths.ReadSource(BehaviorPath, stripComments: true);
        int end = src.IndexOf("return;", src.IndexOf("if (!_coopSession.IsAuthority)", StringComparison.Ordinal),
            StringComparison.Ordinal);
        Assert.IsTrue(end > 0, "could not find the client branch");

        StringAssert.Contains(src.Substring(end), "_lastAnswer = QuartermasterAnswerPolicy.For(result);");
        Assert.AreEqual(2, Regex.Matches(src, @"_lastAnswer\s*=[^=]").Count,
            "only the client branch and the policy may set the answer");
    }

    [TestMethod]
    public void Behavior_AnswerToken_IsSpelledTheSameOnEveryLine()
    {
        // The player line outputs to this token and every reply reads it; a typo in one copy would
        // make every request fall through to the catch-all line.
        var src = RepoPaths.ReadSource(BehaviorPath, stripComments: true);
        Assert.AreEqual(3, Regex.Matches(src, "\"taom_enlist_qm_answer\"").Count);
    }

    [TestMethod]
    public void Behavior_EveryAnswer_AppearsInTheExpectedNumberOfConditions()
    {
        var src = RepoPaths.ReadSource(BehaviorPath, stripComments: true);
        foreach (var answer in Enum.GetValues(typeof(QuartermasterAnswer)).Cast<QuartermasterAnswer>())
        {
            int expected = answer == QuartermasterAnswer.CannotLoad ? 0 : 1;
            int count = Regex.Matches(src, @"_lastAnswer == QuartermasterAnswer\." + answer + @"\b").Count;
            Assert.AreEqual(expected, count,
                $"{answer} must be the condition of {expected} dialog line(s); CannotLoad is the catch-all");
        }
    }

    [TestMethod]
    public void Behavior_EachAnswer_UsesItsOwnLine()
    {
        var src = RepoPaths.ReadSource(BehaviorPath, stripComments: true);
        var table = new[]
        {
            (QuartermasterAnswer.Issued, "taom_enlist_qm_issued"),
            (QuartermasterAnswer.AlreadyDrawn, "taom_enlist_qm_refused"),
            (QuartermasterAnswer.EmptyStores, "taom_enlist_qm_refused_stores"),
            (QuartermasterAnswer.HostOnly, "taom_enlist_qm_refused_host"),
        };

        foreach (var (answer, key) in table)
        {
            int cond = src.IndexOf("_lastAnswer == QuartermasterAnswer." + answer, StringComparison.Ordinal);
            Assert.IsTrue(cond >= 0, $"{answer} has no condition");
            int start = src.LastIndexOf("AddAnswer(", cond, StringComparison.Ordinal);
            Assert.IsTrue(start >= 0, $"no registration found before the {answer} condition");
            int end = src.IndexOf(");", cond, StringComparison.Ordinal);
            Assert.IsTrue(end > start, $"could not find the end of the {answer} registration");
            var block = src.Substring(start, end - start);
            StringAssert.Contains(block, "\"" + key + "\"", $"{answer} must register id {key}");
            StringAssert.Contains(block, "{=" + key + "}", $"{answer} must carry key {key}");
        }
    }

    [TestMethod]
    public void Behavior_CatchAllLine_HasNullConditionNullConsequenceAndLowestPriority()
    {
        var src = RepoPaths.ReadSource(BehaviorPath, stripComments: true);
        var open = Regex.Match(src, @"starter\.AddDialogLine\(\s*""" + CatchAllId + @"""");
        Assert.IsTrue(open.Success, "the catch-all line is not registered with its own AddDialogLine");
        int start = open.Index;
        int end = src.IndexOf(");", start, StringComparison.Ordinal);
        Assert.IsTrue(end > start, "could not find the end of the catch-all registration");
        var call = src.Substring(start, end - start);

        var match = Regex.Match(call, @"null\s*,\s*null\s*,\s*(\d+)\s*$");
        Assert.IsTrue(match.Success, "the catch-all needs a null condition and a null consequence");
        int catchAll = int.Parse(match.Groups[1].Value);

        var helper = Regex.Match(src, @"""close_window"",\s*text,\s*condition,\s*null,\s*(\d+)\)");
        Assert.IsTrue(helper.Success, "could not read the conditioned lines' priority");
        int conditioned = int.Parse(helper.Groups[1].Value);

        Assert.AreEqual(112, conditioned);
        Assert.IsTrue(catchAll < conditioned, "the catch-all must sit below every conditioned line");
        Assert.AreEqual(1, Regex.Matches(src, @"\{=" + CatchAllId + @"\}").Count,
            "the cannot-load sentence must be registered once");
    }
}
