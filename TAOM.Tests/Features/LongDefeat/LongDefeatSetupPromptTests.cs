using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TAOM.Core.Logging;
using TAOM.Features.LongDefeat;
using TAOM.Features.LongDefeat.Domain;
using TAOM.Tests.Infrastructure;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.LongDefeat;

[TestClass]
[TestCategory("RequiresGame")]
public class LongDefeatSetupPromptTests
{
    private IModLogger _logger = null!;
    private ILongDefeatGameManagerFactory _factory = null!;
    private FakeLongDefeatMenu _menu = null!;

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => GameAssemblies.EnsureLoaded();

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _factory = Substitute.For<ILongDefeatGameManagerFactory>();
        _menu = new FakeLongDefeatMenu();
    }

    private string _dir = null!;

    [TestCleanup]
    public void Cleanup()
    {
        if (_dir != null && Directory.Exists(_dir))
            Directory.Delete(_dir, true);
    }

    private LongDefeatConfigCatalog Catalog(bool withFortress = true) => withFortress
        ? LongDefeatConfigCatalog.FromTexts(Shipped("fortresses.json"), Shipped("waves.json"), Shipped("shop.json"), _logger)
        : LongDefeatConfigCatalog.FromTexts(null, null, null, _logger);

    private static string Shipped(string file) =>
        File.ReadAllText(RepoPaths.RepoPath("Main", "_Module", "ModuleData", "long_defeat", file));

    private LongDefeatBestRunStore Store()
    {
        _dir = Path.Combine(Path.GetTempPath(), "taom-ld-prompt-" + Guid.NewGuid().ToString("N"));
        return new LongDefeatBestRunStore(_dir);
    }

    private LongDefeatSetupPrompt Prompt(bool withFortress = true, LongDefeatBestRunStore? best = null) =>
        new(_menu, _factory, _logger, Catalog(withFortress), new LongDefeatRunRecords(best ?? Store(), null));

    private static LongDefeatSetup Sample() => new("helms", "console", RunOptions.Default with { StopAfterWaves = 2 });

    [TestMethod]
    public void Start_HandsTheSetupToTheFactory_AndSaysNothingWhenItStarts()
    {
        _factory.Start(Arg.Any<LongDefeatSetup>()).Returns(true);

        Assert.IsTrue(Prompt().Start(Sample()));

        _factory.Received(1).Start(Sample());
        Assert.AreEqual(0, _menu.Messages.Count);
    }

    [TestMethod]
    public void Start_WithNoClientFactory_TellsThePlayer()
    {
        _factory.Start(Arg.Any<LongDefeatSetup>()).Returns(false);

        Assert.IsFalse(Prompt().Start(Sample()));

        Assert.AreEqual(1, _menu.Messages.Count);
        StringAssert.Contains(_menu.Messages[0], "{=TAOM_LD_NoClient}");
    }

    [TestMethod]
    public void Start_WhenTheFactoryThrows_ShowsTheFailedNoticeAndDoesNotThrow()
    {
        _factory.Start(Arg.Any<LongDefeatSetup>()).Throws(new InvalidOperationException("boom"));

        Assert.IsFalse(Prompt().Start(Sample()));

        Assert.AreEqual(LongDefeatSetupPrompt.StartFailedMessage, _menu.Messages[0]);
        _logger.Received().LogError(Arg.Is<string>(s => s.Contains("boom")));
    }

    [TestMethod]
    public void Start_WhenEvenTheNoticeThrows_StillReturnsFalse()
    {
        _factory.Start(Arg.Any<LongDefeatSetup>()).Throws(new InvalidOperationException("boom"));
        _menu.NotifyThrows = true;

        Assert.IsFalse(Prompt().Start(Sample()));
    }

    [TestMethod]
    public void ShowStartFailed_ShowsTheNotice_AndSwallowsAMenuThatCannot()
    {
        LongDefeatSetupPrompt.ShowStartFailed(_menu, _logger);
        _menu.NotifyThrows = true;
        LongDefeatSetupPrompt.ShowStartFailed(_menu, _logger);

        Assert.AreEqual(1, _menu.Messages.Count);
        Assert.AreEqual(LongDefeatSetupPrompt.StartFailedMessage, _menu.Messages[0]);
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("start-failed notice")));
    }

    [TestMethod]
    public void Begin_AsksForTheFortress_WithTheShaderSentenceInTheDescription()
    {
        Prompt().Begin();

        var prompt = _menu.Prompts.Single();
        Assert.AreEqual("{=TAOM_LD_MenuOption}The Long Defeat", prompt.Title);
        StringAssert.Contains(prompt.Description, "If a shader error window appears while the fortress loads, press Cancel.");
        CollectionAssert.AreEqual(new[] { "helms", "minas_tirith" }, prompt.Choices.Select(c => c.Id).ToArray(), "every shipped fortress, in the file's order");
        CollectionAssert.AreEqual(Catalog().Fortresses.Select(f => f.Name).ToArray(), prompt.Choices.Select(c => c.Label).ToArray(), "no best run yet: the plain names");
    }

    [TestMethod]
    public void Begin_ABestRunRidesOnItsFortressRow_AsKeyedTextWithVariables()
    {
        var store = Store();
        Assert.IsTrue(store.TrySave("helms", new BestRun(4, 321), out _));

        Prompt(best: store).Begin();

        var choices = _menu.Prompts.Single().Choices;
        var choice = choices.Single(c => c.Id == "helms");
        StringAssert.StartsWith(choice.Label, "{=TAOM_LD_Prompt_ChoiceBest}");
        Assert.AreEqual("4", choice.Variables!["WAVES"]);
        Assert.AreEqual("321", choice.Variables["KILLS"]);
        Assert.AreEqual(Catalog().Fortresses[0].Name, choice.Variables["NAME"]);
        Assert.AreEqual(Catalog().Fortresses[1].Name, choices.Single(c => c.Id == "minas_tirith").Label, "a fortress with no best run keeps its plain name");
    }

    [TestMethod]
    public void Begin_PickingTheFortressAndNormal_StartsAMenuRunWithTheDefaultOptions()
    {
        _menu = new FakeLongDefeatMenu("helms", "normal");
        _factory.Start(Arg.Any<LongDefeatSetup>()).Returns(true);

        Prompt().Begin();

        _factory.Received(1).Start(new LongDefeatSetup("helms", "menu", RunOptions.Default));
    }

    [TestMethod]
    public void Begin_AfterTheFortress_AsksTheDifficulty_NormalThenHard()
    {
        _menu = new FakeLongDefeatMenu("helms");

        Prompt().Begin();

        Assert.AreEqual(2, _menu.Prompts.Count);
        var second = _menu.Prompts[1];
        Assert.AreEqual("{=TAOM_LD_Prompt_DifficultyTitle}Choose the difficulty", second.Title);
        StringAssert.StartsWith(second.Description, "{=TAOM_LD_Prompt_DifficultyDesc}");
        CollectionAssert.AreEqual(new[] { "normal", "hard" }, second.Choices.Select(c => c.Id).ToArray());
        Assert.AreEqual("{=TAOM_LD_Difficulty_Normal}Normal", second.Choices[0].Label);
        Assert.AreEqual("{=TAOM_LD_Difficulty_Hard}Hard", second.Choices[1].Label);
        _factory.DidNotReceive().Start(Arg.Any<LongDefeatSetup>());
    }

    [TestMethod]
    public void Begin_EachDifficultyRowShowsItsOwnBestRun()
    {
        var store = Store();
        Assert.IsTrue(store.TrySave("helms", new BestRun(4, 321), out _));
        Assert.IsTrue(store.TrySave("helms:hard", new BestRun(2, 90), out _));
        _menu = new FakeLongDefeatMenu("helms");

        Prompt(best: store).Begin();

        var rows = _menu.Prompts[1].Choices;
        StringAssert.StartsWith(rows[0].Label, "{=TAOM_LD_Prompt_ChoiceBest}");
        Assert.AreEqual("{=TAOM_LD_Difficulty_Normal}Normal", rows[0].Variables!["NAME"]);
        Assert.AreEqual("4", rows[0].Variables["WAVES"]);
        Assert.AreEqual("321", rows[0].Variables["KILLS"]);
        StringAssert.StartsWith(rows[1].Label, "{=TAOM_LD_Prompt_ChoiceBest}");
        Assert.AreEqual("{=TAOM_LD_Difficulty_Hard}Hard", rows[1].Variables!["NAME"]);
        Assert.AreEqual("2", rows[1].Variables["WAVES"]);
        Assert.AreEqual("90", rows[1].Variables["KILLS"]);
    }

    [TestMethod]
    public void Begin_ADifficultyWithNoBestRunKeepsItsPlainName_AndTheFortressRowShowsOnlyTheNormalBest()
    {
        var store = Store();
        Assert.IsTrue(store.TrySave("helms:hard", new BestRun(2, 90), out _));
        _menu = new FakeLongDefeatMenu("helms");

        Prompt(best: store).Begin();

        Assert.AreEqual(Catalog().Fortresses[0].Name, _menu.Prompts[0].Choices[0].Label, "a hard best does not ride on the fortress row");
        var rows = _menu.Prompts[1].Choices;
        Assert.AreEqual("{=TAOM_LD_Difficulty_Normal}Normal", rows[0].Label);
        StringAssert.StartsWith(rows[1].Label, "{=TAOM_LD_Prompt_ChoiceBest}");
    }

    [TestMethod]
    public void Begin_PickingHard_StartsAMenuRunOnHard()
    {
        _menu = new FakeLongDefeatMenu("helms", "hard");
        _factory.Start(Arg.Any<LongDefeatSetup>()).Returns(true);

        Prompt().Begin();

        _factory.Received(1).Start(new LongDefeatSetup("helms", "menu", RunOptions.Default with { Difficulty = RunDifficulty.Hard }));
    }

    [TestMethod]
    public void Begin_PickingHardThenAClass_StartsAMenuRunWithBoth()
    {
        _menu = new FakeLongDefeatMenu("helms", "hard", "bow");
        _factory.Start(Arg.Any<LongDefeatSetup>()).Returns(true);

        PromptWithClasses().Begin();

        _factory.Received(1).Start(new LongDefeatSetup("helms", "menu", RunOptions.Default with { ClassId = "bow", Difficulty = RunDifficulty.Hard }));
    }

    [TestMethod]
    public void Begin_CancellingTheDifficultyQuestion_StartsNothing()
    {
        _menu = new FakeLongDefeatMenu("helms");

        PromptWithClasses().Begin();

        Assert.AreEqual(2, _menu.Prompts.Count, "no class question follows a cancel");
        _factory.DidNotReceive().Start(Arg.Any<LongDefeatSetup>());
        Assert.AreEqual(0, _menu.Messages.Count);
    }

    [TestMethod]
    public void Begin_WhenTheDifficultyPromptCannotShow_TellsThePlayerAndDoesNotThrow()
    {
        _menu = new FakeLongDefeatMenu("helms");
        var prompt = Prompt();
        _menu.ChooseThrowsAfter = 1;

        prompt.Begin();

        Assert.AreEqual(LongDefeatSetupPrompt.StartFailedMessage, _menu.Messages.Single());
        _logger.Received().LogError(Arg.Is<string>(s => s.Contains("the start flow failed")));
    }

    private const string TwoClasses = "{'classes':[" +
        "{'id':'sword','name':'{=N_sword}Sword','text':'{=T_sword}A sword.','mounted':false,'kits':{'vlandia':{'Item0':'s'}}}," +
        "{'id':'bow','name':'{=N_bow}Bow','text':'{=T_bow}A bow.','mounted':false,'kits':{'vlandia':{'Item0':'b'}}}]}";

    private LongDefeatSetupPrompt PromptWithClasses() =>
        new(_menu, _factory, _logger,
            LongDefeatConfigCatalog.FromTexts(Shipped("fortresses.json"), Shipped("waves.json"), Shipped("shop.json"), _logger, classes: TwoClasses),
            new LongDefeatRunRecords(Store(), null));

    [TestMethod]
    public void Begin_WithClassesOnOffer_AsksTheClassThird_OneRowPerClassWithItsKitText()
    {
        _menu = new FakeLongDefeatMenu("helms", "normal");

        PromptWithClasses().Begin();

        Assert.AreEqual(3, _menu.Prompts.Count);
        var second = _menu.Prompts[2];
        Assert.AreEqual("{=TAOM_LD_Prompt_ClassTitle}Choose your class", second.Title);
        CollectionAssert.AreEqual(new[] { "sword", "bow" }, second.Choices.Select(c => c.Id).ToArray());
        Assert.AreEqual("{=N_sword}Sword", second.Choices[0].Label);
        Assert.AreEqual("{=T_sword}A sword.", second.Choices[0].Hint);
        Assert.IsNull(second.Choices[0].Variables);
        _factory.DidNotReceive().Start(Arg.Any<LongDefeatSetup>());
    }

    [TestMethod]
    public void Begin_PickingTheClass_StartsAMenuRunWithThatClassInTheOptions()
    {
        _menu = new FakeLongDefeatMenu("helms", "normal", "bow");
        _factory.Start(Arg.Any<LongDefeatSetup>()).Returns(true);

        PromptWithClasses().Begin();

        _factory.Received(1).Start(new LongDefeatSetup("helms", "menu", RunOptions.Default with { ClassId = "bow" }));
    }

    [TestMethod]
    public void Begin_WithAStartOfItsOwn_HandsThePickedClassToThatStart()
    {
        _menu = new FakeLongDefeatMenu("helms", "normal", "bow");
        var started = new List<LongDefeatSetup>();

        PromptWithClasses().Begin(setup => { started.Add(setup); return true; });

        Assert.AreEqual(new LongDefeatSetup("helms", "menu", RunOptions.Default with { ClassId = "bow" }), started.Single());
        _factory.DidNotReceive().Start(Arg.Any<LongDefeatSetup>());
    }

    private LongDefeatSetupPrompt PromptWithShippedClasses() =>
        new(_menu, _factory, _logger,
            LongDefeatConfigCatalog.FromTexts(Shipped("fortresses.json"), Shipped("waves.json"), Shipped("shop.json"), _logger, classes: Shipped("classes.json")),
            new LongDefeatRunRecords(Store(), null));

    [TestMethod]
    public void Begin_WithTheShippedClasses_ListsCavalryLast_WithItsKitText()
    {
        _menu = new FakeLongDefeatMenu("helms", "normal");

        PromptWithShippedClasses().Begin();

        var second = _menu.Prompts[2];
        CollectionAssert.AreEqual(new[] { "sword_and_board", "two_handed", "archer", "cavalry" }, second.Choices.Select(c => c.Id).ToArray());
        Assert.AreEqual("{=TAOM_LD_Class_Cavalry}Cavalry", second.Choices[3].Label);
        StringAssert.StartsWith(second.Choices[3].Hint, "{=TAOM_LD_Class_Cavalry_Text}");
    }

    // Slice 11 review (data flow, trace 10): the Cavalry kit overwrites the armour (Head, Body, Cape, Gloves, Leg) and the horse slots, so the class question may not say that the rest of the
    // gear stays as it is.
    [TestMethod]
    public void Begin_TheClassQuestion_SaysCavalrySetsTheArmourAndTheHorse_NotThatTheRestOfTheGearStays()
    {
        _menu = new FakeLongDefeatMenu("helms", "normal");

        PromptWithShippedClasses().Begin();

        var description = _menu.Prompts[2].Description;
        StringAssert.Contains(description, "armour");
        StringAssert.Contains(description, "horse");
        Assert.IsFalse(description.Contains("stays as it is"), description);
    }

    [TestMethod]
    public void Begin_PickingCavalry_StartsAMenuRunWithCavalryInTheOptions()
    {
        _menu = new FakeLongDefeatMenu("helms", "normal", "cavalry");
        _factory.Start(Arg.Any<LongDefeatSetup>()).Returns(true);

        PromptWithShippedClasses().Begin();

        _factory.Received(1).Start(new LongDefeatSetup("helms", "menu", RunOptions.Default with { ClassId = "cavalry" }));
    }

    [TestMethod]
    public void Begin_CancellingTheClassQuestion_StartsNothing()
    {
        _menu = new FakeLongDefeatMenu("helms", "normal");

        PromptWithClasses().Begin();

        Assert.AreEqual(3, _menu.Prompts.Count, "fortress, difficulty, then the class question that was cancelled");
        _factory.DidNotReceive().Start(Arg.Any<LongDefeatSetup>());
        Assert.AreEqual(0, _menu.Messages.Count);
    }

    [TestMethod]
    public void Begin_WhenTheClassPromptCannotShow_TellsThePlayerAndDoesNotThrow()
    {
        _menu = new FakeLongDefeatMenu("helms", "normal");
        var prompt = PromptWithClasses();
        _menu.ChooseThrowsAfter = 2;

        prompt.Begin();

        Assert.AreEqual(LongDefeatSetupPrompt.StartFailedMessage, _menu.Messages.Single());
        _logger.Received().LogError(Arg.Is<string>(s => s.Contains("the start flow failed")));
    }

    [TestMethod]
    public void Begin_Cancelling_StartsNothing()
    {
        Prompt().Begin();

        _factory.DidNotReceive().Start(Arg.Any<LongDefeatSetup>());
        Assert.AreEqual(0, _menu.Messages.Count);
    }

    [TestMethod]
    public void Begin_WithNoFortressInTheConfig_SaysSoAndAsksNothing()
    {
        Prompt(withFortress: false).Begin();

        Assert.AreEqual(0, _menu.Prompts.Count);
        StringAssert.Contains(_menu.Messages.Single(), "{=TAOM_LD_NoContent}");
    }

    [TestMethod]
    public void Begin_WhenThePromptCannotShow_TellsThePlayerAndDoesNotThrow()
    {
        _menu.ChooseThrows = true;

        Prompt().Begin();

        Assert.AreEqual(LongDefeatSetupPrompt.StartFailedMessage, _menu.Messages.Single());
        _logger.Received().LogError(Arg.Is<string>(s => s.Contains("the menu could not show the prompt")));
    }
}
