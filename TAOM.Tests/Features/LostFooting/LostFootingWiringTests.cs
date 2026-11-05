using System.Linq;
using System.Text.RegularExpressions;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features;
using TAOM.Features.CoopInterop;
using TAOM.Features.LostFooting;
using TAOM.Features.LostFooting.Hooks;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.LostFooting;

/// <summary>
/// Wiring guards for the Lost Footing census (#894, phase 1). No patch and no model reaches the feature, and
/// nothing in it writes to the battle: the module list adds the mission behavior, the behavior ticks the
/// service, and the adapter is the only code that touches an agent, by reading.
/// </summary>
[TestClass]
public class LostFootingWiringTests
{
    private const string LogicPath = "Main/Features/LostFooting/Hooks/LostFootingMissionLogic.cs";
    private const string ModulePath = "Main/Features/LostFooting/LostFootingModule.cs";
    private const string AdapterPath = "Main/Adapters/LostFootingAdapter.cs";

    [TestMethod]
    public void FeatureModules_ListTheLostFootingModuleOnce()
    {
        Assert.AreEqual(1, FeatureModules.All.OfType<LostFootingModule>().Count(),
            "LostFootingModule must be listed exactly once in Main/Composition/FeatureModules.cs.");
    }

    [TestMethod]
    public void Module_DeclaresTheMissionBehavior_AndNothingElse()
    {
        var module = new LostFootingModule();

        Assert.AreEqual("LostFooting", module.Id);
        Assert.AreEqual(1, module.MissionBehaviors.Count);
        Assert.AreEqual(typeof(LostFootingMissionLogic), module.MissionBehaviors[0].BehaviorType);
        Assert.AreEqual(0, module.CampaignBehaviors.Count);
        Assert.AreEqual(0, module.PatchCategories.Count, "the census ships no Harmony patch");
        Assert.AreEqual(0, module.GameModels.Count);
    }

    [TestMethod]
    public void Module_RegistersEverythingTheBehaviorNeeds()
    {
        var container = new Container();
        container.RegisterInstance(Substitute.For<IModLogger>());
        container.RegisterInstance(Substitute.For<ICoopSessionProvider>());
        new LostFootingModule().RegisterServices(container);

        Assert.IsNotNull(container.Resolve<ILostFootingAdapter>());
        Assert.IsNotNull(container.Resolve<ILostFootingSettingsProvider>());
    }

    [TestMethod]
    public void Module_TheMissionGate_IsTheLostFootingGate_WiredIntoTheClosure()
    {
        var src = RepoPaths.ReadSource(ModulePath, stripComments: true);

        Assert.IsTrue(Regex.IsMatch(src, @"\(\)\s*=>\s*LostFootingMissionGate\.IsEligible\(mission,\s*coop\)"),
            "the census reads its eligibility through LostFootingMissionGate, passed into the service as a closure");
    }

    [TestMethod]
    public void TheGate_IsTheMountDespawnAllowlist_WithTheCoopStandDownAndAnEndedMission()
    {
        // On the 1.5.x line the census shares Fell Foes' gate; Fell Foes is not on the 1.4.x line, so the census
        // owns the same expression here. A change to MountDespawnMissionGate moves the census too.
        var src = RepoPaths.ReadSource("Main/Features/LostFooting/Hooks/LostFootingMissionGate.cs", stripComments: true);

        StringAssert.Contains(src, "coop.IsSessionActive");
        StringAssert.Contains(src, "coop.ShouldDeferToHost");
        StringAssert.Contains(src, "MountDespawnMissionGate.IsEligible(mission)");
        StringAssert.Contains(src, "!mission.MissionEnded");
    }

    [TestMethod]
    public void Behavior_OverridesTheTeardowns_AndFinishesInBoth()
    {
        var src = RepoPaths.ReadSource(LogicPath, stripComments: true);

        StringAssert.Contains(src, "protected override void OnEndMission() => Finish();");
        Assert.IsTrue(Regex.IsMatch(src, @"OnRemoveBehavior\(\)\s*\{\s*Finish\(\);\s*base\.OnRemoveBehavior\(\);"),
            "removal finishes before the base call");
        Assert.IsTrue(Regex.IsMatch(src, @"void Finish\(\)\s*\{\s*try\s*\{\s*_service\.Finish\(\);"),
            "Finish asks the service for its summary");
    }

    [TestMethod]
    public void Behavior_CatchesAFailure_AndStopsTheCensusForTheMission()
    {
        var src = RepoPaths.ReadSource(LogicPath, stripComments: true);

        StringAssert.Contains(src, "catch (Exception ex)");
        StringAssert.Contains(src, "_disabled = true;");
        StringAssert.Contains(src, "if (_disabled) return;");
    }

    [TestMethod]
    public void Behavior_IsALogic_SoVanillaAddsItToTheLogicList()
    {
        Assert.IsTrue(typeof(TaleWorlds.MountAndBlade.MissionLogic).IsAssignableFrom(typeof(LostFootingMissionLogic)));
        var src = RepoPaths.ReadSource(LogicPath, stripComments: true);
        Assert.IsFalse(src.Contains("override MissionBehaviorType BehaviorType"), "MissionLogic already reports Logic");
    }

    [TestMethod]
    public void Behavior_IsAThinEntryPoint_WithNoSubscriptionsAndNoBannedConstructs()
    {
        var src = RepoPaths.ReadSource(LogicPath, stripComments: true);
        var raw = RepoPaths.ReadSource(LogicPath, stripComments: false);

        Assert.IsTrue(raw.Split('\n').Length < 150, "ADR-002: entry points stay under 150 lines");
        Assert.IsFalse(Regex.IsMatch(src, @"\.\w+\s*\+="), "no event subscriptions: every read is in OnMissionTick");
        Assert.IsFalse(src.Contains("OnBehaviorInitialize"), "never fires for a TAOM behavior (#606)");
        Assert.IsFalse(raw.Contains("#region") || raw.Contains("[Obsolete") || raw.Contains("#if DEBUG"));
        Assert.IsFalse(src.Contains(".Index"), "never key on an agent index");
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void EveryMission_GetsItsOwnBehaviorAndItsOwnState()
    {
        var container = new Container();
        container.RegisterInstance(Substitute.For<IModLogger>());
        container.RegisterInstance(Substitute.For<ICoopSessionProvider>());
        new LostFootingModule().RegisterServices(container);
        var decl = new LostFootingModule().MissionBehaviors[0];

        var first = (LostFootingMissionLogic)decl.Create(null!, container);
        var second = (LostFootingMissionLogic)decl.Create(null!, container);

        Assert.AreNotSame(first, second);
        var field = typeof(LostFootingMissionLogic).GetField("_service", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        Assert.AreNotSame(field.GetValue(first), field.GetValue(second), "state is per mission: a shared service would carry one battle into the next");
    }

    [TestMethod]
    public void CoopClassification_TheCensusToggleIsInstrumentation_AndExcluded()
    {
        var mine = typeof(TaomSettings).GetProperties().Where(p => p.Name.StartsWith("LostFooting", System.StringComparison.Ordinal)).ToList();

        Assert.AreEqual(1, mine.Count);
        Assert.AreEqual("LostFootingCensusEnabled", mine[0].Name);
        Assert.IsFalse(CoopSettingsRelevance.IsSimulationRelevant(mine[0]));
        Assert.IsTrue(CoopSettingsRelevance.IsExcluded(mine[0].Name));
    }

    [TestMethod]
    public void Adapter_HasNoWriteCalls_AndNeverCountsThePlayer()
    {
        var src = RepoPaths.ReadSource(AdapterPath, stripComments: true);

        foreach (var write in new[]
                 {
                     "TeleportToPosition", "SetIsAIPaused", "SetScriptedPosition", "SetScriptedPositionAndDirection",
                     "DisableScriptedMovement", "SetAgentFlags", "SetTargetAgent", "SetMorale", "SetMovementDirection", "SetLookDirection",
                     "AddHitPoints", "MakeDead", "Retreat", "SetFormationFrameDisabled", "SetTargetPosition", "ForceAiBehavior",
                 })
            Assert.IsFalse(src.Contains(write), "phase 1 changes nothing in the battle, so the adapter must not call " + write);

        StringAssert.Contains(src, "IsPlayerControlled");
        StringAssert.Contains(src, "GetWorldPosition().GetNavMesh()");
        StringAssert.Contains(src, "IsPositionOnADynamicNavMesh(");
        StringAssert.Contains(src, "FindClosestExitPositionForPositionOnABoundaryFace(");
        Assert.IsFalse(src.Contains(".Index"), "handles are agent objects, never indices");
        Assert.IsFalse(src.Contains("GetCurrentNavigationFaceId"), "face id 0 collides with face group 0");
    }

    [TestMethod]
    public void Adapter_ReadsTheAiMoveDestination_NotTheMovementLockTarget()
    {
        var src = RepoPaths.ReadSource(AdapterPath, stripComments: true);

        StringAssert.Contains(src, "GetAIMoveDestination()");
        Assert.IsFalse(src.Contains("GetTargetPosition"), "that is the movement-lock target, (0,0) for an ordinary soldier");
    }

    [TestMethod]
    public void Adapter_AMovementModeThatIsNotLand_IsTheWaterTest()
    {
        var src = RepoPaths.ReadSource(AdapterPath, stripComments: true);

        StringAssert.Contains(src, "InWaterMode = !agent.IsOnLand()");
        Assert.IsFalse(src.Contains("IsInWater"), "IsInWater implies not Land, so a second test is dead code");
    }

    [TestMethod]
    public void Adapter_AnyBuiltTree_MarksACreature()
    {
        // The 1.5.x line also excludes its Race Abilities tree; that feature is not on the 1.4.x line, so any built
        // tree marks one of TAOM's creatures here (a failed build leaves Tree null).
        var src = RepoPaths.ReadSource(AdapterPath, stripComments: true);

        Assert.IsTrue(Regex.IsMatch(src, @"GetComponent<BehaviorTreeAgentComponent>\(\) is \{ \} (?<t>\w+) && \k<t>\.Tree != null"));
        Assert.IsFalse(src.Contains("RaceAbility"), "Race Abilities is not on the 1.4.x line");
    }

    [TestMethod]
    public void Adapter_TheSnapPairIsAskedOnlyOfAConfirmedAgent()
    {
        var src = RepoPaths.ReadSource(AdapterPath, stripComments: true);

        var guard = src.IndexOf("if (!detail.Confirmed) return detail;", System.StringComparison.Ordinal);
        Assert.IsTrue(guard > 0);
        Assert.IsTrue(guard < src.IndexOf("GetNearestNavMesh()", System.StringComparison.Ordinal),
            "an unconfirmed agent is on the mesh or on a platform, so its snap candidate means nothing");
    }

    [TestMethod]
    public void Adapter_TheCostlyReads_HappenOnlyForASlowOrOffMeshAgent()
    {
        var src = RepoPaths.ReadSource(AdapterPath, stripComments: true);

        var block = Regex.Match(src, @"if \(speed < SlowEnoughToReadMore \|\| probe\.FaceMiss\)\s*\{(?<body>.*?)\n        \}", RegexOptions.Singleline);
        Assert.IsTrue(block.Success, "one block gates the costly reads");
        foreach (var read in new[] { "GetWaterLevelAtPosition(", "GetAIMoveDestination()", "GetOrderPositionOfUnit(" })
        {
            StringAssert.Contains(block.Groups["body"].Value, read);
            Assert.AreEqual(1, src.Split(new[] { read }, System.StringSplitOptions.None).Length - 1, read + " is read nowhere else");
        }
    }

    [TestMethod]
    public void Adapter_ReadsTheUnreachableFlag()
    {
        var src = RepoPaths.ReadSource(AdapterPath, stripComments: true);

        StringAssert.Contains(src, "AgentFlag.UnreachableViaNavMesh");
    }

    [TestMethod]
    public void Services_CarryNoEngineTypes()
    {
        var root = RepoPaths.RepoPath("Main", "Features", "LostFooting");
        var files = System.IO.Directory.GetFiles(root, "*.cs", System.IO.SearchOption.AllDirectories)
            .Where(f => !f.Contains(System.IO.Path.DirectorySeparatorChar + "Hooks" + System.IO.Path.DirectorySeparatorChar)
                        && !f.EndsWith("LostFootingModule.cs", System.StringComparison.Ordinal))
            .Append(RepoPaths.RepoPath("Main", "Adapters", "ILostFootingAdapter.cs"))
            .ToList();

        Assert.IsTrue(files.Count >= 10, "the walk found the service, rules, domain and formatter files");
        foreach (var path in files)
            Assert.IsFalse(RepoPaths.StripComments(System.IO.File.ReadAllText(path)).Contains("using TaleWorlds"),
                path + " is a service: ADR-007 keeps TaleWorlds types out");
    }

    [TestMethod]
    public void Feature_HardCodesNoSceneIdOrCoordinate()
    {
        var files = System.IO.Directory.GetFiles(RepoPaths.RepoPath("Main", "Features", "LostFooting"), "*.cs", System.IO.SearchOption.AllDirectories)
            .Append(RepoPaths.RepoPath("Main", "Adapters", "LostFootingAdapter.cs"));
        foreach (var file in files)
        {
            var src = RepoPaths.StripComments(System.IO.File.ReadAllText(file));
            Assert.IsFalse(Regex.IsMatch(src, "\"(taom_|battle_terrain|scn_)"), file + ": no scene id in the code");
            Assert.IsFalse(Regex.IsMatch(src, @"new Vec[23]\(\s*-?\d"), file + ": no literal coordinate");
        }
    }
}
