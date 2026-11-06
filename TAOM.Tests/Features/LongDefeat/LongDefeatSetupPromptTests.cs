using System;
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
        Assert.AreEqual("helms", prompt.Choices.Single().Id);
        Assert.AreEqual(Catalog().Fortresses[0].Name, prompt.Choices[0].Label, "no best run yet: the plain name");
    }

    [TestMethod]
    public void Begin_ABestRunRidesOnItsFortressRow_AsKeyedTextWithVariables()
    {
        var store = Store();
        Assert.IsTrue(store.TrySave("helms", new BestRun(4, 321), out _));

        Prompt(best: store).Begin();

        var choice = _menu.Prompts.Single().Choices.Single();
        StringAssert.StartsWith(choice.Label, "{=TAOM_LD_Prompt_ChoiceBest}");
        Assert.AreEqual("4", choice.Variables!["WAVES"]);
        Assert.AreEqual("321", choice.Variables["KILLS"]);
        Assert.AreEqual(Catalog().Fortresses[0].Name, choice.Variables["NAME"]);
    }

    [TestMethod]
    public void Begin_PickingTheFortress_StartsAMenuRunWithTheDefaultOptions()
    {
        _menu = new FakeLongDefeatMenu("helms");
        _factory.Start(Arg.Any<LongDefeatSetup>()).Returns(true);

        Prompt().Begin();

        _factory.Received(1).Start(new LongDefeatSetup("helms", "menu", RunOptions.Default));
    }

    private const string TwoClasses = "{'classes':[" +
        "{'id':'sword','name':'{=N_sword}Sword','text':'{=T_sword}A sword.','mounted':false,'kits':{'vlandia':{'Item0':'s'}}}," +
        "{'id':'bow','name':'{=N_bow}Bow','text':'{=T_bow}A bow.','mounted':false,'kits':{'vlandia':{'Item0':'b'}}}]}";

    private LongDefeatSetupPrompt PromptWithClasses() =>
        new(_menu, _factory, _logger,
            LongDefeatConfigCatalog.FromTexts(Shipped("fortresses.json"), Shipped("waves.json"), Shipped("shop.json"), _logger, classes: TwoClasses),
            new LongDefeatRunRecords(Store(), null));

    [TestMethod]
    public void Begin_WithClassesOnOffer_AsksTheClassSecond_OneRowPerClassWithItsKitText()
    {
        _menu = new FakeLongDefeatMenu("helms");

        PromptWithClasses().Begin();

        Assert.AreEqual(2, _menu.Prompts.Count);
        var second = _menu.Prompts[1];
        Assert.AreEqual("{=TAOM_LD_Prompt_ClassTitle}Choose your class", second.Title);
        CollectionAssert.AreEqual(new[] { "sword", "bow" }, second.Choices.Select(c => c.Id).ToArray());
        StringAssert.StartsWith(second.Choices[0].Label, "{=TAOM_LD_Prompt_ChoiceClass}");
        Assert.AreEqual("{=N_sword}Sword", second.Choices[0].Variables!["NAME"]);
        Assert.AreEqual("{=T_sword}A sword.", second.Choices[0].Variables["TEXT"]);
        _factory.DidNotReceive().Start(Arg.Any<LongDefeatSetup>());
    }

    [TestMethod]
    public void Begin_PickingTheClass_StartsAMenuRunWithThatClassInTheOptions()
    {
        _menu = new FakeLongDefeatMenu("helms", "bow");
        _factory.Start(Arg.Any<LongDefeatSetup>()).Returns(true);

        PromptWithClasses().Begin();

        _factory.Received(1).Start(new LongDefeatSetup("helms", "menu", RunOptions.Default with { ClassId = "bow" }));
    }

    private LongDefeatSetupPrompt PromptWithShippedClasses() =>
        new(_menu, _factory, _logger,
            LongDefeatConfigCatalog.FromTexts(Shipped("fortresses.json"), Shipped("waves.json"), Shipped("shop.json"), _logger, classes: Shipped("classes.json")),
            new LongDefeatRunRecords(Store(), null));

    [TestMethod]
    public void Begin_WithTheShippedClasses_ListsCavalryLast_WithItsKitText()
    {
        _menu = new FakeLongDefeatMenu("helms");

        PromptWithShippedClasses().Begin();

        var second = _menu.Prompts[1];
        CollectionAssert.AreEqual(new[] { "sword_and_board", "two_handed", "archer", "cavalry" }, second.Choices.Select(c => c.Id).ToArray());
        Assert.AreEqual("{=TAOM_LD_Class_Cavalry}Cavalry", second.Choices[3].Variables!["NAME"]);
        StringAssert.StartsWith(second.Choices[3].Variables["TEXT"], "{=TAOM_LD_Class_Cavalry_Text}");
    }

    // Slice 11 review (data flow, trace 10): the Cavalry kit overwrites the armour (Head, Body, Cape, Gloves, Leg) and the horse slots, so the class question may not say that the rest of the
    // gear stays as it is.
    [TestMethod]
    public void Begin_TheClassQuestion_SaysCavalrySetsTheArmourAndTheHorse_NotThatTheRestOfTheGearStays()
    {
        _menu = new FakeLongDefeatMenu("helms");

        PromptWithShippedClasses().Begin();

        var description = _menu.Prompts[1].Description;
        StringAssert.Contains(description, "armour");
        StringAssert.Contains(description, "horse");
        Assert.IsFalse(description.Contains("stays as it is"), description);
    }

    [TestMethod]
    public void Begin_PickingCavalry_StartsAMenuRunWithCavalryInTheOptions()
    {
        _menu = new FakeLongDefeatMenu("helms", "cavalry");
        _factory.Start(Arg.Any<LongDefeatSetup>()).Returns(true);

        PromptWithShippedClasses().Begin();

        _factory.Received(1).Start(new LongDefeatSetup("helms", "menu", RunOptions.Default with { ClassId = "cavalry" }));
    }

    [TestMethod]
    public void Begin_CancellingTheClassQuestion_StartsNothing()
    {
        _menu = new FakeLongDefeatMenu("helms");

        PromptWithClasses().Begin();

        _factory.DidNotReceive().Start(Arg.Any<LongDefeatSetup>());
        Assert.AreEqual(0, _menu.Messages.Count);
    }

    [TestMethod]
    public void Begin_WhenTheClassPromptCannotShow_TellsThePlayerAndDoesNotThrow()
    {
        _menu = new FakeLongDefeatMenu("helms");
        var prompt = PromptWithClasses();
        _menu.ChooseThrowsAfter = 1;

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
