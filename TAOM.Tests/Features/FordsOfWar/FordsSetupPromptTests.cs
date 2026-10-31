using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;
using TAOM.Features.FordsOfWar;
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

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => GameAssemblies.EnsureLoaded();

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _factory = Substitute.For<IFordsOfWarGameManagerFactory>();
    }

    private FordsSetupPrompt Prompt(FakeFordsMenu menu, string? maps = null, string? factions = null) =>
        new(FordsConfigCatalog.FromTexts(null, factions ?? FordsConfigFixtures.Factions, maps ?? FordsConfigFixtures.Maps, _logger),
            menu, _factory, _logger);

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

        FordsSetupPrompt.ShowStartFailed(menu, _logger);

        CollectionAssert.AreEqual(new[] { "{=TAOM_FOW_StartFailed}Fords of War could not start. The log has the reason." }, menu.Messages);
    }

    [TestMethod]
    public void ShowStartFailed_WhenTheMenuThrows_LogsAWarningAndDoesNotThrow()
    {
        var menu = Substitute.For<TAOM.Adapters.IFordsMenuAdapter>();
        menu.When(m => m.Notify(Arg.Any<string>())).Do(_ => throw new InvalidOperationException("no inquiry"));

        FordsSetupPrompt.ShowStartFailed(menu, _logger);

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
