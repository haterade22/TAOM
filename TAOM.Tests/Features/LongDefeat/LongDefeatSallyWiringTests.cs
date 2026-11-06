using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.LongDefeat;

/// <summary>
/// The engine-facing lines of the sally that no test can run without a mission (phase2-plan.md Slice 10, spike S9), read off the source: the order of the remount step, the teleport, the
/// mounted spawn, the planned-removal check in <c>Classify</c>, the callbacks the mission logic forwards, and the rule that no code fades a horse. The in-game proof is the <c>sally</c> line and check.
/// </summary>
[TestClass]
public class LongDefeatSallyWiringTests
{
    private const string Player = "Main/Adapters/LongDefeatBattlefieldAdapter.Player.cs";
    private const string Field = "Main/Adapters/LongDefeatBattlefieldAdapter.cs";
    private const string Diagnostics = "Main/Adapters/LongDefeatBattlefieldAdapter.Diagnostics.cs";
    private const string Logic = "Main/Features/LongDefeat/Hooks/LongDefeatMissionLogic.cs";

    private static string Source(string path) => RepoPaths.ReadSource(path, stripComments: true);

    // The text from the marker to the next member of the class at the same indent.
    private static string Body(string source, string marker)
    {
        var at = source.IndexOf(marker, StringComparison.Ordinal);
        Assert.IsTrue(at >= 0, marker + " not found");
        var ends = new[] { "\n    public ", "\n    private ", "\n    internal ", "\n    protected " }
            .Select(m => source.IndexOf(m, at + marker.Length, StringComparison.Ordinal))
            .Where(i => i > 0)
            .DefaultIfEmpty(source.Length);
        return source.Substring(at, ends.Min() - at);
    }

    private static void InOrder(string body, params string[] parts)
    {
        var last = -1;
        foreach (var part in parts)
        {
            var at = body.IndexOf(part, StringComparison.Ordinal);
            Assert.IsTrue(at > last, $"'{part}' is missing or out of order in:\n{body}");
            last = at;
        }
    }

    [TestMethod]
    public void TheRemount_ReadsTheShare_MarksTheOldBodyPlanned_FadesItWithoutItsHorse_ThenSpawnsTheNewOne_ThenWritesTheShare()
    {
        var body = Body(Source(Player), "public LdRemount RemountPlayer(");

        // Agent.FadeOut(hideInstantly, hideMount) hands removal to native (Agent.cs:4304-4310); the horse is never faded.
        InOrder(body, "HealthLimit", "_lordWatch.Plan(", ".FadeOut(true, false)", "SpawnMounted(", ".Health =");
        Assert.IsFalse(body.Contains("FadeOut(true, true)"), "no code fades a horse");
    }

    [TestMethod]
    public void NoLongDefeatCode_FadesAHorse_AndOnlyTheRemountFadesAnything()
    {
        var files = RepoPaths.CSharpFiles("Main")
            .Where(f => f.Replace('\\', '/').Contains("/Features/LongDefeat/") || Path.GetFileName(f).StartsWith("LongDefeat") && f.Replace('\\', '/').Contains("/Main/Adapters/"))
            .ToList();

        var fades = files.SelectMany(f => RepoPaths.StripComments(File.ReadAllText(f).Replace("\r\n", "\n")).Split('\n').Select(l => (File: Path.GetFileName(f), Line: l)))
            .Where(x => x.Line.Contains(".FadeOut("))
            .ToList();

        Assert.AreEqual(1, fades.Count, string.Join("\n", fades.Select(x => x.File + ": " + x.Line.Trim())));
        Assert.AreEqual("LongDefeatBattlefieldAdapter.Player.cs", fades[0].File);
        StringAssert.Contains(fades[0].Line, ".FadeOut(true, false)");
    }

    [TestMethod]
    public void TheRemount_ReadsTheShareThroughTheNaNSafeHelper_AndKeepsNoKitReadBackNobodyReads()
    {
        var body = Body(Source(Player), "public LdRemount RemountPlayer(");

        StringAssert.Contains(body, "LdRemount.ShareOf(");
        Assert.IsFalse(body.Contains("ReadBack("), "the only reader of the kit facts is SpawnLord, right after its own spawn");
    }

    [TestMethod]
    public void TheTeleport_TurnsTheHorseAndTheRider_ThenMovesThem()
    {
        var body = Body(Source(Player), "public bool TeleportPlayer(");

        // Vanilla turns a mounted main agent's horse explicitly, then the rider, then teleports (SandBox MissionConversationLogic.cs:176-182); the camera rewrites a human rider's look every tick, so the
        // horse's heading is the part that lasts. LookDirection has a public setter (Agent.cs:1493-1495); TeleportToPosition moves the mount, then the rider (Agent.cs:4287-4299).
        StringAssert.Contains(body, "HasFacing");
        StringAssert.Contains(body, "MountAgent");
        Assert.AreEqual(2, body.Split(new[] { "LookDirection =" }, StringSplitOptions.None).Length - 1, "the horse's look and the rider's");
        Assert.IsTrue(body.LastIndexOf("LookDirection =", StringComparison.Ordinal) < body.IndexOf("TeleportToPosition(", StringComparison.Ordinal), "both looks are set before the move");
        InOrder(body, "MountAgent", "LookDirection =", "TeleportToPosition(");
    }

    [TestMethod]
    public void TheMountedSpawn_PositionsTheFormationOnlyOnce_AndOpensItsSpawnAsMounted()
    {
        var body = Body(Source(Player), "private Agent SpawnMounted(");

        // Only an unpositioned formation is positioned (Mission.cs:4214-4226; SetPositioning sets HasBeenPositioned, Formation.cs:864-867). A remount spawns into the positioned formation and must not drag its order
        // point to the sally point, where the out line would then show the code's own write as if it were the formation's pull.
        InOrder(body, "HasBeenPositioned", "SetPositioning(");
        // Vanilla opens a formation's spawn with the real mounted flag (MissionBattleSideSpawnContext.cs:360, :371).
        StringAssert.Contains(body, "BeginSpawn(1, true)");
    }

    [TestMethod]
    public void TheMountedSpawn_PositionsTheCavalryFormation_ThenSpawnsMountedWithAPlaceAndDirection()
    {
        var body = Body(Source(Player), "private Agent SpawnMounted(");

        InOrder(body, "GetFormation(FormationClass.Cavalry)", "SetPositioning(", "BeginSpawn(", "= Spawn(player, team, true, FormationClass.Cavalry", "EndSpawn()");
        StringAssert.Contains(body, "mounted: true");
        StringAssert.Contains(body, "place: place");
    }

    [TestMethod]
    public void Spawn_CanTakeAPlaceWithItsDirection_AndAHorseWhenTheClassRides()
    {
        var spawn = Body(Source(Field), "private Agent Spawn(");

        // A position needs a direction: AgentBuildData.InitialDirection throws on a null one (Mission.cs:4498).
        StringAssert.Contains(spawn, "initialPosition");
        StringAssert.Contains(spawn, "initialDirection");
        StringAssert.Contains(spawn, ".NoHorses(!mounted)");
    }

    [TestMethod]
    public void TheSallyPoint_IsJudgedByANavmeshAndTheMissionBoundary()
    {
        var body = Body(Source(Player), "public LdPointFacts SallyPointUsable(");

        StringAssert.Contains(body, "GetNavigationMeshForPosition(");
        StringAssert.Contains(body, "IsPositionInsideBoundaries(");
    }

    [TestMethod]
    public void Classify_ChecksThePlannedRemovalByIdentity_BeforeItDecidesWhatTheBodyWas()
    {
        var body = Body(Source(Diagnostics), "public LdRemoval Classify(");

        InOrder(body, "_lordWatch.IsPlanned(", "_registry.TakeRemoval(", "agent.Team");
    }

    [TestMethod]
    public void TheBoundaryCheck_ReadsTheMainAgent_AsMissionBoundaryCrossingHandlerDoes()
    {
        // MissionBoundaryCrossingHandler.TickForMainAgent tests !Mission.IsPositionInsideBoundaries(Agent.Main.Position.AsVec2) (MissionBoundaryCrossingHandler.cs:210).
        var body = Body(Source(Player), "public LdLordFacts ReadLord(");

        StringAssert.Contains(body, "_mission.MainAgent");
        StringAssert.Contains(body, "IsPositionInsideBoundaries(");
        StringAssert.Contains(body, "AsVec2");
    }

    [TestMethod]
    public void TheLogic_ForwardsTheContactHit_AfterTheBladesHit_AndEveryBodyEventOnlyForAMountedClass()
    {
        var source = Source(Logic);
        var hit = Body(source, "override void OnScoreHit");
        var removed = Body(source, "override void OnAgentRemoved");
        var deleted = Body(source, "override void OnAgentDeleted");

        InOrder(hit, "_host.WantsHits", "_host.OnHit(", "_host.OnContactHit(");
        StringAssert.Contains(hit, "affectedAgent?.Health");
        InOrder(removed, "_host.WatchesBodies", "_host.OnBodyEvent(", "_host.OnAgentRemoved(");
        InOrder(deleted, "_host.WatchesBodies", "_host.OnBodyEvent(");
        StringAssert.Contains(deleted, "\"deleted\"");
    }

    [TestMethod]
    public void TheLord_IsRemembered_WhenHeSpawns()
    {
        var body = Body(Source(Player), "public bool SpawnPlayer(");

        StringAssert.Contains(body, "_lordWatch.SetLord(");
    }
}
