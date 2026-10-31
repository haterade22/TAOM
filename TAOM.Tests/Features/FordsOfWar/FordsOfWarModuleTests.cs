using System;
using System.Linq;
using System.Reflection;
using DryIoc;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.FordsOfWar;
using TAOM.Features.FordsOfWar.Hooks;
using TAOM.Tests.Infrastructure;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.FordsOfWar;

/// <summary>
/// Wiring guard for Fords of War. Each piece fails silently when unwired: drop the module line and the menu entry never
/// appears; drop the category and the tower damage rules never apply; drop the null factory and the menu option throws on a
/// machine without the client.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class FordsOfWarModuleTests
{
    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => GameAssemblies.EnsureLoaded();

    private static Container Registered()
    {
        var container = new Container();
        container.RegisterInstance(Substitute.For<IPathService>());
        container.RegisterInstance(Substitute.For<IModLogger>());
        new FordsOfWarModule().RegisterServices(container);
        return container;
    }

    [TestMethod]
    public void Module_ListedOnceInTheFeatureModules()
    {
        Assert.AreEqual(1, FeatureModules.All.OfType<FordsOfWarModule>().Count());
    }

    [TestMethod]
    public void Module_IsEnabledAndOwnsNoSaveData()
    {
        var module = new FordsOfWarModule();

        Assert.AreEqual("FordsOfWar", module.Id);
        Assert.IsNull(module.ParkedReason);
        Assert.IsFalse(module.OwnsSaveData);
    }

    [TestMethod]
    public void Module_DeclaresPatch131AtFirstMission()
    {
        var decls = new FordsOfWarModule().PatchCategories.ToList();

        Assert.AreEqual(1, decls.Count);
        Assert.AreEqual("Patch131_FordsOfWar", decls[0].Category);
        Assert.AreEqual(ApplyPhase.FirstMission, decls[0].Phase);
    }

    [TestMethod]
    public void TheTowerHitPatch_NamesTheModulesCategory()
    {
        var category = typeof(FordsTowerHitPatch).GetCustomAttribute<HarmonyPatchCategory>();

        Assert.AreEqual(FordsOfWarModule.PatchCategory, category!.info.category);
    }

    [TestMethod]
    public void Module_AddsNoMissionBehaviorCampaignBehaviorOrModel()
    {
        // The mission logic is added only by the launcher; a declaration here would put it in every mission.
        var module = new FordsOfWarModule();

        Assert.AreEqual(0, module.MissionBehaviors.Count);
        Assert.AreEqual(0, module.CampaignBehaviors.Count);
        Assert.AreEqual(0, module.GameModels.Count);
    }

    [TestMethod]
    public void RegisterServices_EveryFordsServiceResolvesAsASingleton()
    {
        var container = Registered();

        AssertSingleton<FordsConfigCatalog>(container);
        AssertSingleton<FordsSession>(container);
        AssertSingleton<FordsSetupPrompt>(container);
        AssertSingleton<FordsMenuEntry>(container);
        AssertSingleton<FordsMissionLauncher>(container);
        AssertSingleton<IFordsMenuAdapter>(container);
        AssertSingleton<IFordsOfWarGameManagerFactory>(container);
    }

    [TestMethod]
    public void RegisterServices_TheDefaultFactoryIsTheNullOne_AndAClientRegistrationReplacesIt()
    {
        var container = Registered();

        Assert.IsInstanceOfType(container.Resolve<IFordsOfWarGameManagerFactory>(), typeof(NullFordsOfWarGameManagerFactory));

        var client = Substitute.For<IFordsOfWarGameManagerFactory>();
        container.RegisterInstance(client, ifAlreadyRegistered: IfAlreadyRegistered.Replace);
        Assert.AreSame(client, container.Resolve<IFordsOfWarGameManagerFactory>());
    }

    [TestMethod]
    public void OnPhase_MainMenu_AddsTheMenuOptionAndRepeatingIsSafe()
    {
        var container = Registered();
        var menu = new FakeFordsMenu();
        container.RegisterInstance<IFordsMenuAdapter>(menu, ifAlreadyRegistered: IfAlreadyRegistered.Replace);
        var module = new FordsOfWarModule();

        module.OnPhase(ApplyPhase.MainMenu, container);
        module.OnPhase(ApplyPhase.MainMenu, container);

        Assert.AreEqual(1, menu.AddCalls);
        Assert.IsTrue(menu.HasOption("TaomFordsOfWar"));
    }

    [TestMethod]
    public void OnPhase_AnyOtherPhase_AddsNothing()
    {
        var container = Registered();
        var menu = new FakeFordsMenu();
        container.RegisterInstance<IFordsMenuAdapter>(menu, ifAlreadyRegistered: IfAlreadyRegistered.Replace);
        var module = new FordsOfWarModule();

        foreach (var phase in new[] { ApplyPhase.ProcessLoad, ApplyPhase.GameInit, ApplyPhase.FirstMission })
            module.OnPhase(phase, container);

        Assert.AreEqual(0, menu.AddCalls);
    }

    [TestMethod]
    public void OnPhase_MainMenu_AMenuThatThrowsNeverReachesTheHook()
    {
        var container = Registered();
        var menu = Substitute.For<IFordsMenuAdapter>();
        menu.HasOption(NSubstitute.Arg.Any<string>()).Returns(_ => throw new InvalidOperationException("no module"));
        container.RegisterInstance(menu, ifAlreadyRegistered: IfAlreadyRegistered.Replace);
        MainMenuStep.ResetForTests();

        new FordsOfWarModule().OnPhase(ApplyPhase.MainMenu, container);
    }

    [TestMethod]
    public void TheEntryPoints_StayUnder150Lines()
    {
        foreach (var file in new[] { "Hooks/FordsOfWarMissionLogic.cs", "Hooks/FordsMissionLauncher.cs", "Cheats/FordsOfWarCheats.cs" })
        {
            var lines = System.IO.File.ReadAllLines(RepoPaths.RepoPath("Main", "Features", "FordsOfWar", file.Replace('/', System.IO.Path.DirectorySeparatorChar))).Length;
            Assert.IsTrue(lines < 150, $"{file} is {lines} lines (ADR-002: entry points under 150)");
        }
    }

    [TestMethod]
    public void TheSetupFlow_NamesNoEngineType()
    {
        // ADR-007: the prompt and the entry take the menu adapter; the engine's inquiry, option and state classes stay behind it.
        foreach (var file in new[] { "FordsSetupPrompt.cs", "FordsMenuEntry.cs", "FordsSetupChoices.cs" })
        {
            var source = RepoPaths.ReadSource("Main/Features/FordsOfWar/" + file, stripComments: true);
            foreach (var banned in new[] { "InquiryData", "InquiryElement", "InitialStateOption", "Module.CurrentModule", "InformationManager" })
                Assert.IsFalse(source.Contains(banned), $"{file} must not name {banned}");
        }
    }

    private static void AssertSingleton<T>(IResolver container) where T : class
    {
        var first = container.Resolve<T>();
        Assert.IsNotNull(first);
        Assert.AreSame(first, container.Resolve<T>(), typeof(T).Name + " must be a singleton");
    }
}
