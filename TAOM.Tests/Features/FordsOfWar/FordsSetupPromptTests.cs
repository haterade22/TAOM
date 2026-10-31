using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.MountAndBlade;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.FordsOfWar;
using TAOM.Features.FordsOfWar.Diagnostics;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.FordsOfWar;

[TestClass]
[TestCategory("RequiresGame")]
public class FordsSetupPromptTests
{
    private const string TwoMaps = @"{
  'scenes': [
    { 'scene': 'scene_a', 'name': '{=aom_a}Scene A', 'layout': 'generated' },
    { 'scene': 'scene_b', 'name': '{=aom_b}Scene B', 'layout': 'generated' }
  ]
}";

    private IModLogger _logger = null!;
    private IFordsOfWarGameManagerFactory _factory = null!;
    private FordsRunReport _report = null!;

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => GameAssemblies.EnsureLoaded();

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _factory = Substitute.For<IFordsOfWarGameManagerFactory>();
        _report = new FordsRunReport(Substitute.For<IModLogger>()); // its own logger: these tests count the prompt's own log lines
    }

    private FordsSetupPrompt Prompt(IFordsMenuAdapter menu, string? maps = null, string? factions = null) =>
        new(FordsConfigCatalog.FromTexts(null, factions ?? FordsConfigFixtures.Factions, maps ?? FordsConfigFixtures.Maps, _logger),
            menu, _factory, _logger, _report);

    [TestMethod]
    public void Begin_OneMap_AsksYourFactionEnemyFactionThenDifficultyAndStarts()
    {
        var menu = new FakeFordsMenu("gondor", "mordor", "hard");
        var manager = StubGameManager.Create();
        _factory.Create(Arg.Any<FordsMatchSetup>()).Returns(manager);

        Prompt(menu).Begin();

        Assert.AreEqual(3, menu.Prompts.Count);
        StringAssert.Contains(menu.Prompts[0].Title, "TAOM_FOW_Prompt_YourFaction");
        StringAssert.Contains(menu.Prompts[1].Title, "TAOM_FOW_Prompt_EnemyFaction");
        StringAssert.Contains(menu.Prompts[2].Title, "TAOM_FOW_Prompt_Difficulty");
        _factory.Received(1).Create(new FordsMatchSetup("taom_rohan_battle_001_forceatmo", "gondor", "mordor", "hard", 3));
        Assert.AreEqual(1, menu.Started.Count);
        Assert.AreSame(manager, menu.Started[0]);
        Assert.AreEqual(0, menu.Messages.Count);
    }

    [TestMethod]
    public void Begin_MoreThanOneMap_AsksForTheMapLast()
    {
        var menu = new FakeFordsMenu("gondor", "mordor", "normal", "scene_b");
        _factory.Create(Arg.Any<FordsMatchSetup>()).Returns(StubGameManager.Create());

        Prompt(menu, TwoMaps).Begin();

        Assert.AreEqual(4, menu.Prompts.Count);
        StringAssert.Contains(menu.Prompts[3].Title, "TAOM_FOW_Prompt_Map");
        CollectionAssert.AreEqual(new[] { "scene_a", "scene_b" }, menu.Prompts[3].Choices.Select(c => c.Id).ToArray());
        _factory.Received(1).Create(Arg.Is<FordsMatchSetup>(s => s.SceneName == "scene_b"));
    }

    [TestMethod]
    public void Begin_EveryPromptTextIsALocalizationKey()
    {
        var menu = new FakeFordsMenu("gondor", "mordor", "normal", "scene_a");
        _factory.Create(Arg.Any<FordsMatchSetup>()).Returns(StubGameManager.Create());

        Prompt(menu, TwoMaps).Begin();

        foreach (var prompt in menu.Prompts)
        {
            StringAssert.StartsWith(prompt.Title, "{=TAOM_FOW_");
            StringAssert.StartsWith(prompt.Description, "{=TAOM_FOW_");
        }
    }

    [TestMethod]
    public void Begin_TheEnemyPrompt_OffersEveryFaction()
    {
        var menu = new FakeFordsMenu("gondor");

        Prompt(menu).Begin();

        CollectionAssert.AreEqual(new[] { "gondor", "mordor" }, menu.Prompts[1].Choices.Select(c => c.Id).ToArray());
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    public void Begin_CancelAtAnyStep_StartsNothing(int answersGiven)
    {
        var answers = new[] { "gondor", "mordor", "normal" }.Take(answersGiven).ToArray();
        var menu = new FakeFordsMenu(answers);

        Prompt(menu, TwoMaps).Begin();

        _factory.DidNotReceiveWithAnyArgs().Create(null!);
        Assert.AreEqual(0, menu.Started.Count);
    }

    [TestMethod]
    public void Begin_NoFactions_ShowsAMessageAndPromptsNothing()
    {
        var menu = new FakeFordsMenu();

        Prompt(menu, factions: "{ }").Begin();

        Assert.AreEqual(0, menu.Prompts.Count);
        Assert.AreEqual(1, menu.Messages.Count);
        StringAssert.StartsWith(menu.Messages[0], "{=TAOM_FOW_");
    }

    [TestMethod]
    public void ShowStartFailed_ShowsTheStartFailedNotice()
    {
        var menu = new FakeFordsMenu();

        FordsSetupPrompt.ShowStartFailed(menu, _logger, _report);

        CollectionAssert.AreEqual(new[] { "{=TAOM_FOW_StartFailed}Fords of War could not start. The log has the reason." }, menu.Messages);
    }

    [TestMethod]
    public void ShowStartFailed_WhenTheMenuThrows_LogsAWarningAndDoesNotThrow()
    {
        var menu = Substitute.For<TAOM.Adapters.IFordsMenuAdapter>();
        menu.When(m => m.Notify(Arg.Any<string>(), Arg.Any<Action>())).Do(_ => throw new InvalidOperationException("no inquiry"));

        FordsSetupPrompt.ShowStartFailed(menu, _logger, _report);

        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("no inquiry")));
    }

    [TestMethod]
    public void Start_NoClientFactory_LogsOneWarningAndShowsAMessage()
    {
        var menu = new FakeFordsMenu();
        _factory.Create(Arg.Any<FordsMatchSetup>()).Returns((MBGameManager?)null);

        var started = Prompt(menu).Start(new FordsMatchSetup("s", "gondor", "mordor", "normal", 3));

        Assert.IsFalse(started);
        Assert.AreEqual(0, menu.Started.Count);
        _logger.Received(1).LogWarning(Arg.Any<string>());
        Assert.AreEqual(1, menu.Messages.Count);
        StringAssert.StartsWith(menu.Messages[0], "{=TAOM_FOW_");
    }

    [TestMethod]
    public void Start_FactoryThrows_NeverReachesTheMenuHook()
    {
        var menu = new FakeFordsMenu();
        _factory.Create(Arg.Any<FordsMatchSetup>()).Returns(_ => throw new InvalidOperationException("boom"));

        var started = Prompt(menu).Start(new FordsMatchSetup("s", "gondor", "mordor", "normal", 3));

        Assert.IsFalse(started);
        _logger.Received(1).LogError(Arg.Is<string>(m => m.Contains("boom")));
        Assert.AreEqual(1, menu.Messages.Count);
    }

    [TestMethod]
    public void Begin_RecordsTheClickAndEachShownStep()
    {
        var menu = new FakeFordsMenu("gondor", "mordor");

        Prompt(menu).Begin();

        var menuEvidence = _report.Session.Menu;
        Assert.AreEqual(1, menuEvidence.Clicks);
        CollectionAssert.AreEqual(new[] { "faction", "enemy", "difficulty" }, menuEvidence.Flows[0].Shown);
        CollectionAssert.AreEqual(new[] { "faction", "enemy" }, menuEvidence.Flows[0].Answered);
    }

    [TestMethod]
    public void Cancel_RecordsTheCancelledStep_AndBeginsNoRun()
    {
        var menu = new FakeFordsMenu();
        Prompt(menu).Begin();

        menu.Cancel();

        Assert.AreEqual("cancel", _report.Session.Menu.Flows[0].Ended);
        Assert.AreEqual(0, _report.Session.RunsBegun);
        Assert.IsNull(_report.Run);
    }

    [TestMethod]
    public void FullFlow_BeginsARunWithTheMenuTrigger()
    {
        var menu = new FakeFordsMenu("gondor", "mordor", "hard");
        _factory.Create(Arg.Any<FordsMatchSetup>()).Returns(StubGameManager.Create());

        Prompt(menu).Begin();

        Assert.AreEqual("menu", _report.Run!.Trigger);
        Assert.AreEqual("start", _report.Session.Menu.Flows[0].Ended);
        Assert.IsTrue(_report.Run.Stages.Any(s => s.Name == "startNewGame"));
    }

    [TestMethod]
    public void ConsoleStart_BeginsARunWithTheConsoleTrigger()
    {
        _factory.Create(Arg.Any<FordsMatchSetup>()).Returns(StubGameManager.Create());

        Prompt(new FakeFordsMenu()).Start(new FordsMatchSetup("s", "gondor", "mordor", "normal", 3), "console");

        Assert.AreEqual("console", _report.Run!.Trigger);
    }

    [TestMethod]
    public void NullFactory_RecordsRouteC()
    {
        _factory.Create(Arg.Any<FordsMatchSetup>()).Returns((MBGameManager?)null);

        Prompt(new FakeFordsMenu()).Start(new FordsMatchSetup("s", "gondor", "mordor", "normal", 3));

        Assert.AreEqual("C", _report.Run!.Failure!.Route);
    }

    [TestMethod]
    public void MenuFlow_NullFactory_FailsTheStartedPartOfCheck3()
    {
        var menu = new FakeFordsMenu("gondor", "mordor", "hard");
        _factory.Create(Arg.Any<FordsMatchSetup>()).Returns((MBGameManager?)null);

        Prompt(menu).Begin();

        var part = FordsVerdicts.Judge(_report.Session, _report.Run!)[2].Parts.Single(p => p.Name == "started");
        Assert.AreEqual(FordsResult.Fail, part.Result);
        StringAssert.Contains(part.Numbers, "route=C");
    }

    [TestMethod]
    public void MenuFlow_LauncherFailure_FailsTheStartedPartOfCheck3()
    {
        var menu = new FakeFordsMenu("gondor", "mordor", "hard");
        _factory.Create(Arg.Any<FordsMatchSetup>()).Returns(StubGameManager.Create());
        Prompt(menu).Begin();

        _report.Failure("B", "the launcher opened no mission");

        var part = FordsVerdicts.Judge(_report.Session, _report.Run!)[2].Parts.Single(p => p.Name == "started");
        Assert.AreEqual(FordsResult.Fail, part.Result);
        StringAssert.Contains(part.Numbers, "route=B");
        StringAssert.Contains(part.Numbers, "the launcher opened no mission");
    }

    [TestMethod]
    public void Fault_RecordsRouteC()
    {
        _factory.Create(Arg.Any<FordsMatchSetup>()).Returns(_ => throw new InvalidOperationException("boom"));

        Prompt(new FakeFordsMenu()).Start(new FordsMatchSetup("s", "gondor", "mordor", "normal", 3));

        Assert.AreEqual("C", _report.Run!.Failure!.Route);
        StringAssert.Contains(_report.Run.Failure.Reason, "boom");
    }

    [TestMethod]
    public void Fault_AfterAnEarlierClosedRun_MarksTheFlowFault_AndLeavesTheEarlierRunAlone()
    {
        _report.BeginRun("menu", new FordsMatchSetup("s", "gondor", "mordor", "normal", 3));
        _report.MissionClosed();
        var earlier = _report.Run!;
        var menu = Substitute.For<IFordsMenuAdapter>();
        menu.When(m => m.Choose(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<System.Collections.Generic.IReadOnlyList<FordsChoice>>(),
            Arg.Any<Action<string>>(), Arg.Any<Action?>())).Do(_ => throw new InvalidOperationException("boom"));

        Prompt(menu).Begin();

        Assert.IsNull(earlier.Failure);
        Assert.AreEqual(0, earlier.FinalPasses);
        Assert.AreEqual("fault", _report.Session.Menu.Flows.Last().Ended);
    }

    [TestMethod]
    public void ShowStartFailed_RecordsTheRequest_AndPassesTheAck()
    {
        _report.BeginRun("console", new FordsMatchSetup("s", "gondor", "mordor", "normal", 3));
        var menu = new FakeFordsMenu();

        FordsSetupPrompt.ShowStartFailed(menu, _logger, _report);

        Assert.IsTrue(_report.Run!.End.NoticeRequested);
        Assert.AreEqual(true, _report.Run.End.NoticeOk);
        menu.Acknowledge();
        Assert.IsNotNull(_report.Run.End.NoticeAckWall);
    }

    [TestMethod]
    public void Start_FactoryBuildsAManager_StartsItAndReportsTrue()
    {
        var menu = new FakeFordsMenu();
        var manager = StubGameManager.Create();
        _factory.Create(Arg.Any<FordsMatchSetup>()).Returns(manager);

        var started = Prompt(menu).Start(new FordsMatchSetup("s", "gondor", "mordor", "normal", 3));

        Assert.IsTrue(started);
        Assert.AreSame(manager, menu.Started.Single());
    }
}
