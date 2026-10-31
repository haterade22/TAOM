using System.IO;
using System.Linq;
using System.Reflection;
using DryIoc;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.StagedSiege;
using TAOM.Features.StagedSiege.Hooks;
using TAOM.Tests.Infrastructure;
using TAOM.Tests.Migration;
using TaleWorlds.MountAndBlade;

namespace TAOM.Tests.Features.StagedSiege;

/// <summary>
/// Wiring guard for the staged siege (#1054). Each piece fails silently when unwired: drop the module line and the feature never
/// arms; drop the category and the two patches never apply; drop the behavior declaration and nothing ticks; give the tactic an
/// IoC lookup and it throws on the AI thread. The spike that proved the engine seams is gone and must stay gone.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class StagedSiegeWiringTests
{
    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => GameAssemblies.EnsureLoaded();

    [TestMethod]
    public void Module_Listed_SpikeGone()
    {
        Assert.AreEqual(1, FeatureModules.All.OfType<StagedSiegeModule>().Count());
        Assert.IsFalse(FeatureModules.All.Any(m => m.GetType().Name.Contains("Spike")), "the Phase 0 spike module is deleted");
        Assert.IsFalse(typeof(StagedSiegeModule).Assembly.GetTypes().Any(t => t.Name.Contains("StagedSiegeSpike")),
            "no spike type may remain in the assembly");
    }

    [TestMethod]
    public void Module_DeclaresPatch126AtGameInit()
    {
        var decls = new StagedSiegeModule().PatchCategories.ToList();

        Assert.AreEqual(1, decls.Count);
        Assert.AreEqual("Patch126_StagedSiege", decls[0].Category);
        Assert.AreEqual(ApplyPhase.GameInit, decls[0].Phase);
    }

    [TestMethod]
    public void Module_DeclaresTheMissionBehavior()
    {
        var module = new StagedSiegeModule();

        Assert.AreEqual("StagedSiege", module.Id);
        Assert.IsNull(module.ParkedReason, "the feature ships behind its MCM switch, not parked");
        Assert.IsFalse(module.OwnsSaveData);
        Assert.AreEqual(1, module.MissionBehaviors.Count);
        Assert.AreEqual(typeof(StagedSiegeMissionBehavior), module.MissionBehaviors[0].BehaviorType);
        Assert.IsFalse(RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true).Contains(nameof(StagedSiegeMissionBehavior)),
            "the module's declaration is what adds the behavior; a hand add would run it twice");
    }

    [TestMethod]
    public void Module_RegistersTheFieldReaderCreatureSiegeRoleAsks()
    {
        var container = new Container();
        new StagedSiegeModule().RegisterServices(container);

        Assert.IsInstanceOfType(container.Resolve<IStagedSiegeFieldReader>(), typeof(StagedSiegeFieldReader));
        Assert.AreSame(container.Resolve<IStagedSiegeFieldReader>(), container.Resolve<IStagedSiegeFieldReader>());
        StringAssert.Contains(RepoPaths.ReadSource("Main/Features/CreatureSiegeRole/CreatureSiegeRoleModule.cs", stripComments: true),
            "resolver.Resolve<IStagedSiegeFieldReader>()");
    }

    [TestMethod]
    public void TheEarlyStartCatch_SaysTheRulesAreOffForTheMission()
    {
        // That catch drops the service and the hall, so no report follows: its log line is the only evidence.
        StringAssert.Contains(RepoPaths.ReadSource("Main/Features/StagedSiege/Hooks/StagedSiegeMissionBehavior.cs", stripComments: true),
            "_edge.Fault(\"EarlyStart\", ex, effect: StagedSiegeMissionEdge.RulesOffForMission)");
    }

    [TestMethod]
    public void TheMissionBehavior_StaysUnder150Lines()
    {
        var lines = File.ReadAllLines(RepoPaths.RepoPath("Main", "Features", "StagedSiege", "Hooks", "StagedSiegeMissionBehavior.cs")).Length;

        Assert.IsTrue(lines < 150, $"StagedSiegeMissionBehavior.cs is {lines} lines (ADR-002: entry points under 150)");
    }

    [TestMethod]
    public void TheHallPath_AddsNoHarmonyPatch()
    {
        foreach (var file in new[]
                 {
                     "Main/Features/StagedSiege/StagedHallService.cs", "Main/Adapters/StagedHallMissionAdapter.cs",
                     "Main/Features/StagedSiege/Domain/HallZone.cs", "Main/Features/StagedSiege/Domain/StagedHallActivation.cs",
                 })
        {
            var source = RepoPaths.ReadSource(file, stripComments: true);
            Assert.IsFalse(source.Contains("Harmony"), file + " must not patch: the hall rides a MissionLogic");
        }
    }

    [TestMethod]
    public void TheHallService_ReadsNoEngineType()
    {
        // ADR-007: the service takes the adapter; the engine's marker, controller and message classes stay behind it.
        var source = RepoPaths.ReadSource("Main/Features/StagedSiege/StagedHallService.cs", stripComments: true);

        foreach (var banned in new[] { "FightAreaMarker", "LordsHallFightMissionController", "InformationManager", "PlayerEncounter", "TaleWorlds" })
            Assert.IsFalse(source.Contains(banned), $"StagedHallService must not name {banned}");
    }

    [TestMethod]
    public void Behavior_IsMissionLogic()
    {
        // The engine adds `behavior as MissionLogic` to its logic list when BehaviorType is Logic; a null in it throws ten seconds
        // into every battle (lessons/gamemodels-services.md).
        Assert.IsTrue(typeof(MissionLogic).IsAssignableFrom(typeof(StagedSiegeMissionBehavior)));
        var logger = Substitute.For<IModLogger>();
        var behavior = new StagedSiegeMissionBehavior(new StagedSiegeSettingsProvider(),
            StagedSiegeSceneCatalog.FromText(null, logger), logger);

        Assert.AreEqual(MissionBehaviorType.Logic, behavior.BehaviorType);
        Assert.IsNull(typeof(StagedSiegeMissionBehavior).GetMethod("OnBehaviorInitialize",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly),
            "OnBehaviorInitialize never reaches a behavior TAOM adds late; the setup is in EarlyStart and AfterStart");
    }

    [TestMethod]
    public void Patch126_HasExactlyTheTwoPatchesInItsCategory()
    {
        var classes = AccessTools.GetTypesFromAssembly(typeof(StagedSiegeModule).Assembly)
            .Where(t => t.GetCustomAttributes<HarmonyPatchCategory>().Any(c => c.info.category == "Patch126_StagedSiege"))
            .Select(t => t.Name)
            .ToList();

        CollectionAssert.AreEquivalent(new[] { nameof(StagedSiegeSpawnSettingsPatch), nameof(StagedSiegeSpawnFramePatch) }, classes);
    }

    [TestMethod]
    public void SpikeFolder_Deleted()
    {
        Assert.IsFalse(Directory.Exists(RepoPaths.RepoPath("Main", "Features", "StagedSiege", "Spike")));
        Assert.IsFalse(File.Exists(RepoPaths.RepoPath("Main", "Features", "DevConsole", "Cheats", "StagedSiegeSpikeCheats.cs")));
    }

    [TestMethod]
    public void Tactic_SourceNamesNoIoCLoggerOrSettings()
    {
        // The tactic runs on the AI thread: only what the engine hands over plus the immutable snapshot (the CultureDoctrine tactic
        // rule). A container lookup, a log call or a switch read there is a thread hazard and a startup-order hazard.
        var source = RepoPaths.ReadSource("Main/Features/StagedSiege/Hooks/TacticStagedObjective.cs", stripComments: true);

        foreach (var banned in new[] { "IoC", "IModLogger", "Logger", "Settings", "Resolve" })
            Assert.IsFalse(source.Contains(banned), $"TacticStagedObjective must not name {banned}");
    }

    [TestMethod]
    public void Tactic_AlwaysCallsTheBaseTick_AndOverridesTheWeightAndBothEdges()
    {
        var source = RepoPaths.ReadSource("Main/Features/StagedSiege/Hooks/TacticStagedObjective.cs", stripComments: true);

        StringAssert.Contains(source, "base.TickOccasionally();");
        foreach (var name in new[] { "GetTacticWeight", "OnApply", "OnCancel" })
            StringAssert.Contains(source, "override " + (name == "GetTacticWeight" ? "float " : "void ") + name);
    }
}
