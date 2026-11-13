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
/// allowed to kill is <c>ApplyPoison</c> and <c>Fell</c>. <c>KillAgentCheat</c> uses <c>MainAgent ?? victim</c> (<c>Mission.cs:6220-6221</c>), so with the player dead the victim
/// would own his own death; <c>Fell</c> therefore keeps <c>KillAgentCheat</c> only while the player's body stands and otherwise strikes with its own blow owned by a standing defender,
/// and <c>ApplyPoison</c> gives every killing tick an owner through <c>PoisonOwner.Choose</c>. The two pins below read those members' source. This gate reads the Long Defeat source and fails on <c>new Blow(-1)</c> anywhere, or on any <c>Agent.Die</c>, <c>KillAgentCheat</c> or
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

    private static string SourceOf(string fileName) =>
        RepoPaths.StripComments(File.ReadAllText(LongDefeatSource().Single(f => Path.GetFileName(f) == fileName)).Replace("\r\n", "\n"));

    // The text of the first block after the marker, by brace depth.
    private static string BodyOf(string source, string marker)
    {
        var at = source.IndexOf(marker, System.StringComparison.Ordinal);
        Assert.IsTrue(at >= 0, marker + " is not in the source");
        var open = source.IndexOf('{', at);
        var depth = 0;
        for (var i = open; i < source.Length; i++)
        {
            if (source[i] == '{')
                depth++;
            else if (source[i] == '}' && --depth == 0)
                return source.Substring(open, i - open + 1);
        }

        Assert.Fail(marker + " has no closing brace");
        return "";
    }

    [TestMethod]
    public void Fell_WithNoMainAgentStanding_StrikesWithItsOwnBlowOwnedByAStandingDefender()
    {
        var source = SourceOf("LongDefeatBattlefieldAdapter.cs");
        var fell = BodyOf(source, "public int Fell(");
        var owner = BodyOf(source, "private Agent? LivingOwner()");

        // The player's body keeps KillAgentCheat (its owner is MainAgent); without one, the blow is built here with a standing defender's index, never -1 and never the victim. LivingOwner picks the
        // owner, and BreakEngines (#1160) uses the same one.
        StringAssert.Contains(fell, "LivingOwner()");
        StringAssert.Contains(fell, "MainAgent");
        StringAssert.Contains(owner, "MainAgent");
        StringAssert.Contains(owner, "IsActive()");
        StringAssert.Contains(owner, "StandingDefenders()");
        StringAssert.Contains(fell, "new Blow(owner.Index)");
        StringAssert.Contains(fell, "RegisterBlow(");
        Assert.IsFalse(Regex.IsMatch(fell, @"new\s+Blow\s*\(\s*agent\.Index"), "the felled soldier must not own his own death");
        Assert.IsTrue(fell.IndexOf("KillAgentCheat", System.StringComparison.Ordinal) > fell.IndexOf("MainAgent", System.StringComparison.Ordinal), "KillAgentCheat only when the main agent stands");
    }

    [TestMethod]
    public void Fell_MarksTheSoldierBeforeAnyKill_AndCapsHisHealthSoTheBlowIsLethal()
    {
        var fell = BodyOf(SourceOf("LongDefeatBattlefieldAdapter.cs"), "public int Fell(");

        var mark = fell.IndexOf("MarkFelled(", System.StringComparison.Ordinal);
        Assert.IsTrue(mark >= 0);
        Assert.IsTrue(mark < fell.IndexOf("KillAgentCheat", System.StringComparison.Ordinal) && mark < fell.IndexOf("RegisterBlow(", System.StringComparison.Ordinal), "marked before the kill");
        Assert.IsTrue(fell.IndexOf("FellHealth(", System.StringComparison.Ordinal) < fell.IndexOf("RegisterBlow(", System.StringComparison.Ordinal), "health capped under half the blow before it lands");
    }

    [TestMethod]
    public void Fell_WithNoDefenderStanding_MarksAndKillsNobody()
    {
        var fell = BodyOf(SourceOf("LongDefeatBattlefieldAdapter.cs"), "public int Fell(");

        var none = fell.IndexOf("owner == null", System.StringComparison.Ordinal);
        var mark = fell.IndexOf("MarkFelled(", System.StringComparison.Ordinal);
        Assert.IsTrue(none >= 0 && none < mark, "an owner is found before anyone is marked");
        StringAssert.Contains(fell.Substring(none, mark - none), "return 0");
    }

    [TestMethod]
    public void ApplyPoison_ChoosesItsOwnerThroughPoisonOwner_AndReadsIsActiveAtTheCall()
    {
        var poison = BodyOf(SourceOf("LongDefeatEffectsAdapter.cs"), "public LdPoisonResult ApplyPoison(");

        var choose = poison.IndexOf("PoisonOwner.Choose(", System.StringComparison.Ordinal);
        var active = poison.IndexOf("IsLive(owner)", choose, System.StringComparison.Ordinal);
        var die = poison.IndexOf(".Die(", System.StringComparison.Ordinal);
        Assert.IsTrue(choose >= 0 && active > choose && die > active, "the owner is chosen, then read for IsLive right here, then the blow lands");
        StringAssert.Contains(poison, "new Blow(owner.Index)");
        StringAssert.Contains(poison, "OwnerMissing");
        StringAssert.Contains(poison, "Withheld");
    }

    // A deleted Agent answers IsActive() for the slot's next tenant (csharp-architecture.md, "Mission-scope agent handles"), so every handle the registry holds across frames and the poison then reads
    // goes through IsLive (IsActive plus AgentSlotIdentity.IsCurrentOccupant), as the Fords adapter's IsLive does. An owner read through a stale slot would hand Die the index of another man.
    [TestMethod]
    public void ApplyPoison_GatesTheVictimAndTheOwnerBySlotIdentity_NeverIsActiveAlone()
    {
        var poison = BodyOf(SourceOf("LongDefeatEffectsAdapter.cs"), "public LdPoisonResult ApplyPoison(");

        StringAssert.Contains(poison, "IsLive(agent)");
        StringAssert.Contains(poison, "IsLive(owner)");
        Assert.IsFalse(poison.Contains(".IsActive()"), "IsActive alone passes for a stale handle");
    }

    // A health that is not a number must not reach the int cast: Math.Max(0f, NaN) is NaN and (int)NaN is int.MinValue, which the withheld branch would wrap into a huge damage (rule 4b).
    [TestMethod]
    public void ApplyPoison_CastsTheHealthOnlyAfterAPositiveTest()
    {
        var poison = BodyOf(SourceOf("LongDefeatEffectsAdapter.cs"), "public LdPoisonResult ApplyPoison(");

        Assert.IsFalse(poison.Contains("Math.Max(0f, agent.Health)"), "Math.Max(0f, NaN) is NaN");
        var test = poison.IndexOf("health > 0f", System.StringComparison.Ordinal);
        var cast = poison.IndexOf("(int)health", System.StringComparison.Ordinal);
        Assert.IsTrue(test >= 0 && cast > test, "the health is tested positive, then cast");
    }

    [TestMethod]
    public void TheOwnerCandidates_AreGatedBySlotIdentity_NeverIsActiveAlone()
    {
        var source = SourceOf("LongDefeatEffectsAdapter.cs");

        foreach (var member in new[] { "private AgentKey? StandingAttackerKey(", "private AgentKey? OwnerCandidate(" })
        {
            var body = BodyOf(source, member);
            StringAssert.Contains(body, "IsLive(a)", member);
            Assert.IsFalse(body.Contains(".IsActive()"), member + " trusts IsActive alone");
        }

        StringAssert.Contains(source, "AgentSlotIdentity.IsCurrentOccupant(agent)");
    }

    // A removal the registry holds must reach the run even when the agent has no team by then (a deleted agent is set to no team, Mission.cs:2982-2996): the registry entry is taken first, and the
    // null-team answer carries its key so the run forgets it.
    [TestMethod]
    public void Classify_TakesTheRemovalBeforeTheNullTeamReturn_AndKeepsTheKey()
    {
        var classify = BodyOf(SourceOf("LongDefeatBattlefieldAdapter.Diagnostics.cs"), "public LdRemoval Classify(");

        var take = classify.IndexOf("TakeRemoval(", System.StringComparison.Ordinal);
        var nullTeam = classify.IndexOf("team == null", System.StringComparison.Ordinal);
        Assert.IsTrue(take >= 0 && nullTeam > take, "TakeRemoval comes before the null-team return");
        var answer = classify.Substring(nullTeam, System.Math.Min(120, classify.Length - nullTeam));
        StringAssert.Contains(answer, "Key: key", "the null-team answer keeps the key");
    }

    // ---- the Elven Volley's scripted missiles (phase3-plan.md D22, Slice 6) ----

    private static readonly Regex Missile = new(@"\bAddCustomMissile\b", RegexOptions.Compiled);

    /// <summary>Every <c>AddCustomMissile</c> in one file's text that is not inside <c>FireVolley</c>: "line n: text (in Member)". A scripted arrow kills like any other, so only the member that picks a real owner may add one.</summary>
    internal static List<string> MissileOffences(string text)
    {
        var offences = new List<string>();
        var member = "(outside any member)";
        var lines = RepoPaths.StripComments(text.Replace("\r\n", "\n")).Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var header = MemberHeader.Match(lines[i]);
            if (header.Success && (!lines[i].TrimEnd().EndsWith(";") || lines[i].Contains("=>")))
                member = header.Groups[1].Value;

            if (Missile.IsMatch(lines[i]) && member != "FireVolley")
                offences.Add($"line {i + 1}: {lines[i].Trim()} (in {member}; only FireVolley may add a missile)");
        }

        return offences;
    }

    [TestMethod]
    public void LongDefeatSource_AddsAMissileOnlyInsideFireVolley()
    {
        var offences = LongDefeatSource()
            .SelectMany(f => MissileOffences(File.ReadAllText(f)).Select(o => Path.GetFileName(f) + " " + o))
            .ToList();

        Assert.AreEqual(0, offences.Count, "a scripted arrow needs an owner chosen by the volley rule:\n" + string.Join("\n", offences));
        Assert.IsTrue(LongDefeatSource().Any(f => Missile.IsMatch(RepoPaths.StripComments(File.ReadAllText(f)))), "FireVolley adds its missiles through AddCustomMissile; the scan must see that call");
    }

    [TestMethod]
    public void MissileOffences_CatchesAMissileOutsideFireVolley_AndAllowsOneInside()
    {
        Assert.AreEqual(1, MissileOffences("class X\n{\n    private void Tidy()\n    {\n        _mission.AddCustomMissile(a, w, p, d, o, 1f, 1f, false, null);\n    }\n}").Count);
        Assert.AreEqual(0, MissileOffences("class X\n{\n    public LdVolleyFire FireVolley(string item)\n    {\n        _mission.AddCustomMissile(a, w, p, d, o, 1f, 1f, false, null);\n    }\n}").Count);
        Assert.AreEqual(0, MissileOffences("class X\n{\n    private void Tidy()\n    {\n        // never AddCustomMissile here\n    }\n}").Count);
    }

    [TestMethod]
    public void FireVolley_ChoosesAnOwnerBeforeAnyMissile_AndRefusesWhenThereIsNone()
    {
        var fire = BodyOf(SourceOf("LongDefeatBattlefieldAdapter.Volley.cs"), "public LdVolleyFire FireVolley(");

        var choose = fire.IndexOf("ChooseVolleyOwner(", System.StringComparison.Ordinal);
        var none = fire.IndexOf("owner == null", System.StringComparison.Ordinal);
        var refused = fire.IndexOf("VolleyWithheldReason.NoOwner", System.StringComparison.Ordinal);
        var missile = fire.IndexOf("AddCustomMissile(", System.StringComparison.Ordinal);
        Assert.IsTrue(choose >= 0 && choose < none && none < refused && refused < missile, "the owner is chosen, a missing one refuses, then the first missile: " + fire);
        StringAssert.Contains(fire, "AddCustomMissile(owner,", "every missile is owned by the chosen defender, never the main agent or a null shooter");
    }

    [TestMethod]
    public void TheVolleyOwner_IsReadForSlotIdentityAiControlAndTheMainAgent_AtTheCall()
    {
        var choose = BodyOf(SourceOf("LongDefeatBattlefieldAdapter.Volley.cs"), "private Agent? ChooseVolleyOwner(");

        StringAssert.Contains(choose, "IsLive(a)", "a recycled slot is refused");
        StringAssert.Contains(choose, "IsAIControlled", "a player-controlled shooter loses the friendly-hit cancel (E14)");
        StringAssert.Contains(choose, "IsMainAgent", "the main agent never owns a volley arrow (D22)");
        StringAssert.Contains(choose, "VolleyOwnerRule.Choose(");
        Assert.IsFalse(choose.Contains(".IsActive()"), "IsActive alone passes for a stale handle");
        Assert.IsFalse(choose.Contains("MainAgent ??"), "the main agent is not a fallback owner");
        StringAssert.Contains(SourceOf("LongDefeatBattlefieldAdapter.Volley.cs"), "agent.IsActive() && AgentSlotIdentity.IsCurrentOccupant(agent)");
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
