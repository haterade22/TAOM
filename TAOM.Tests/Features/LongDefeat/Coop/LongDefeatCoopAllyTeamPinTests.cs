using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.LongDefeat.Coop;

/// <summary>
/// In a Coop session (#1280) Coop adds a defender ally team for the other players' lords, and the engine puts a plain troop origin on it
/// (Mission.cs:5229-5233). So every TAOM spawn names its team, the garrison counts only its own team, and a hit on an ally is no
/// defender hit. The adapter reads engine agents, which a unit test cannot build, so these guards are pinned in the source.
/// </summary>
[TestClass]
public class LongDefeatCoopAllyTeamPinTests
{
    [TestMethod]
    public void Classify_IgnoresADefenderLossOffTheGarrisonsTeam_BeforeItCountsOne()
    {
        var source = RepoPaths.ReadSource("Main/Adapters/LongDefeatBattlefieldAdapter.Diagnostics.cs", stripComments: true);
        var body = source.Substring(source.IndexOf("public LdRemoval Classify(", System.StringComparison.Ordinal));

        var guard = Regex.Matches(body, @"if \(team != _mission\.DefenderTeam\)\s*return new LdRemoval\(LdRemovalKind\.None");
        Assert.AreEqual(1, guard.Count);
        Assert.IsTrue(body.IndexOf("if (team != _mission.DefenderTeam)", System.StringComparison.Ordinal)
            < body.IndexOf("LdRemovalKind.Defender", System.StringComparison.Ordinal), "the guard must come before the garrison loss");
    }

    [TestMethod]
    public void Spawn_PutsTheTroopOnTheTeamItWasGiven()
    {
        var source = RepoPaths.ReadSource("Main/Adapters/LongDefeatBattlefieldAdapter.cs", stripComments: true);
        var body = source.Substring(source.IndexOf("private Agent Spawn(", System.StringComparison.Ordinal));
        body = body.Substring(0, body.IndexOf("SpawnTroopWithAgentBuildData", System.StringComparison.Ordinal));

        StringAssert.Contains(body, ".Team(team)");
        StringAssert.Contains(body, "team.GetFormation(");
    }

    [TestMethod]
    public void TryHitKeys_TakesOnlyTheGarrisonsTeamAsTheVictim()
    {
        var source = RepoPaths.ReadSource("Main/Adapters/LongDefeatBattlefieldAdapter.Diagnostics.cs", stripComments: true);
        var body = source.Substring(source.IndexOf("public bool TryHitKeys(", System.StringComparison.Ordinal));
        body = body.Substring(0, body.IndexOf("_registry.TryGetKey", System.StringComparison.Ordinal));

        StringAssert.Contains(body, "hurt.Team != _mission.DefenderTeam");
    }
}
