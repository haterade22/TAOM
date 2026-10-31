using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.FordsOfWar;
using TAOM.Features.FordsOfWar.Cheats;
using TAOM.Features.FordsOfWar.Domain;
using TAOM.Tests.Migration;
using static TAOM.Tests.Features.FordsOfWar.Domain.FordsMatchFixture;

namespace TAOM.Tests.Features.FordsOfWar;

/// <summary>
/// The two Fords of War console commands. The shape tests mirror the whole-assembly contract of ConsoleCommandBindingTests for
/// these two commands (a wrongly shaped command throws in the engine's unguarded startup discovery); the logic tests run the
/// command bodies with fakes.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class FordsOfWarCheatsTests
{
    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => GameAssemblies.EnsureLoaded();

    private static MethodInfo Command(string name) =>
        typeof(FordsOfWarCheats).GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Single(m => m.GetCustomAttribute<CommandLineFunctionality.CommandLineArgumentFunction>()?.Name == name);

    [DataTestMethod]
    [DataRow("fords_start")]
    [DataRow("fords_gold")]
    public void Command_HasTheEngineDelegateShapeAndTheTaomGroup(string name)
    {
        var method = Command(name);

        Assert.IsTrue(method.IsStatic);
        Assert.AreEqual(typeof(string), method.ReturnType);
        Assert.AreEqual(1, method.GetParameters().Length);
        Assert.AreEqual(typeof(List<string>), method.GetParameters()[0].ParameterType);
        Assert.AreEqual("taom", method.GetCustomAttribute<CommandLineFunctionality.CommandLineArgumentFunction>()!.GroupName);
        Delegate.CreateDelegate(typeof(Func<List<string>, string>), method);
    }

    [TestMethod]
    public void FordsGold_WithNoGame_ReturnsTheGateMessageAndDoesNotThrow()
    {
        var result = (string)Command("fords_gold").Invoke(null, new object[] { new List<string> { "100" } })!;

        Assert.AreEqual(CampaignCheats.CampaignNotStarted, result);
    }

    [TestMethod]
    public void FordsStart_WithNoGameAndCheatsOff_ReturnsTheGateMessageAndDoesNotThrow()
    {
        var result = (string)Command("fords_start").Invoke(null, new object[] { new List<string>() })!;

        Assert.AreEqual(CampaignCheats.CheatModeDisabled, result);
    }

    [TestMethod]
    public void Start_NoArguments_StartsTheDefaultMatchThroughThePromptFreePath()
    {
        var logger = Substitute.For<IModLogger>();
        var catalog = FordsConfigCatalog.FromTexts(null, FordsConfigFixtures.Factions, FordsConfigFixtures.Maps, logger);
        var factory = Substitute.For<IFordsOfWarGameManagerFactory>();
        factory.Create(Arg.Any<FordsMatchSetup>()).Returns(StubGameManager.Create());
        var menu = new FakeFordsMenu();

        var reply = FordsOfWarCommands.Start(new List<string>(), catalog, new FordsSetupPrompt(catalog, menu, factory, logger));

        StringAssert.Contains(reply, "gondor");
        StringAssert.Contains(reply, "mordor");
        factory.Received(1).Create(new FordsMatchSetup("taom_rohan_battle_001_forceatmo", "gondor", "mordor", "normal", 3));
        Assert.AreEqual(1, menu.Started.Count);
        Assert.AreEqual(0, menu.Prompts.Count, "no prompt is shown");
    }

    [TestMethod]
    public void Start_AllFourArguments_AreUsed()
    {
        var logger = Substitute.For<IModLogger>();
        var catalog = FordsConfigCatalog.FromTexts(null, FordsConfigFixtures.Factions, FordsConfigFixtures.Maps, logger);
        var factory = Substitute.For<IFordsOfWarGameManagerFactory>();
        factory.Create(Arg.Any<FordsMatchSetup>()).Returns(StubGameManager.Create());

        FordsOfWarCommands.Start(new List<string> { "mordor", "gondor", "hard", "taom_rohan_battle_001_forceatmo" }, catalog,
            new FordsSetupPrompt(catalog, new FakeFordsMenu(), factory, logger));

        factory.Received(1).Create(new FordsMatchSetup("taom_rohan_battle_001_forceatmo", "mordor", "gondor", "hard", 3));
    }

    [TestMethod]
    public void Start_UnknownFaction_RepliesWithTheErrorAndStartsNothing()
    {
        var logger = Substitute.For<IModLogger>();
        var catalog = FordsConfigCatalog.FromTexts(null, FordsConfigFixtures.Factions, FordsConfigFixtures.Maps, logger);
        var factory = Substitute.For<IFordsOfWarGameManagerFactory>();

        var reply = FordsOfWarCommands.Start(new List<string> { "narnia" }, catalog,
            new FordsSetupPrompt(catalog, new FakeFordsMenu(), factory, logger));

        StringAssert.Contains(reply, "narnia");
        factory.DidNotReceiveWithAnyArgs().Create(null!);
    }

    [TestMethod]
    public void Start_NoClient_SaysSo()
    {
        var logger = Substitute.For<IModLogger>();
        var catalog = FordsConfigCatalog.FromTexts(null, FordsConfigFixtures.Factions, FordsConfigFixtures.Maps, logger);
        var factory = Substitute.For<IFordsOfWarGameManagerFactory>();
        factory.Create(Arg.Any<FordsMatchSetup>()).Returns((MBGameManager?)null);

        var reply = FordsOfWarCommands.Start(new List<string>(), catalog, new FordsSetupPrompt(catalog, new FakeFordsMenu(), factory, logger));

        StringAssert.Contains(reply, "could not");
    }

    private static FordsSession Filled()
    {
        var session = new FordsSession();
        session.Fill(new object(), Create(), WestSlot, Substitute.For<IFordsBattlefieldAdapter>(), default, default, new List<Vec3f>());
        return session;
    }

    [TestMethod]
    public void Gold_AnAmount_AddsItToTheHumanSlotAndEchoesTheBalance()
    {
        var session = Filled();

        var reply = FordsOfWarCommands.Gold(new List<string> { "500" }, session);

        StringAssert.Contains(reply, "500");
        StringAssert.Contains(reply, "1500");
        Assert.AreEqual(1500, session.GetView()!.Gold);
    }

    [TestMethod]
    public void Gold_NoMatchRunning_SaysSo()
    {
        var reply = FordsOfWarCommands.Gold(new List<string> { "500" }, new FordsSession());

        StringAssert.Contains(reply, "No Fords of War match");
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow("abc")]
    [DataRow("0")]
    [DataRow("-5")]
    [DataRow("2.5")]
    [DataRow("100000")]
    public void Gold_BadAmount_IsAnErrorAndChangesNothing(string amount)
    {
        var session = Filled();
        var args = amount.Length == 0 ? new List<string>() : new List<string> { amount };

        var reply = FordsOfWarCommands.Gold(args, session);

        Assert.IsFalse(string.IsNullOrWhiteSpace(reply));
        Assert.AreEqual(1000, session.GetView()!.Gold);
    }
}
