using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.Library;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.DevConsole.Cheats;
using TAOM.Features.LongDefeat;
using TAOM.Tests.Infrastructure;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.LongDefeat;

/// <summary>
/// <c>taom.ld_start</c>: the body runs over the shipped config and a fake factory, so what it hands the prompt-free start path is
/// what a console player gets. The shape test pins the engine delegate contract (a wrong shape throws in unguarded discovery).
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class LongDefeatCommandsTests
{
    private ILongDefeatGameManagerFactory _factory = null!;
    private LongDefeatConfigCatalog _catalog = null!;

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => GameAssemblies.EnsureLoaded();

    [TestInitialize]
    public void Setup()
    {
        _factory = Substitute.For<ILongDefeatGameManagerFactory>();
        var logger = Substitute.For<IModLogger>();
        _catalog = LongDefeatConfigCatalog.FromTexts(Shipped("fortresses.json"), Shipped("waves.json"), Shipped("shop.json"), logger);
    }

    private static string Shipped(string file) =>
        File.ReadAllText(RepoPaths.RepoPath("Main", "_Module", "ModuleData", "long_defeat", file));

    private string Start(params string[] args)
    {
        var prompt = new LongDefeatSetupPrompt(Substitute.For<ILongDefeatMenuAdapter>(), _factory, Substitute.For<IModLogger>(), _catalog,
            LongDefeatRunRecords.ForFolder(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "taom-ld-unused"), null));
        return LongDefeatCommands.Start(args, _catalog, prompt);
    }

    private LongDefeatSetup Started()
    {
        var call = _factory.ReceivedCalls().Single(c => c.GetMethodInfo().Name == nameof(ILongDefeatGameManagerFactory.Start));
        return (LongDefeatSetup)call.GetArguments()[0]!;
    }

    [TestMethod]
    public void Start_AKnownFortress_StartsItThroughThePromptFreePathAsAConsoleRun()
    {
        _factory.Start(Arg.Any<LongDefeatSetup>()).Returns(true);

        var reply = Start("helms");

        StringAssert.StartsWith(reply, "Starting");
        var setup = Started();
        Assert.AreEqual("helms", setup.FortressId);
        Assert.AreEqual("console", setup.Trigger);
        Assert.IsNull(setup.Options.StopAfterWaves, "no count plays until the run is lost");
        Assert.IsFalse(setup.Options.Autoplay, "a person plays this run");
        Assert.IsNull(setup.RecordsFolder, "a console run writes the player's own best");
    }

    [TestMethod]
    public void Start_TheIdIsMatchedWithoutCase_AndHandedOnAsTheConfigSpellsIt()
    {
        _factory.Start(Arg.Any<LongDefeatSetup>()).Returns(true);

        Start("HELMS");

        Assert.AreEqual("helms", Started().FortressId);
    }

    [TestMethod]
    public void Start_AWaveCount_BecomesTheStopTarget()
    {
        _factory.Start(Arg.Any<LongDefeatSetup>()).Returns(true);

        Start("helms", "3");

        Assert.AreEqual(3, Started().Options.StopAfterWaves);
    }

    [DataTestMethod]
    [DataRow("0")]
    [DataRow("-2")]
    [DataRow("1001")]
    [DataRow("2.5")]
    [DataRow("many")]
    public void Start_ABadWaveCount_IsRefusedAndNothingStarts(string waves)
    {
        var reply = Start("helms", waves);

        Assert.IsFalse(string.IsNullOrWhiteSpace(reply));
        Assert.AreEqual(0, _factory.ReceivedCalls().Count(), "a refused count starts no game");
    }

    [TestMethod]
    public void Start_NoArgument_AnswersTheUsageAndStartsNothing()
    {
        var reply = Start();

        StringAssert.Contains(reply, "taom.ld_start");
        StringAssert.Contains(reply, "helms", "the usage names the fortresses the config holds");
        Assert.AreEqual(0, _factory.ReceivedCalls().Count());
    }

    [TestMethod]
    public void Start_AnUnknownFortress_NamesTheKnownOnesAndStartsNothing()
    {
        var reply = Start("rivendell");

        StringAssert.Contains(reply, "rivendell");
        StringAssert.Contains(reply, "helms");
        Assert.AreEqual(0, _factory.ReceivedCalls().Count());
    }

    [TestMethod]
    public void Start_WithNoValidFortressInTheConfig_SaysSoInsteadOfListingNothing()
    {
        var empty = LongDefeatConfigCatalog.FromTexts(null, null, null, Substitute.For<IModLogger>());
        var prompt = new LongDefeatSetupPrompt(Substitute.For<ILongDefeatMenuAdapter>(), _factory, Substitute.For<IModLogger>(), empty,
            LongDefeatRunRecords.ForFolder(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "taom-ld-unused"), null));

        var reply = LongDefeatCommands.Start(new[] { "helms" }, empty, prompt);

        StringAssert.Contains(reply, "no fortress");
        Assert.AreEqual(0, _factory.ReceivedCalls().Count());
    }

    [TestMethod]
    public void Start_WhenTheStartPathRefuses_SaysItDidNotStart()
    {
        _factory.Start(Arg.Any<LongDefeatSetup>()).Returns(false);

        var reply = Start("helms");

        Assert.IsFalse(reply.StartsWith("Starting"), reply);
        StringAssert.Contains(reply, "could not start");
    }

    [TestMethod]
    public void LdStart_HasTheEngineDelegateShapeAndTheTaomGroup()
    {
        var method = typeof(LongDefeatCheats).GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Single(m => m.GetCustomAttribute<CommandLineFunctionality.CommandLineArgumentFunction>()?.Name == "ld_start");

        Assert.AreEqual(typeof(string), method.ReturnType);
        Assert.AreEqual(typeof(List<string>), method.GetParameters().Single().ParameterType);
        Assert.AreEqual("taom", method.GetCustomAttribute<CommandLineFunctionality.CommandLineArgumentFunction>()!.GroupName);
    }

    [TestMethod]
    public void LdStart_RunsAtTheMainMenuAndLeavesTheSpikeBehind()
    {
        var source = RepoPaths.ReadSource("Main/Features/DevConsole/Cheats/LongDefeatCheats.cs", stripComments: true);

        StringAssert.Contains(source, "TaomConsole.RunAtMainMenu(");
        StringAssert.Contains(source, "LongDefeatCommands.Start(");
        Assert.IsFalse(source.Contains("Spike"), "the Phase 0 command is replaced, not wrapped");
    }
}
