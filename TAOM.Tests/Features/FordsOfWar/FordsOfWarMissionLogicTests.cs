using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.FordsOfWar;
using TAOM.Features.FordsOfWar.Hooks;
using TAOM.Tests.Infrastructure;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.FordsOfWar;

[TestClass]
[TestCategory("RequiresGame")]
public class FordsOfWarMissionLogicTests
{
    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => GameAssemblies.EnsureLoaded();

    private static FordsOfWarMissionLogic Logic(string scene, IModLogger logger, FordsMissionExit? exit = null) =>
        new(new FordsMatchSetup(scene, "gondor", "mordor", "normal", 3),
            FordsConfigCatalog.FromTexts(null, FordsConfigFixtures.Factions, FordsConfigFixtures.Maps, logger),
            new FordsSession(), logger, exit);

    [TestMethod]
    public void TheLogic_IsAMissionLogic_SoCheckMissionEndedPollsIt()
    {
        Assert.IsTrue(typeof(MissionLogic).IsAssignableFrom(typeof(FordsOfWarMissionLogic)));
        Assert.AreEqual(MissionBehaviorType.Logic, Logic("x", Substitute.For<IModLogger>()).BehaviorType);
    }

    [TestMethod]
    public void TheLogic_DoesNotOverrideOnBehaviorInitialize()
    {
        // A behavior TAOM adds meets OnBehaviorInitialize never (harmony-patches.md, "MissionBehavior lifecycle").
        var method = typeof(FordsOfWarMissionLogic).GetMethod("OnBehaviorInitialize", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        Assert.IsNull(method);
    }

    [TestMethod]
    public void MissionEnded_BeforeAnythingStarted_IsFalse()
    {
        var logic = Logic("taom_rohan_battle_001_forceatmo", Substitute.For<IModLogger>());
        MissionResult? result = null;

        Assert.IsFalse(logic.MissionEnded(ref result));
        Assert.IsNull(result);
    }

    [TestMethod]
    public void Ticks_BeforeAnythingStarted_DoNothingAndDoNotThrow()
    {
        var logic = Logic("taom_rohan_battle_001_forceatmo", Substitute.For<IModLogger>());

        logic.OnMissionTick(0.1f);
        logic.OnAgentRemoved(null!, null!, AgentState.Killed, default);
    }

    [TestMethod]
    public void AfterStart_UnknownScene_LogsAnErrorAndEndsTheMissionThroughMissionEnded()
    {
        var logger = Substitute.For<IModLogger>();
        var logic = Logic("no_such_scene", logger);

        logic.AfterStart();
        MissionResult? result = null;

        logger.Received().LogError(Arg.Is<string>(m => m.Contains("no_such_scene")));
        Assert.IsTrue(logic.MissionEnded(ref result));
        Assert.IsNotNull(result);
    }

    [TestMethod]
    public void OnMissionTick_AfterAFailedStart_EndsTheGameWithNoPanelAttached()
    {
        var menu = Substitute.For<IFordsMenuAdapter>();
        var logger = Substitute.For<IModLogger>();
        var logic = Logic("no_such_scene", logger, new FordsMissionExit(menu, logger));
        logic.AfterStart();

        logic.OnMissionTick(2f);
        menu.DidNotReceive().EndGame();
        logic.OnMissionTick(1.5f);

        menu.Received(1).EndGame();
        menu.Received(1).Notify(FordsSetupPrompt.StartFailedMessage);
    }

    [TestMethod]
    public void OnMissionTick_BeforeAnythingStarted_NeverEndsTheGame()
    {
        var menu = Substitute.For<IFordsMenuAdapter>();
        var logger = Substitute.For<IModLogger>();
        var logic = Logic("taom_rohan_battle_001_forceatmo", logger, new FordsMissionExit(menu, logger));

        logic.OnMissionTick(100f);

        menu.DidNotReceive().EndGame();
    }

    [TestMethod]
    public void TheLogicAndTheAdapters_NeverSpawnThePlayerTroopOrGiveAnAgentThePlayerController()
    {
        // The mission must keep no main agent (build-spec 6.1). Comment lines may name both; code may not.
        foreach (var relative in new[]
                 {
                     "Main/Features/FordsOfWar/Hooks/FordsOfWarMissionLogic.cs", "Main/Features/FordsOfWar/FordsMissionDirector.cs",
                     "Main/Adapters/FordsBattlefieldAdapter.cs", "Main/Adapters/FordsTowerAdapter.cs",
                 })
        {
            var code = System.IO.File.ReadAllLines(RepoPaths.RepoPath(relative.Split('/')))
                .Where(l => !l.TrimStart().StartsWith("//") && !l.TrimStart().StartsWith("///") && !l.TrimStart().StartsWith("*"))
                .ToList();

            Assert.IsFalse(code.Any(l => l.Contains("PlayerTroop")), relative + " names the player troop");
            Assert.IsFalse(code.Any(l => l.Contains("AgentControllerType")), relative + " sets an agent controller");
        }
    }

    [TestMethod]
    public void EveryFordsEntryPoint_StaysUnderOneHundredFiftyLines()
    {
        foreach (var file in new[] { "FordsOfWarMissionLogic.cs", "FordsTowerHitPatch.cs" })
        {
            var path = RepoPaths.RepoPath("Main", "Features", "FordsOfWar", "Hooks", file);
            Assert.IsTrue(System.IO.File.ReadAllLines(path).Length < 150, $"{file} is an entry point (ADR-002)");
        }
    }
}
