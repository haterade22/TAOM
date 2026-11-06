using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Adapters;
using TAOM.Features.LongDefeat.Domain;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.LongDefeat;

/// <summary>
/// The two decisions the Long Defeat adapters make that need no engine: which attacker the late <c>scale</c> sample reads (a roster soldier, never the commander or a summoned one), and the health
/// a felled soldier is brought to so <c>KillAgentCheat</c>'s blow of 2000 is lethal. The engine calls themselves are tied to these by source.
/// </summary>
[TestClass]
public class LongDefeatAdapterRulesTests
{
    [TestMethod]
    public void FirstRoster_SkipsTheCommanderAndTheSummoned_ReadingTheOldestRosterSoldier()
    {
        var registry = new LongDefeatAgentRegistry();
        var commander = "commander";
        var summoned = "summoned";
        var roster = "roster";
        var later = "later";
        registry.Register(commander, AttackerRole.Commander);
        registry.Register(summoned, AttackerRole.Summoned);
        registry.Register(roster, AttackerRole.Roster);
        registry.Register(later, AttackerRole.Roster);

        Assert.AreSame(roster, LongDefeatEffectsAdapter.FirstRoster(new[] { commander, summoned, roster, later }, registry, _ => true));
        Assert.AreSame(later, LongDefeatEffectsAdapter.FirstRoster(new[] { commander, summoned, roster, later }, registry, a => a != roster));
    }

    [TestMethod]
    public void FirstRoster_FindsNoneWhenOnlyTheCommanderAndSummonsStand()
    {
        var registry = new LongDefeatAgentRegistry();
        registry.Register("commander", AttackerRole.Commander);
        registry.Register("summoned", AttackerRole.Summoned);

        Assert.IsNull(LongDefeatEffectsAdapter.FirstRoster(new[] { "commander", "summoned" }, registry, _ => true));
    }

    [TestMethod]
    public void FirstRoster_WithNoRegistry_TakesTheFirstThatFits()
    {
        Assert.AreEqual("b", LongDefeatEffectsAdapter.FirstRoster(new[] { "a", "b", "c" }, null, a => a != "a"));
    }

    [TestMethod]
    public void ReadScale_PicksItsAttackerThroughFirstRoster()
    {
        var body = Body("Main/Adapters/LongDefeatEffectsAdapter.cs", "public LdScaleSample ReadScale()");

        StringAssert.Contains(body, "FirstRoster(");
    }

    [TestMethod]
    public void FellHealth_BringsAnyHealthUnderTheBlowsDamage_AndLeavesLowerHealthAlone()
    {
        // Mission.KillAgentCheat inflicts 2000 (Mission.cs:6228), and Agent.HandleBlow kills only when the health left is under 1 (Agent.cs:5502-5533).
        Assert.IsTrue(LongDefeatBattlefieldAdapter.FellHealth(2415f) < 2000f);
        Assert.IsTrue(LongDefeatBattlefieldAdapter.FellHealth(4410f) < 2000f);
        Assert.AreEqual(37f, LongDefeatBattlefieldAdapter.FellHealth(37f));
        Assert.AreEqual(0f, LongDefeatBattlefieldAdapter.FellHealth(0f));
    }

    [TestMethod]
    public void Fell_WritesTheHealthBeforeTheBlow()
    {
        var body = Body("Main/Adapters/LongDefeatBattlefieldAdapter.cs", "public int Fell(int count)");

        var write = body.IndexOf("FellHealth(", System.StringComparison.Ordinal);
        var kill = body.IndexOf("KillAgentCheat(", System.StringComparison.Ordinal);
        Assert.IsTrue(write >= 0 && kill > write, "Fell must lower a standing soldier's health before KillAgentCheat");
    }

    [TestMethod]
    public void Read_MeansTheHealthOfTheSamePeopleRestDefendersHeals_RunnersIncluded()
    {
        // The field-care check compares the next tick's reading with RestDefenders' own read-back, so both walk every active human of the defending team with a limit, running away or not.
        var read = Body("Main/Adapters/LongDefeatBattlefieldAdapter.Diagnostics.cs", "public LdReading Read(");
        var rest = Body("Main/Adapters/LongDefeatBattlefieldAdapter.cs", "public LdRest RestDefenders(float share)");

        var health = read.IndexOf("agent.Health / ", System.StringComparison.Ordinal);
        var runner = read.IndexOf("defendersFleeing++", System.StringComparison.Ordinal);
        Assert.IsTrue(health >= 0 && runner > health, "the defenders' health is summed before the loop sets a runner aside");
        StringAssert.Contains(read, "healthShares / healthRead");
        StringAssert.Contains(rest, "!agent.IsHuman || !agent.IsActive() || !(limit > 0f)");
        StringAssert.Contains(read, "if (limit > 0f)");
    }

    [TestMethod]
    public void RestDefenders_ReadsEachHealthBackAfterTheWrite_SoTheCheckSeesWhatTheEngineKept()
    {
        var rest = Body("Main/Adapters/LongDefeatBattlefieldAdapter.cs", "public LdRest RestDefenders(float share)");

        var write = rest.IndexOf("agent.Health = ", System.StringComparison.Ordinal);
        var readBack = rest.IndexOf("after += agent.Health / limit", System.StringComparison.Ordinal);
        Assert.IsTrue(write >= 0 && readBack > write, "the mean after comes from the engine's stored health, not from the value written");
        StringAssert.Contains(rest, "Math.Min(limit, health + share * (limit - health))");
    }

    private static string Body(string relative, string header)
    {
        var path = RepoPaths.CSharpFiles("Main").Single(f => f.Replace('\\', '/').EndsWith("/" + relative));
        var source = RepoPaths.StripComments(File.ReadAllText(path));
        var start = source.IndexOf(header, System.StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, header + " not found in " + relative);
        var next = Regex.Match(source.Substring(start + header.Length), @"\n    (public|private|internal|protected)\b");
        return next.Success ? source.Substring(start, header.Length + next.Index) : source.Substring(start);
    }
}
