using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DryIoc;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Diagnostics;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.ShadowfaxScreens;
using TAOM.Features.ShadowfaxScreens.Clan;
using TAOM.Features.ShadowfaxScreens.Diagnostics;
using TAOM.Features.ShadowfaxScreens.Inventory;
using TAOM.Features.ShadowfaxScreens.Party;
using TAOM.Features.ShadowfaxScreens.Quests;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.ShadowfaxScreens;

/// <summary>
/// How ShadowfaxScreensModule wires all four screens: registration, the five static facades, the install at game init, and
/// the two structural rules every Shadowfax hook class obeys (a category the module declares, a file under 150 lines).
/// </summary>
[TestClass]
public class ShadowfaxScreensModuleTests
{
    private static readonly string[] ExpectedCategories =
    {
        "Patch127_ShadowfaxClan", "Patch128_ShadowfaxInventory", "Patch129_ShadowfaxParty", "Patch130_ShadowfaxQuests",
    };

    private Container _container = null!;
    private readonly ShadowfaxScreensModule _sut = new ShadowfaxScreensModule();

    [TestInitialize]
    public void Setup() => _container = NewWiredContainer();

    [TestCleanup]
    public void Cleanup()
    {
        // InitializeStatics wires every screen's facade, so every one is reset and checked.
        ResetAllFacades();
        Assert.IsNull(ScreenOpenTiming.Timer, "the timer facade leaked out of the test");
        Assert.IsNull(ClanFastOpenCalls.Service, "the Clan facade leaked out of the test");
        Assert.IsNull(ShadowfaxInventoryCalls.Service, "the Inventory facade leaked out of the test");
        Assert.IsNull(PartyFastOpenCalls.Service, "the Party facade leaked out of the test");
        Assert.IsNull(QuestLogDeferCalls.Service, "the Quest log facade leaked out of the test");
        _container.Dispose();
    }

    // IStampClock, IModLogger, ICoopPresenceProvider and IDedicatedServerProvider belong to other registrations; the module
    // must not add them.
    private Container NewWiredContainer()
    {
        var container = new Container();
        container.RegisterInstance(Substitute.For<IStampClock>());
        container.RegisterInstance(Substitute.For<IModLogger>());
        container.RegisterInstance(Substitute.For<ICoopPresenceProvider>());
        container.RegisterInstance(Substitute.For<IDedicatedServerProvider>());
        _sut.RegisterServices(container);
        return container;
    }

    private static void ResetAllFacades()
    {
        ScreenOpenTiming.Initialize(null);
        ClanFastOpenCalls.Initialize(null);
        ShadowfaxInventoryCalls.Initialize(null);
        PartyFastOpenCalls.Initialize(null);
        QuestLogDeferCalls.Initialize(null);
    }

    [TestMethod]
    public void RegisterServices_ProviderResolvesAsASingleton()
    {
        var provider = _container.Resolve<IShadowfaxScreensSettingsProvider>();

        Assert.IsInstanceOfType(provider, typeof(ShadowfaxScreensSettingsProvider));
        Assert.AreSame(provider, _container.Resolve<IShadowfaxScreensSettingsProvider>());
    }

    [TestMethod]
    [DataRow(typeof(ScreenOpenTimer))]
    [DataRow(typeof(ClanScreenFastPathService))]
    [DataRow(typeof(InventoryOpenFastPathService))]
    [DataRow(typeof(PartyScreenFastPathService))]
    [DataRow(typeof(QuestLogDeferService))]
    public void RegisterServices_EachServiceResolvesAsASingleton(Type service)
    {
        Assert.AreSame(_container.Resolve(service), _container.Resolve(service));
    }

    [TestMethod]
    public void RegisterServices_TheQuestAdapterIsTheNullAdapterUntilTheClientReplacesIt()
    {
        // ADR-013: the reflection adapter names QuestItemVM, so it lives in TAOM.Client and TaomClientComposition replaces this.
        Assert.IsInstanceOfType(_container.Resolve<IQuestJournalAdapter>(), typeof(NullQuestJournalAdapter));
        Assert.AreSame(_container.Resolve<IQuestJournalAdapter>(), _container.Resolve<IQuestJournalAdapter>());
    }

    [TestMethod]
    public void RegisterServices_DoesNotRegisterTheStampClock()
    {
        var fresh = new Container();
        new ShadowfaxScreensModule().RegisterServices(fresh);

        Assert.IsFalse(fresh.IsRegistered<IStampClock>());
        fresh.Dispose();
    }

    [TestMethod]
    public void InitializeStatics_WiresAllFiveFacadesToTheirServices()
    {
        _sut.InitializeStatics(_container);

        Assert.AreSame(_container.Resolve<ScreenOpenTimer>(), ScreenOpenTiming.Timer);
        Assert.AreSame(_container.Resolve<ClanScreenFastPathService>(), ClanFastOpenCalls.Service);
        Assert.AreSame(_container.Resolve<InventoryOpenFastPathService>(), ShadowfaxInventoryCalls.Service);
        Assert.AreSame(_container.Resolve<PartyScreenFastPathService>(), PartyFastOpenCalls.Service);
        Assert.AreSame(_container.Resolve<QuestLogDeferService>(), QuestLogDeferCalls.Service);
    }

    [TestMethod]
    public void OnPhase_GameInit_BindsTheQuestAdapter()
    {
        var adapter = Substitute.For<IQuestJournalAdapter>();
        _container.RegisterInstance(adapter, IfAlreadyRegistered.Replace);

        _sut.OnPhase(ApplyPhase.GameInit, _container);

        adapter.Received(1).Bind();
    }

    [TestMethod]
    public void OnPhase_OtherPhases_DoNotBind()
    {
        var adapter = Substitute.For<IQuestJournalAdapter>();
        _container.RegisterInstance(adapter, IfAlreadyRegistered.Replace);

        _sut.OnPhase(ApplyPhase.ProcessLoad, _container);
        _sut.OnPhase(ApplyPhase.MainMenu, _container);
        _sut.OnPhase(ApplyPhase.FirstMission, _container);

        adapter.DidNotReceive().Bind();
    }

    // A dedicated server never indexes the [ClientOnly] hooks, so the clan count is 0 and the quest adapter is the null one:
    // both install lines would report a failure that did not happen.
    [TestMethod]
    public void OnPhase_GameInit_OnADedicatedServer_BindsNothingAndWritesNoInstallLine()
    {
        var logger = Substitute.For<IModLogger>();
        var adapter = Substitute.For<IQuestJournalAdapter>();
        var server = Substitute.For<IDedicatedServerProvider>();
        server.IsDedicatedServer.Returns(true);
        _container.RegisterInstance(logger, IfAlreadyRegistered.Replace);
        _container.RegisterInstance(adapter, IfAlreadyRegistered.Replace);
        _container.RegisterInstance(server, IfAlreadyRegistered.Replace);

        _sut.OnPhase(ApplyPhase.GameInit, _container);

        adapter.DidNotReceive().Bind();
        logger.DidNotReceive().LogWarning(NSubstitute.Arg.Any<string>());
        logger.DidNotReceive().LogInfo(NSubstitute.Arg.Is<string>(m => m.Contains("Clan fast open")));
    }

    [TestMethod]
    public void OnPhase_GameInit_WritesOneClanInstallLine()
    {
        var logger = Substitute.For<IModLogger>();
        _container.RegisterInstance(logger, IfAlreadyRegistered.Replace);
        _container.RegisterInstance(Substitute.For<IQuestJournalAdapter>(), IfAlreadyRegistered.Replace);
        ClanFastOpenCalls.LastSwapped = 1;
        try
        {
            _sut.OnPhase(ApplyPhase.GameInit, _container);
        }
        finally
        {
            ClanFastOpenCalls.LastSwapped = 0;
        }

        logger.Received(1).LogInfo(NSubstitute.Arg.Is<string>(m => m.Contains("Clan fast open installed")));
    }

    [TestMethod]
    public void Module_IsListedOnce_AndDeclaresItsCategoriesAtGameInit()
    {
        var modules = FeatureModules.All.OfType<ShadowfaxScreensModule>().ToList();

        Assert.AreEqual(1, modules.Count, "ShadowfaxScreensModule is not in FeatureModules.All exactly once.");
        Assert.AreEqual("ShadowfaxScreens", modules[0].Id);
        Assert.IsNull(modules[0].ParkedReason);
        CollectionAssert.AreEquivalent(ExpectedCategories, modules[0].PatchCategories.Select(d => d.Category).ToList());
        Assert.IsTrue(modules[0].PatchCategories.All(d => d.Phase == ApplyPhase.GameInit));
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void EveryShadowfaxPatchClass_CarriesACategoryTheModuleDeclares()
    {
        // The patch attributes name engine types, so reading them needs the game assemblies.
        if (!GameAssemblies.EnsureLoaded()) Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
        var declared = _sut.PatchCategories.Select(d => d.Category).ToList();
        var patches = HookTypes().Where(t => t.GetCustomAttributes<HarmonyPatch>(false).Any()).ToList();

        Assert.IsTrue(patches.Count >= 10, "the reflection found too few patch classes: " + patches.Count);
        foreach (var patch in patches)
        {
            var attribute = patch.GetCustomAttributes<HarmonyPatchCategory>(false).SingleOrDefault();
            Assert.IsNotNull(attribute, patch.Name + " has no [HarmonyPatchCategory]");
            CollectionAssert.Contains(declared, attribute!.info.category, patch.Name);
        }
    }

    [TestMethod]
    public void EveryShadowfaxHookFile_StaysUnder150Lines()
    {
        var root = SourceRoot();
        var files = Directory.GetDirectories(root, "Hooks", SearchOption.AllDirectories)
            .SelectMany(d => Directory.GetFiles(d, "*.cs")).ToList();

        Assert.IsTrue(files.Count >= 10, "the hook folders were not found under " + root);
        foreach (var file in files)
            Assert.IsTrue(File.ReadAllLines(file).Length < 150, file);
    }

    private static string SourceRoot()
    {
        var dir = Path.GetDirectoryName(typeof(ShadowfaxScreensModuleTests).Assembly.Location);
        while (dir != null && !Directory.Exists(Path.Combine(dir, "Main", "Features", "ShadowfaxScreens")))
            dir = Path.GetDirectoryName(dir);
        if (dir == null) Assert.Inconclusive("Main/ source tree not found from the test assembly.");
        return Path.Combine(dir!, "Main", "Features", "ShadowfaxScreens");
    }

    // Some TAOM types name engine types the test host may not load; the hook classes are still reported.
    private static IEnumerable<Type> HookTypes()
    {
        Type?[] all;
        try { all = typeof(ShadowfaxScreensModule).Assembly.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { all = ex.Types; }
        return all.OfType<Type>().Where(t =>
            t.Namespace != null && t.Namespace.StartsWith("TAOM.Features.ShadowfaxScreens.", StringComparison.Ordinal)
            && t.Namespace.EndsWith(".Hooks", StringComparison.Ordinal));
    }
}
