using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CoopInterop.Missions;
using TAOM.Features.TrainingGrounds.Waves;
using TAOM.Features.TrainingGrounds.Waves.Hooks;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.TrainingGrounds.Waves;

/// <summary>
/// Wiring guards for the wave fight's mission side (#1529). The decisions live in <c>WaveFightDirector</c> with behaviour tests; these
/// pin only what a fake yard cannot see: which mission logic the module adds, that it is a <c>MissionLogic</c>, that every call into
/// the director runs behind a guard, and that the engine-callback hooks are not used.
/// </summary>
[TestClass]
public class TrainingWavesMissionWiringTests
{
    private const string Folder = "Main/Features/TrainingGrounds/Waves";

    private static string Logic() => RepoPaths.ReadSource(Folder + "/Hooks/TrainingWavesMissionLogic.cs", stripComments: true);

    [TestMethod]
    public void TheModule_AddsTheMissionLogicOnce_AndNothingElseToTheMission()
    {
        var module = new TrainingWavesModule();

        Assert.AreEqual(1, module.MissionBehaviors.Count);
        Assert.AreEqual(typeof(TrainingWavesMissionLogic), module.MissionBehaviors[0].BehaviorType);
    }

    [TestMethod]
    public void TheMissionLogic_IsAMissionLogic_NotAMissionBehaviorThatClaimsToBeOne()
    {
        // A MissionBehavior that returns BehaviorType.Logic by hand puts a null in MissionLogics and throws every tick
        // (docs/reference/engine/mission-and-missionbehavior-lifecycle.md, the ": MissionLogic gotcha").
        StringAssert.Matches(Logic(), new Regex(@"class TrainingWavesMissionLogic\s*:\s*MissionLogic\b"));
        Assert.IsFalse(Logic().Contains("BehaviorType"));
    }

    [TestMethod]
    public void TheMissionLogic_IsInTheCoopRoleTable_AsUnsafe()
    {
        Assert.AreEqual(CoopMissionRole.Unsafe, CoopMissionRoles.Of(typeof(TrainingWavesMissionLogic)));
        Assert.IsFalse(CoopMissionRolePolicy.ShouldAdd(typeof(TrainingWavesMissionLogic), sessionActive: true));
        Assert.IsTrue(CoopMissionRolePolicy.ShouldAdd(typeof(TrainingWavesMissionLogic), sessionActive: false));
    }

    [TestMethod]
    public void TheMissionLogic_TakesTheDirectorOnlyWhereTheYardHasVanillasPracticeController()
    {
        var afterStart = Regex.Match(Logic(), @"public override void AfterStart\(\)\s*\{(?<body>.*?)\n    \}", RegexOptions.Singleline);

        Assert.IsTrue(afterStart.Success);
        var body = afterStart.Groups["body"].Value;
        StringAssert.Contains(body, "GetMissionBehavior<ArenaPracticeFightMissionController>()");
        Assert.IsTrue(body.IndexOf("== null", System.StringComparison.Ordinal) < body.IndexOf("new ArenaWaveArena(", System.StringComparison.Ordinal),
            "the controller is tested for before the yard is built");
        StringAssert.Contains(body, "catch (Exception", "a throw in AfterStart reloads the mission forever");
    }

    [TestMethod]
    public void TheTick_RunsTheDirectorInsideATry_AndSettlesTheRunOnAFault()
    {
        var tick = Regex.Match(Logic(), @"public override void OnMissionTick\(float dt\)\s*\{(?<body>.*?)\n    \}", RegexOptions.Singleline);

        Assert.IsTrue(tick.Success);
        var body = tick.Groups["body"].Value;
        StringAssert.Contains(body, "try");
        StringAssert.Contains(body, "director.Tick(");
        StringAssert.Contains(body, "catch (Exception");
        StringAssert.Contains(Logic(), "director.Fault()");
    }

    [TestMethod]
    public void TheMissionEnd_SettlesTheRun_AndTheRemovalLetsGo()
    {
        var src = Logic();

        Assert.IsTrue(Regex.IsMatch(src, @"OnEndMission\(\)\s*\{[^}]*MissionEnding\(\)", RegexOptions.Singleline));
        Assert.IsTrue(Regex.IsMatch(src, @"OnRemoveBehavior\(\)\s*\{[^}]*Detach\(\)", RegexOptions.Singleline));
    }

    [TestMethod]
    public void TheLeaveRequest_AsksOnlyDuringAFight()
    {
        var request = Regex.Match(Logic(), @"OnEndMissionRequest\(out bool canPlayerLeave\)\s*\{(?<body>.*?)\n    \}", RegexOptions.Singleline);

        Assert.IsTrue(request.Success);
        var body = request.Groups["body"].Value;
        StringAssert.Contains(body, "canPlayerLeave = true");
        StringAssert.Contains(body, "FightInProgress");
        StringAssert.Contains(body, "new InquiryData(");
    }

    [TestMethod]
    public void TheMissionLogic_UsesNoEngineCallbackThatCanRunOffTheMainThread()
    {
        // Counts are polled in the tick; OnAgentRemoved and OnAgentDeleted can run off the main thread
        // (csharp-architecture.md, "Mission-scope agent handles and the engine's threads").
        var src = Logic();

        Assert.IsFalse(src.Contains("OnAgentRemoved"));
        Assert.IsFalse(src.Contains("OnAgentDeleted"));
        Assert.IsFalse(src.Contains("OnAgentBuild"));
    }

    [TestMethod]
    public void TheModule_BuildsTheLogicInTheDeclaration_AndRegistersNoSecondCopyInTheContainer()
    {
        var module = RepoPaths.ReadSource(Folder + "/TrainingWavesModule.cs", stripComments: true);

        Assert.IsTrue(module.Contains("MissionBehaviorDecl.Of("));
        Assert.IsTrue(module.Contains("new TrainingWavesMissionLogic("));
        Assert.IsFalse(RepoPaths.ReadSource(Folder + "/TrainingWavesIoC.cs", stripComments: true).Contains("TrainingWavesMissionLogic"));
    }

    [TestMethod]
    public void TheControllerAccess_NamesExactlyTheFourMembersOfTheReflectionCatalogue()
    {
        var src = RepoPaths.ReadSource("Main/Adapters/ArenaPracticeControllerAccess.cs", stripComments: true);
        var members = Regex.Matches(src, @"AccessTools\.(?:Field|Method)\(\s*typeof\(ArenaPracticeFightMissionController\),\s*""(?<name>[A-Za-z_]+)""")
            .Cast<Match>().Select(m => m.Groups["name"].Value).OrderBy(n => n, System.StringComparer.Ordinal).ToArray();

        CollectionAssert.AreEqual(new[] { "SpawnPlayerNearTournamentMaster", "StartPractice", "_participantAgents", "_spawnedOpponentAgentCount" }, members);
        var catalogue = RepoPaths.ReadSource("docs/reference/taleworlds-api-snapshot/reflection-sites.md");
        foreach (var name in members)
            StringAssert.Contains(catalogue, "`" + name + "`", name + " has no Category B row");
    }
}
