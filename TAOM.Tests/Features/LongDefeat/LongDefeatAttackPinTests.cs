using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.LongDefeat;

/// <summary>
/// Source pins for a story attack (#1409): the player fights on the attacker team. The adapter reads engine agents, which a unit test cannot build, so the rules are
/// pinned in the source. One member names the player's team: the attacker team only when the mission made it the player's team, else the defender team as before, so a
/// Coop follower keeps the defender team whatever Coop does with PlayerTeam. The player's body is never a wave soldier: the reads skip it, the fell never takes it, a felled
/// straggler's death is never his, and his removal is his own. The enemy heroes stand in the garrison's class formations under AI, never held.
/// </summary>
[TestClass]
public class LongDefeatAttackPinTests
{
    private const string Field = "Main/Adapters/LongDefeatBattlefieldAdapter.cs";
    private const string Player = "Main/Adapters/LongDefeatBattlefieldAdapter.Player.cs";
    private const string Diagnostics = "Main/Adapters/LongDefeatBattlefieldAdapter.Diagnostics.cs";
    private const string Heroes = "Main/Adapters/LongDefeatBattlefieldAdapter.Heroes.cs";

    private static string Source(string path) => RepoPaths.ReadSource(path, stripComments: true);

    private static string Body(string source, string marker)
    {
        var at = source.IndexOf(marker, StringComparison.Ordinal);
        Assert.IsTrue(at >= 0, marker + " not found");
        var open = source.IndexOf('{', at);
        var depth = 0;
        for (var i = open; i < source.Length; i++)
        {
            depth += source[i] == '{' ? 1 : source[i] == '}' ? -1 : 0;
            if (depth == 0)
                return source.Substring(at, i - at + 1);
        }

        Assert.Fail(marker + " has no end");
        return string.Empty;
    }

    // An expression-bodied member, up to its first semicolon.
    private static string Expression(string source, string marker)
    {
        var at = source.IndexOf(marker, StringComparison.Ordinal);
        Assert.IsTrue(at >= 0, marker + " not found");
        return source.Substring(at, source.IndexOf(';', at) - at + 1);
    }

    private static int At(string text, string part)
    {
        var at = text.IndexOf(part, StringComparison.Ordinal);
        Assert.IsTrue(at >= 0, part + " not found in: " + text);
        return at;
    }

    [TestMethod]
    public void ThePlayersSideTeam_IsTheAttackerTeamOnlyWhenItIsThePlayersTeam_ElseTheDefenderTeam_AndNothingElseReadsPlayerTeam()
    {
        var side = Expression(Source(Field), "private Team PlayerSideTeam =>");

        StringAssert.Matches(side, new Regex(@"_mission\.AttackerTeam is \{ \} attackers && ReferenceEquals\(_mission\.PlayerTeam, attackers\)\s*\?\s*attackers\s*:\s*_mission\.DefenderTeam;"));
        StringAssert.Matches(Expression(Source(Field), "private bool PlayerAttacks =>"), new Regex(@"PlayerSideTeam is \{ \} team && ReferenceEquals\(team, _mission\.AttackerTeam\);"));

        var reads = Directory.GetFiles(RepoPaths.RepoPath("Main", "Adapters"), "LongDefeatBattlefieldAdapter*.cs")
            .Sum(f => Regex.Matches(RepoPaths.ReadSource("Main/Adapters/" + Path.GetFileName(f), stripComments: true), @"\.PlayerTeam\b").Count);
        Assert.AreEqual(1, reads, "a Coop follower keeps the defender team: no member takes PlayerTeam itself as the team to spawn on or adopt");
    }

    [TestMethod]
    public void SpawnPlayer_TakesThePlayersSideTeam_AndAnAttackerSpawnsAloneFromTheAttackerReinforcementPlan()
    {
        var spawn = Body(Source(Player), "public bool SpawnPlayer(");

        StringAssert.Contains(spawn, "var team = PlayerSideTeam;");
        StringAssert.Contains(spawn, "var attacks = PlayerAttacks;");
        StringAssert.Contains(spawn, "if (respawn || attacks)", "an attacker never joins the defenders' open foot batch");
        StringAssert.Matches(spawn, new Regex(@"if \(attacks && !formation\.HasBeenPositioned\)\s*_mission\.SetFormationPositioningFromDeploymentPlan\(formation, true\);"));
        Assert.IsTrue(At(spawn, "formation.BeginSpawn(1, false);") < At(spawn, "SetFormationPositioningFromDeploymentPlan("), "the order PlaceFormation uses");
        Assert.IsTrue(At(spawn, "SetFormationPositioningFromDeploymentPlan(") < At(spawn, "Spawn(player, team, true,"));
        Assert.IsFalse(spawn.Contains("DefenderTeam"), spawn);
    }

    [TestMethod]
    public void AdoptMain_BootFacts_AndTheDescribeLines_ReadThePlayersSideTeam()
    {
        var adopt = Body(Source(Field), "public bool AdoptMain(");
        var diagnostics = Source(Diagnostics);
        var player = Body(diagnostics, "public string DescribePlayer(");
        var boot = Body(diagnostics, "public LdBootFacts BootFacts(");
        var formations = Body(diagnostics, "public string DescribeFormations(");

        StringAssert.Contains(adopt, "var team = PlayerSideTeam;");
        Assert.IsFalse(adopt.Contains("DefenderTeam"), adopt);
        StringAssert.Contains(player, "PlayerSideTeam?.PlayerOrderController?.Owner");
        Assert.IsFalse(player.Contains("DefenderTeam"), player);
        StringAssert.Contains(boot, "var team = PlayerSideTeam;");
        StringAssert.Contains(boot, "MainOnPlayerTeam: main != null && ReferenceEquals(main.Team, team)");
        StringAssert.Contains(boot, "PlayerAttacks: PlayerAttacks");
        foreach (var read in new[] { "team?.GeneralAgent", "team?.IsPlayerGeneral", "team?.PlayerOrderController?.Owner" })
            StringAssert.Contains(boot, read);
        StringAssert.Contains(formations, "PlayerSideTeam", "ai_formations= keeps the player's own team");
        StringAssert.Contains(formations, "_mission.DefenderTeam", "the #1370 signal: the defenders' AI state in an attack too");
        Assert.IsTrue(At(formations, "ai_formations=") < At(formations, " defender_ai="), "the old text first, the new field after it");
    }

    [TestMethod]
    public void DescribePlayer_GivesTheAttackerFootFrameWhenHeAttacks_AndTheMissionsBoundaryTestForHisPlace()
    {
        var player = Body(Source(Diagnostics), "public string DescribePlayer(");

        StringAssert.Contains(player, "PlayerAttacks && FrameOf(_mission.AttackerTeam, FormationClass.Infantry, true) is { } frame");
        StringAssert.Contains(player, "_mission.IsPositionInsideBoundaries(main.Position.AsVec2)", "the test MissionBoundaryCrossingHandler makes for the main agent");
        Assert.IsTrue(At(player, "at={At(main.Position)}") < At(player, " inside="), "inside= ends the line");
    }

    [TestMethod]
    public void ReadAttackersNearestAndFell_SkipThePlayersBody()
    {
        var diagnostics = Source(Diagnostics);
        var read = Body(diagnostics, "public LdReading Read(");
        var attackersLoop = read.Substring(0, At(read, "_mission.DefenderTeam.ActiveAgents"));
        var nearest = Expression(diagnostics, "public IReadOnlyList<LdPoint> AttackersNearest(");
        var fell = Body(Source(Field), "public int Fell(");
        var left = fell.Substring(At(fell, "var left = _mission.AttackerTeam.ActiveAgents"));
        left = left.Substring(0, left.IndexOf(';'));

        StringAssert.Contains(attackersLoop, "ReferenceEquals(agent, _mission.MainAgent)");
        StringAssert.Contains(nearest, "!ReferenceEquals(a, _mission.MainAgent)");
        StringAssert.Contains(left, "!ReferenceEquals(a, _mission.MainAgent)");
    }

    [TestMethod]
    public void LivingOwner_TakesTheMainAgentOnlyOnTheDefenderTeam()
    {
        var owner = Body(Source(Field), "private Agent? LivingOwner(");

        StringAssert.Contains(owner, "main != null && main.IsActive() && ReferenceEquals(main.Team, _mission.DefenderTeam)");
    }

    [TestMethod]
    public void Classify_TestsThePlayersBodyOnHisOwnTeam_AfterTheNullTeam_BeforeTheSide()
    {
        var classify = Body(Source(Diagnostics), "public LdRemoval Classify(");

        Assert.IsTrue(At(classify, "team == null") < At(classify, "team == PlayerSideTeam && IsPlayerBody(agent)"));
        Assert.IsTrue(At(classify, "team == PlayerSideTeam && IsPlayerBody(agent)") < At(classify, "team.Side == BattleSideEnum.Attacker"), "his body on the attacker team is no wave kill");
        StringAssert.Matches(classify, new Regex(@"return loss \? new LdRemoval\(LdRemovalKind\.Player, id, Key: key\) : new LdRemoval\(LdRemovalKind\.None, """", Key: key\);"));
        StringAssert.Contains(classify, "return new LdRemoval(LdRemovalKind.Defender, id, Key: key);");
        Assert.IsFalse(classify.Contains("? LdRemovalKind.Player : LdRemovalKind.Defender"), "the player test is made once, on his own team");
    }

    [TestMethod]
    public void OrderCharge_HandsThePlayersSideTeamBackToTheAi()
    {
        var charge = Body(Source(Field), "public void OrderCharge(");

        StringAssert.Contains(charge, "PlayerSideTeam.DelegateCommandToAI();");
        Assert.IsFalse(charge.Contains("DefenderTeam.DelegateCommandToAI"), charge);
    }

    [TestMethod]
    public void SpawnHero_OnTheEnemySide_TakesTheClassFormation_AndOnlyHuntersAreHeldAndPosted()
    {
        var heroes = Source(Heroes);
        var spawn = Body(heroes, "public LdHeroReport SpawnHero(");
        var post = Body(heroes, "private void PostHunters(");

        // On a field run (#1105) the heroes of either side take their class formations too: a group held at a post in the open does not fight.
        StringAssert.Contains(heroes, "private bool HeroesHunt => !PlayerAttacks && !IsField;");
        StringAssert.Contains(spawn, "var hunt = HeroesHunt;");
        StringAssert.Contains(spawn, "var cls = hunt ? HeroClass : troop.DefaultFormationClass == FormationClass.Ranged ? FormationClass.Ranged : FormationClass.Infantry;");
        StringAssert.Contains(spawn, "var formation = team.GetFormation(cls);");
        StringAssert.Matches(spawn, new Regex(@"if \(hunt\)\s*MoveHeroStrangersOut\(formation, _heroes\);"));
        StringAssert.Contains(spawn, "Spawn(troop, team, true, cls,");
        var after = spawn.Substring(At(spawn, "HoldHero("));
        Assert.IsTrue(At(after, "if (hunt)") < At(after, "PutHeroesBack(") && At(after, "if (hunt)") < At(after, "Post(formation)"), "an enemy hero is never put back into a held group or posted");
        StringAssert.Contains(spawn, "Enemy: PlayerAttacks, Loose: !hunt && !PlayerAttacks");
        StringAssert.Matches(post, new Regex(@"^private void PostHunters\(\)\s*\{\s*if \(!HeroesHunt\)\s*return;"), "an attack takes no defender formation off AI (#1370)");
    }

    [TestMethod]
    public void OnAField_TheStalemateCharge_NeverReplacesTheDefendersTactics()
    {
        // #1105 slice 2: on a field the defenders run the engine's field tactics and TAOM's doctrine. ClearTacticOptions has no getter to restore them from
        // (TeamAIComponent.cs:139-142), so the charge and its restore leave that list alone; only the attackers are set to charge, as at every wave start.
        var adapter = Source(Field);
        var charge = Body(adapter, "public void OrderCharge(");
        var restore = Body(adapter, "public void RestoreDefenderTactic(");

        Assert.IsTrue(At(charge, "if (IsField)") < At(charge, "SetOnlyTactic(defenders"), "the field branch returns before the defenders' swap");
        StringAssert.Matches(charge, new Regex(@"if \(IsField\)\s*return;"));
        Assert.IsTrue(At(restore, "if (IsField)") < At(restore, "SetOnlyTactic("), "the field branch returns before any tactic is set");
        StringAssert.Matches(restore, new Regex(@"if \(IsField\)\s*return;"));
    }
}
