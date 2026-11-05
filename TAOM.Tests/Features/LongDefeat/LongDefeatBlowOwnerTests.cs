using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.LongDefeat;

/// <summary>
/// A kill needs a living affector (phase2-plan.md, "A kill must have a living affector"): <c>AgentMoraleInteractionLogic</c> reads the affector's
/// formation with no null test, and <c>Agent.Die</c> swaps in a stale last hitter when the owner is -1 or the victim itself. The only code
/// allowed to kill is <c>ApplyPoison</c> and <c>Fell</c>. <c>ApplyPoison</c> does not exist yet; <c>Fell</c> does not yet choose its owner:
/// <c>KillAgentCheat</c> uses <c>MainAgent ?? victim</c> (<c>Mission.cs:6220-6221</c>), and Slice 9 gives it a standing defender and pins that.
/// This gate reads the Long Defeat source and fails on <c>new Blow(-1)</c> anywhere, or on any <c>Agent.Die</c>, <c>KillAgentCheat</c> or
/// <c>new Blow(</c> outside those two methods, so it keeps every other member from adding a kill.
/// </summary>
[TestClass]
public class LongDefeatBlowOwnerTests
{
    private static readonly string[] Allowed = { "ApplyPoison", "Fell" };

    private static readonly Regex Kill = new(@"\bAgent\.Die\b|\.Die\s*\(|\bKillAgentCheat\b|\bnew\s+Blow\s*\(", RegexOptions.Compiled);
    private static readonly Regex NoOwnerBlow = new(@"\bnew\s+Blow\s*\(\s*-\s*1\s*\)", RegexOptions.Compiled);
    private static readonly Regex MemberHeader = new(
        @"^\s+(?:public|private|internal|protected)(?:\s+[\w<>\[\],.?]+)*?\s+(\w+)\s*(?:<[^>]*>)?\s*\(", RegexOptions.Compiled);

    /// <summary>Every offence in one file's text: "line n: text (in Member)". Comments are blanked first, so prose naming a call is not one.</summary>
    internal static List<string> Offences(string text)
    {
        var offences = new List<string>();
        var member = "(outside any member)";
        var lines = RepoPaths.StripComments(text.Replace("\r\n", "\n")).Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var header = MemberHeader.Match(lines[i]);
            // A declaration ending in ';' is an interface or abstract member with no body, unless it is an expression-bodied member (=>).
            if (header.Success && (!lines[i].TrimEnd().EndsWith(";") || lines[i].Contains("=>")))
                member = header.Groups[1].Value;

            if (NoOwnerBlow.IsMatch(lines[i]))
                offences.Add($"line {i + 1}: {lines[i].Trim()} (a blow with owner -1 lets Agent.Die swap in a stale hitter)");
            else if (Kill.IsMatch(lines[i]) && !Allowed.Contains(member))
                offences.Add($"line {i + 1}: {lines[i].Trim()} (in {member}; only ApplyPoison and Fell may kill)");
        }

        return offences;
    }

    private static IEnumerable<string> LongDefeatSource()
    {
        var files = RepoPaths.CSharpFiles("Main", "Client")
            .Where(f => f.Replace('\\', '/').Contains("/Features/LongDefeat/")
                || Path.GetFileName(f).StartsWith("LongDefeat") && f.Replace('\\', '/').Contains("/Main/Adapters/")
                || Path.GetFileName(f).StartsWith("ILongDefeat") && f.Replace('\\', '/').Contains("/Main/Adapters/"))
            .ToList();
        Assert.IsTrue(files.Count > 20, "the scan found only " + files.Count + " Long Defeat files; the folders moved?");
        return files;
    }

    [TestMethod]
    public void LongDefeatSource_KillsOnlyInsideApplyPoisonAndFell_AndNeverWithAnOwnerOfMinusOne()
    {
        var offences = LongDefeatSource()
            .SelectMany(f => Offences(File.ReadAllText(f)).Select(o => Path.GetFileName(f) + " " + o))
            .ToList();

        Assert.AreEqual(0, offences.Count, "a kill needs a living affector:\n" + string.Join("\n", offences));
    }

    [TestMethod]
    public void TheScan_SeesFellsKill_SoAnEmptyResultMeansNothingElseKills()
    {
        var all = LongDefeatSource().Select(f => File.ReadAllText(f)).ToList();

        Assert.IsTrue(all.Any(t => Regex.IsMatch(RepoPaths.StripComments(t), @"\bKillAgentCheat\b")), "Fell kills through KillAgentCheat today; the scan must see that call");
    }

    [TestMethod]
    public void Offences_CatchesAKillOutsideTheAllowedMembers()
    {
        const string source = @"
internal sealed class X
{
    public void Tidy()
    {
        _mission.KillAgentCheat(agent);
    }
}";

        var offences = Offences(source);

        Assert.AreEqual(1, offences.Count);
        StringAssert.Contains(offences[0], "in Tidy");
    }

    [TestMethod]
    public void Offences_CatchesEachKillFormOutsideTheAllowedMembers()
    {
        foreach (var call in new[] { "agent.Die(blow);", "Agent.Die(blow);", "var b = new Blow(5);", "m.KillAgentCheat(a);" })
        {
            var source = "class X\n{\n    private void Other()\n    {\n        " + call + "\n    }\n}";

            Assert.AreEqual(1, Offences(source).Count, call);
        }
    }

    [TestMethod]
    public void Offences_AllowsTheKillInsideFellAndApplyPoison()
    {
        const string source = @"
class X
{
    public int Fell(int count)
    {
        _mission.KillAgentCheat(agent);
        return 1;
    }

    public void ApplyPoison(int victim)
    {
        var blow = new Blow(owner);
        agent.Die(blow, Agent.KillInfo.Gravity);
    }
}";

        Assert.AreEqual(0, Offences(source).Count);
    }

    [TestMethod]
    public void Offences_AnOwnerOfMinusOneFailsEvenInsideTheAllowedMembers()
    {
        const string source = @"
class X
{
    public void ApplyPoison(int victim)
    {
        var blow = new Blow(-1);
    }
}";

        var offences = Offences(source);

        Assert.AreEqual(1, offences.Count);
        StringAssert.Contains(offences[0], "-1");
    }

    [TestMethod]
    public void Offences_SpacingInTheMinusOneDoesNotHideIt()
    {
        Assert.AreEqual(1, Offences("class X\n{\n    public void Fell()\n    {\n        var b = new Blow( - 1 );\n    }\n}").Count);
    }

    [TestMethod]
    public void Offences_AMemberAfterFellIsNotCoveredByFell()
    {
        const string source = @"
class X
{
    public int Fell(int count)
    {
        return 0;
    }

    public void AdoptMain()
    {
        _mission.KillAgentCheat(agent);
    }
}";

        var offences = Offences(source);

        Assert.AreEqual(1, offences.Count);
        StringAssert.Contains(offences[0], "in AdoptMain");
    }

    [TestMethod]
    public void Offences_AnExpressionBodiedMemberThatKillsIsNotCreditedToTheMemberBeforeIt()
    {
        const string source = @"
class X
{
    public int Fell(int count)
    {
        return 0;
    }

    private void Tidy() => _m.KillAgentCheat(a);
}";

        var offences = Offences(source);

        Assert.AreEqual(1, offences.Count);
        StringAssert.Contains(offences[0], "in Tidy");
    }

    [TestMethod]
    public void Offences_AnExpressionBodiedFellMayKill()
    {
        Assert.AreEqual(0, Offences(@"class X
{
    public void Fell() => _m.KillAgentCheat(a);
}").Count);
    }

    [TestMethod]
    public void Offences_ABodylessDeclarationDoesNotChangeTheCurrentMember()
    {
        const string source = @"interface I
{
    void Other();
}
class X
{
    public int Fell(int count)
    {
        _m.KillAgentCheat(a);
        return 1;
    }
}";

        Assert.AreEqual(0, Offences(source).Count);
    }

    [TestMethod]
    public void Offences_ACommentNamingTheCallIsNotACall()
    {
        const string source = "class X\n{\n    public void Tidy()\n    {\n        // never KillAgentCheat here, and no new Blow(-1)\n        /* agent.Die( */\n    }\n}";

        Assert.AreEqual(0, Offences(source).Count);
    }
}
