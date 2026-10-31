using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Bannerlord.UIExtenderEx;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Client;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Tests.Infrastructure;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Composition;

/// <summary>The client submodule hands core its composition from its constructor (ADR-013).</summary>
[TestClass]
public class ClientSubModuleTests
{
    [ClassInitialize]
    public static void Init(TestContext _) => GameAssemblies.EnsureLoaded();

    [TestCleanup]
    public void Cleanup() => ClientComposition.Current = null;

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void Constructor_SetsTheClientCompositionSlot()
    {
        ClientComposition.Current = null;

        _ = new ClientSubModule();

        Assert.IsInstanceOfType(ClientComposition.Current, typeof(TaomClientComposition));
    }

    // ADR-013, B5: UIExtenderEx reads one assembly per Register call and refuses a second call under one module
    // name. The refusal traces an [ERR] "Failed to load extension module TAOM - already loaded!" line to ButterLib's
    // Configs/ModLogs/default<date>.log and, once Native has created the chat log, posts it as a red chat message;
    // the extensions of the refused call never load. So every TAOM extension lives in TAOM.Client and the client
    // registers once, under the name "TAOM" that the census and PatchShieldPolicy match. The once-guard, checked
    // before Create, keeps a second campaign or a custom battle from registering again.
    [TestMethod]
    public void OnGameStart_RegistersTheClientAssemblyOnceUnderTheTaomName()
    {
        var source = Collapse(RepoPaths.ReadSource("Client/ClientSubModule.cs", stripComments: true));

        StringAssert.Contains(source, "private const string UiExtenderModuleName = \"TAOM\";");
        var start = source.IndexOf("protected override void OnGameStart(", StringComparison.Ordinal);
        Assert.AreNotEqual(-1, start, "ClientSubModule has no OnGameStart: nothing registers the screen extensions.");
        var body = source.Substring(start);
        var guard = body.IndexOf("if (_uiExtender != null) return;", StringComparison.Ordinal);
        var create = body.IndexOf("_uiExtender = UIExtender.Create(UiExtenderModuleName);", StringComparison.Ordinal);
        Assert.IsTrue(guard >= 0 && create > guard, "OnGameStart must return when the UIExtender exists, before it creates one.");
        StringAssert.Contains(body.Substring(create),
            "_uiExtender = UIExtender.Create(UiExtenderModuleName); "
            + "ClientUiExtensions.Register(_uiExtender, typeof(ClientSubModule).Assembly); _uiExtender.Enable(); }");
    }

    // The IL, not the source text. A UIExtender made in core would register under the same module name and be
    // refused by UIExtenderEx's one-assembly rule. And registration must wait for a game: Register compiles every
    // mixin target, and the .NET Framework JIT may then run CampaignUIHelper's static initializer, which reads
    // GameTexts. GameTexts is null until Game.Initialize (OnSubModuleLoad, the first main menu, and the main menu
    // after a campaign ends), the initializer throws there, and .NET keeps CampaignUIHelper broken for the
    // process: TAOM patches on it can fail, and character creation crashes.
    [TestMethod]
    [TestCategory("RequiresGame")]
    [DataRow("Create")]
    [DataRow("Register")]
    public void UIExtenderCreateAndRegister_AreCalledOnlyFromTheClientOnGameStart(string uiExtenderMember)
    {
        var callers = TaomAssemblies.FindCallers(
            m => uiExtenderMember == "Create"
                ? m.DeclaringType == typeof(UIExtender) && m.Name == "Create"
                : m.DeclaringType == typeof(ClientUiExtensions) && m.Name == "Register"
                    && m.GetParameters().Length == 2 && m.GetParameters()[0].ParameterType == typeof(UIExtender),
            out var unreadable, out var scanned);

        Assert.IsTrue(scanned > 1000, "Only " + scanned + " bodies scanned across the TAOM assemblies.");
        Assert.AreEqual(0, unreadable.Count, "unreadable bodies cannot vouch: " + string.Join("; ", unreadable));
        CollectionAssert.AreEqual(new[] { typeof(ClientSubModule).FullName + ".OnGameStart" }, callers,
            "only the client's OnGameStart may create or register the UIExtender (ADR-013, CampaignUIHelper)");
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void ClientUiExtensions_Solo_RegistersTheWholeAssembly()
    {
        var assembly = typeof(ClientSubModule).Assembly;
        var assemblies = new List<Assembly>();
        var typeLists = new List<IReadOnlyList<Type>>();

        ClientUiExtensions.Register(assembly, () => false, Substitute.For<IModLogger>(), assemblies.Add, typeLists.Add);

        CollectionAssert.AreEqual(new[] { assembly }, assemblies, "solo takes UIExtenderEx's own Register(Assembly)");
        Assert.AreEqual(0, typeLists.Count, "solo registers no filtered list");
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void ClientUiExtensions_CoopActive_RegistersTheFilteredTypesWithoutTheSuppressedOnes()
    {
        var assembly = typeof(ClientSubModule).Assembly;
        var all = CoopUiRegistrationPolicy.CollectUiExtensionTypes(assembly);
        var suppressed = CoopUiRegistrationPolicy.Suppressed(all);
        var assemblies = new List<Assembly>();
        var typeLists = new List<IReadOnlyList<Type>>();

        ClientUiExtensions.Register(assembly, () => true, Substitute.For<IModLogger>(), assemblies.Add, typeLists.Add);

        Assert.AreEqual(0, assemblies.Count, "co-op registers a filtered list, not the whole assembly");
        Assert.AreEqual(1, typeLists.Count);
        Assert.IsTrue(suppressed.Count > 0, "the client declares [CoopSuppressedUi] types, so the filter has work to do");
        CollectionAssert.AreEquivalent(all.Except(suppressed).ToList(), typeLists[0].ToList());
        Assert.AreEqual(all.Count - suppressed.Count, typeLists[0].Count);
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void ClientUiExtensions_ProbeThrows_RegistersTheWholeAssemblyAndWarns()
    {
        var assembly = typeof(ClientSubModule).Assembly;
        var assemblies = new List<Assembly>();
        var typeLists = new List<IReadOnlyList<Type>>();
        var logger = Substitute.For<IModLogger>();

        ClientUiExtensions.Register(assembly, () => throw new InvalidOperationException("probe"), logger, assemblies.Add, typeLists.Add);

        CollectionAssert.AreEqual(new[] { assembly }, assemblies);
        Assert.AreEqual(0, typeLists.Count);
        logger.Received(1).LogWarning(NSubstitute.Arg.Is<string>(m => m.Contains("co-op probe failed")));
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void ClientUiExtensions_CoopFilterThrows_RegistersTheWholeAssembly()
    {
        var assembly = typeof(ClientSubModule).Assembly;
        var assemblies = new List<Assembly>();
        var logger = Substitute.For<IModLogger>();

        ClientUiExtensions.Register(assembly, () => true, logger, assemblies.Add,
            _ => throw new InvalidOperationException("register"));

        CollectionAssert.AreEqual(new[] { assembly }, assemblies);
        logger.Received(1).LogWarning(NSubstitute.Arg.Is<string>(m => m.Contains("filtering failed")));
    }

    // Module.cs 536-549 and 790-798: every return to the main menu runs OnBeforeInitialModuleScreenSetAsRoot on every submodule.
    // The Fords final verdict pass rides on it, through MainMenuStep so a fault never throws into the engine's tick.
    [TestMethod]
    public void OnBeforeInitialModuleScreenSetAsRoot_RunsTheFordsFinalPassThroughMainMenuStep()
    {
        var source = Collapse(RepoPaths.ReadSource("Client/ClientSubModule.cs", stripComments: true));

        var start = source.IndexOf("protected override void OnBeforeInitialModuleScreenSetAsRoot()", StringComparison.Ordinal);
        Assert.AreNotEqual(-1, start, "ClientSubModule has no OnBeforeInitialModuleScreenSetAsRoot override.");
        var body = source.Substring(start);
        var baseCall = body.IndexOf("base.OnBeforeInitialModuleScreenSetAsRoot();", StringComparison.Ordinal);
        var step = body.IndexOf("MainMenuStep.Run(\"fords of war final report\", () => IoC.Resolve<FordsRunReport>().MainMenuReturned(),", StringComparison.Ordinal);
        Assert.IsTrue(baseCall >= 0 && step > baseCall, "the base call comes first, then the guarded final pass");
    }

    private static string Collapse(string source) => Regex.Replace(source, @"\s+", " ");

    // Each move batch (B4a to B5) adds its rows here. This is the container round trip: DryIoc's
    // constructor selection fails at Register time, which no other test exercises for the client.
    [TestMethod]
    [TestCategory("RequiresGame")]
    public void TaomClientComposition_RegisterServices_RegistersExactlyTheExpectedServices()
    {
        var container = new Container();
        ClientComposition.Current = new TaomClientComposition();

        ClientComposition.Apply(container);

        var expected = new[]
        {
            typeof(TAOM.Adapters.IFactionMapWidgetAdapter),
            typeof(TAOM.Adapters.IFrontEndSpriteTableAdapter),
            typeof(TAOM.Features.CustomBattles.ICustomBattleFactionSelectionFactory),
            typeof(TAOM.Features.WarOfTheRingMomentum.UI.IMomentumUIService),
            typeof(TAOM.Adapters.IRealmBordersMapViewAdapter),
            typeof(TAOM.Adapters.IFieldCampOverlayAdapter),
            typeof(TAOM.Features.FiefManagement.IFiefsNavigationElementFactory),
            typeof(TAOM.Features.HallOfFire.IMapConversationProviderWrapper),
            typeof(TAOM.Features.HostReview.IHostReviewOrderProviderRegistrar),
            typeof(TAOM.Features.ShaderPrecompilation.IShaderPrecompileGameManagerFactory),
            typeof(TAOM.Features.ShaderCompileNotice.IShaderCompileNoticeWindowHost),
            typeof(TAOM.Features.FordsOfWar.IFordsOfWarGameManagerFactory),
            typeof(TAOM.Features.SupplyLines.ISupplyRouteVisualService),
            typeof(TAOM.Features.FieldCamp.ICampVisualService),
            typeof(TAOM.Features.Refuge.IRefugeVisualService),
            typeof(TAOM.Features.LinesOfDescent.UI.FamilyTreePopup),
            typeof(TAOM.Adapters.IQuestJournalAdapter),
        };
        var actual = container.GetServiceRegistrations().Select(r => r.ServiceType).ToArray();
        CollectionAssert.AreEquivalent(expected, actual);
    }
}
