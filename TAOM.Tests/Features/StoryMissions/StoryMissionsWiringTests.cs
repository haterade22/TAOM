using System.Collections.Generic;
using System.Linq;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.DevConsole.Cheats;
using TAOM.Features.Enlistment;
using TAOM.Features.StoryMissions;
using TAOM.Features.StoryMissions.Config;
using TAOM.Features.StoryMissions.Hooks;
using TAOM.Tests.Infrastructure;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.StoryMissions;

/// <summary>The feature's registration, its dev menu and the console command that reaches it.</summary>
[TestClass]
[TestCategory("RequiresGame")]
public class StoryMissionsWiringTests
{
    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => GameAssemblies.EnsureLoaded();

    private const string OneMission =
        "{\"missions\":[{\"id\":\"m1\",\"scene\":\"scene_a\",\"side\":\"Defend\",\"defenderCulture\":\"vlandia\",\"attackerCulture\":\"isengard\","
        + "\"garrison\":[{\"troop\":\"g1\",\"class\":\"Infantry\",\"count\":10}],"
        + "\"roster\":{\"waves\":[{\"count\":110,\"mix\":[{\"troop\":\"a1\",\"class\":\"Infantry\",\"weight\":1}]}]}}]}";

    private static Container Registered()
    {
        var container = new Container();
        container.RegisterInstance(Substitute.For<IModLogger>());
        container.RegisterInstance(Substitute.For<IPathService>());
        container.RegisterInstance(Substitute.For<IOutcomeLog>());
        container.RegisterInstance(Substitute.For<IGameMenuAdapter>());
        container.RegisterInstance(Substitute.For<ICoopSessionProvider>());
        container.RegisterInstance(Substitute.For<IDedicatedServerProvider>());
        container.RegisterInstance(Substitute.For<IEnlistmentStateQuery>());
        container.RegisterInstance(Substitute.For<IEncounterAdapter>());
        new StoryMissionsModule().RegisterServices(container);
        return container;
    }

    private static (StoryDevMenuBehavior Behavior, IGameMenuAdapter Menus) Behavior(bool singlePlayer = true, string? json = OneMission,
        bool enlisted = false, bool inEncounter = false, bool inMapEvent = false, string? currentMenu = null)
    {
        var logger = Substitute.For<IModLogger>();
        var menus = Substitute.For<IGameMenuAdapter>();
        menus.EnsureMenuOpen(StoryDevMenuBehavior.MenuId).Returns(true);
        menus.CurrentMenuId.Returns(currentMenu);
        var coop = Substitute.For<ICoopSessionProvider>();
        coop.IsAuthority.Returns(true);
        coop.IsSessionActive.Returns(!singlePlayer);
        var enlistment = Substitute.For<IEnlistmentStateQuery>();
        enlistment.IsEnlisted.Returns(enlisted);
        var encounter = Substitute.For<IEncounterAdapter>();
        encounter.HasCurrent.Returns(inEncounter);
        encounter.IsMainPartyInMapEvent.Returns(inMapEvent);
        var catalog = StoryMissionCatalog.FromText(json, logger);
        var launcher = new StoryMissionLauncher(catalog, logger, Substitute.For<IOutcomeLog>());
        var behavior = new StoryDevMenuBehavior(catalog, launcher, menus, coop, Substitute.For<IDedicatedServerProvider>(), enlistment, encounter);
        return (behavior, menus);
    }

    [TestMethod]
    public void Module_ListedOnceInTheFeatureModules()
    {
        Assert.AreEqual(1, FeatureModules.All.OfType<StoryMissionsModule>().Count());
    }

    [TestMethod]
    public void Module_DeclaresTheDevMenuAndTheStoryOffer_NoMissionBehaviorModelPatchOrSaveData()
    {
        var module = new StoryMissionsModule();

        Assert.AreEqual(2, module.CampaignBehaviors.Count);
        Assert.AreEqual(typeof(StoryDevMenuBehavior), module.CampaignBehaviors[0].BehaviorType);
        Assert.AreEqual(typeof(StoryOfferBehavior), module.CampaignBehaviors[1].BehaviorType);
        Assert.AreEqual(0, module.MissionBehaviors.Count, "the mission behaviors are built by the launcher, as Host Review's are");
        Assert.AreEqual(0, module.GameModels.Count);
        Assert.AreEqual(0, module.PatchCategories.Count);
        Assert.IsFalse(module.OwnsSaveData);
        Assert.IsNull(module.ParkedReason);
    }

    [TestMethod]
    public void RegisterServices_EachServiceResolvesAsASingleton()
    {
        var container = Registered();

        Assert.AreSame(container.Resolve<StoryMissionCatalog>(), container.Resolve<StoryMissionCatalog>());
        Assert.AreSame(container.Resolve<StoryMissionLauncher>(), container.Resolve<StoryMissionLauncher>());
    }

    [TestMethod]
    public void TheCampaignBehaviorDecl_BuildsAFreshBehaviorPerCampaign()
    {
        var container = Registered();
        var decl = new StoryMissionsModule().CampaignBehaviors[0];

        var first = decl.Create(container);
        var second = decl.Create(container);

        Assert.IsInstanceOfType(first, typeof(StoryDevMenuBehavior));
        Assert.AreNotSame(first, second, "the pending request lives on the behavior, so a second campaign starts clean");
    }

    [TestMethod]
    public void Open_AKnownMission_OpensTheDevMenu()
    {
        var (behavior, menus) = Behavior();

        var message = behavior.Open("m1", false);

        menus.Received(1).EnsureMenuOpen(StoryDevMenuBehavior.MenuId);
        StringAssert.Contains(message, "m1");
    }

    [TestMethod]
    public void Open_AnUnknownMission_NamesTheKnownOnes_AndOpensNothing()
    {
        var (behavior, menus) = Behavior();

        var message = behavior.Open("nowhere", false);

        menus.DidNotReceiveWithAnyArgs().EnsureMenuOpen(default!);
        StringAssert.Contains(message, "nowhere");
        StringAssert.Contains(message, "m1");
    }

    [TestMethod]
    public void Open_WithNoMissionsLoaded_SaysSo()
    {
        var (behavior, menus) = Behavior(json: null);

        var message = behavior.Open("m1", false);

        menus.DidNotReceiveWithAnyArgs().EnsureMenuOpen(default!);
        StringAssert.Contains(message, "no story missions");
    }

    [TestMethod]
    public void Open_InACoopSession_IsRefused()
    {
        var (behavior, menus) = Behavior(singlePlayer: false);

        var message = behavior.Open("m1", false);

        menus.DidNotReceiveWithAnyArgs().EnsureMenuOpen(default!);
        StringAssert.Contains(message, "co-op");
    }

    // Host Review's refusals (HostReviewService.GetBlock). Without them the switch replaces an encounter, town or camp menu, and the
    // mission then runs over a live encounter, where HeroKillCounterMissionLogic counts the story kills (#1105 review, MEDIUM-1).
    [TestMethod]
    public void Open_WhileEnlisted_IsRefused()
    {
        var (behavior, menus) = Behavior(enlisted: true);

        var message = behavior.Open("m1", false);

        menus.DidNotReceiveWithAnyArgs().EnsureMenuOpen(default!);
        StringAssert.Contains(message, "enlisted");
    }

    [TestMethod]
    public void Open_InAnEncounter_IsRefused()
    {
        var (behavior, menus) = Behavior(inEncounter: true);

        var message = behavior.Open("m1", false);

        menus.DidNotReceiveWithAnyArgs().EnsureMenuOpen(default!);
        StringAssert.Contains(message, "encounter");
    }

    [TestMethod]
    public void Open_WithTheMainPartyInAMapEvent_IsRefused()
    {
        var (behavior, menus) = Behavior(inMapEvent: true);

        var message = behavior.Open("m1", false);

        menus.DidNotReceiveWithAnyArgs().EnsureMenuOpen(default!);
        StringAssert.Contains(message, "encounter");
    }

    [TestMethod]
    public void Open_OverAnotherMenu_IsRefused_AndNamesThatMenu()
    {
        var (behavior, menus) = Behavior(currentMenu: "camp");

        var message = behavior.Open("m1", false);

        menus.DidNotReceiveWithAnyArgs().EnsureMenuOpen(default!);
        StringAssert.Contains(message, "camp");
    }

    [TestMethod]
    public void Open_OverTheStoryMenuItself_Opens()
    {
        var (behavior, menus) = Behavior(currentMenu: StoryDevMenuBehavior.MenuId);

        behavior.Open("m1", false);

        menus.Received(1).EnsureMenuOpen(StoryDevMenuBehavior.MenuId);
    }

    [TestMethod]
    public void Open_WhenTheMenuWillNotOpen_SaysWhy()
    {
        var (behavior, menus) = Behavior();
        menus.EnsureMenuOpen(StoryDevMenuBehavior.MenuId).Returns(false);

        var message = behavior.Open("m1", false);

        StringAssert.Contains(message, "could not open");
    }

    [TestMethod]
    public void TheBehavior_SavesNothing()
    {
        var source = RepoPaths.ReadSource("Main/Features/StoryMissions/Hooks/StoryDevMenuBehavior.cs", stripComments: true);

        Assert.IsFalse(source.Contains("dataStore.SyncData"), "a story mission writes nothing to the campaign");
        Assert.IsFalse(source.Contains("TaleWorlds.CampaignSystem.Actions"), "no campaign action in Slice 1");
    }

    [TestMethod]
    public void TryParse_AMissionId_IsTakenAsIs()
    {
        Assert.IsTrue(StoryMissionCheats.TryParse(new List<string> { "helms_deep_defend" }, out var id, out var auto, out var error));
        Assert.AreEqual("helms_deep_defend", id);
        Assert.IsFalse(auto);
        Assert.AreEqual("", error);
    }

    [TestMethod]
    public void TryParse_TheAutoWord_TurnsTheBotOn()
    {
        Assert.IsTrue(StoryMissionCheats.TryParse(new List<string> { "m1", "auto" }, out var id, out var auto, out _));
        Assert.AreEqual("m1", id);
        Assert.IsTrue(auto);
    }

    [TestMethod]
    public void TryParse_NoArguments_GivesTheUsage()
    {
        Assert.IsFalse(StoryMissionCheats.TryParse(new List<string>(), out _, out _, out var error));
        StringAssert.Contains(error, "taom.story_open");
    }

    [TestMethod]
    public void TryParse_ABlankId_GivesTheUsage()
    {
        Assert.IsFalse(StoryMissionCheats.TryParse(new List<string> { "  " }, out _, out _, out var error));
        StringAssert.Contains(error, "taom.story_open");
    }

    [TestMethod]
    public void TryParse_AnUnknownSecondWord_IsRefusedNotIgnored()
    {
        Assert.IsFalse(StoryMissionCheats.TryParse(new List<string> { "m1", "atuo" }, out _, out _, out var error));
        StringAssert.Contains(error, "atuo");
    }

    [TestMethod]
    public void TryParse_ATooLongLine_IsRefused()
    {
        Assert.IsFalse(StoryMissionCheats.TryParse(new List<string> { "m1", "auto", "x" }, out _, out _, out var error));
        StringAssert.Contains(error, "taom.story_open");
    }

    [TestMethod]
    public void TryParse_ANullList_IsRefused()
    {
        Assert.IsFalse(StoryMissionCheats.TryParse(null!, out _, out _, out _));
    }
}
