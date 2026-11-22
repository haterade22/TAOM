using System.Linq;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Client;
using TAOM.Client.Features.Autorun;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.Autorun.Scenario;
using TAOM.Tests.Infrastructure;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.Autorun.Scenario;

/// <summary>
/// Wiring of the campaign scenario autorun's client half (#1075, ADR-013): the Client host replaces core's null host, and the client's
/// application tick builds the driver only through the start helper and ticks it. The host is never constructed here: it subscribes to
/// engine events, so the composition check resolves the registration's implementation type only.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class ScenarioClientWiringTests
{
    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => GameAssemblies.EnsureLoaded();

    [TestCleanup]
    public void Cleanup() => ClientComposition.Current = null;

    [TestMethod]
    public void Apply_OverCoresNullHost_RegistersTheClientHost()
    {
        var container = new Container();
        container.Register<ICampaignAutorunHost, NullCampaignAutorunHost>(Reuse.Singleton);
        container.RegisterInstance(Substitute.For<IModLogger>());
        ClientComposition.Current = new TaomClientComposition();

        ClientComposition.Apply(container);

        var registration = container.GetServiceRegistrations()
            .Single(r => r.ServiceType == typeof(ICampaignAutorunHost));
        Assert.AreEqual(typeof(CampaignAutorunHost), registration.Factory.ImplementationType);
    }

    [TestMethod]
    public void ClientSubModuleSource_BuildsTheDriverThroughTheStartHelper_AndTicksIt()
    {
        var src = RepoPaths.ReadSource("Client/ClientSubModule.cs", stripComments: true);

        StringAssert.Contains(src,
            "_scenarioAutorun = ScenarioAutorunStart.Driver(IoC.Resolve<ScenarioPlan>(), IoC.Resolve<ScenarioAutorunDriver>, IoC.Resolve<IModLogger>());");
        StringAssert.Contains(src, "_scenarioAutorun?.Tick(dt);");
    }

    [TestMethod]
    public void ClientSubModuleSource_RestoresTheFocusSettingAtEveryGameEnd()
    {
        var src = RepoPaths.ReadSource("Client/ClientSubModule.cs", stripComments: true);

        StringAssert.Contains(src, "public override void OnGameEnd(Game game)");
        StringAssert.Contains(src, "_scenarioAutorun?.GameEnded();");
    }

    [TestMethod]
    public void HostSource_TakesItsPlanByConstructor_AndResolvesNothingItself()
    {
        var src = RepoPaths.ReadSource("Client/Features/Autorun/CampaignAutorunHost.cs", stripComments: true);

        Assert.IsFalse(src.Contains("IoC."), "the host takes its plan by constructor");
        StringAssert.Contains(src, "public CampaignAutorunHost(ScenarioPlan plan)");
    }

    // #1368: the driver opens the dev menu through its own Open (the same refusals), then picks an option on the path a person's click
    // takes (GameMenuItemVM.ExecuteAction -> MenuContext.InvokeConsequence), which runs no condition itself, so the host checks it first.
    [TestMethod]
    public void HostSource_ChoosesAStoryOptionAsAClickDoes()
    {
        var src = RepoPaths.ReadSource("Client/Features/Autorun/CampaignAutorunHost.cs", stripComments: true);

        StringAssert.Contains(src, "GetCampaignBehavior<StoryDevMenuBehavior>()");
        StringAssert.Contains(src, ".Open(missionId, true, verdict => _storyVerdict = verdict)");
        StringAssert.Contains(src, "MenuRepeatObjects.Count > 0");
        StringAssert.Contains(src, ".GetMenuOptionConditionsHold(");
        StringAssert.Contains(src, ".InvokeConsequence(index)");
        StringAssert.Contains(src, "Choose(StoryDevMenuBehavior.MenuId, StoryDevMenuBehavior.OpenOptionId)");
        StringAssert.Contains(src, "Choose(StoryDevMenuBehavior.MenuId, StoryDevMenuBehavior.BackOptionId)");
        Assert.IsFalse(src.Contains("StoryMissionLauncher"), "the mission opens from the menu option, never from the launcher directly");
        Assert.IsFalse(src.Contains("RunConsequence"), "the click's path, never the option's consequence run directly");
    }

    // #1105 slices 3 to 5: the offer popup's accept button is the two steps SingleQueryPopUpVM.ExecuteAffirmativeAction runs, in its order: the
    // inquiry's affirmative action, then the close (InformationManager.HideInquiry runs the query manager's own CloseQuery).
    [TestMethod]
    public void HostSource_AcceptsTheOfferPopupAsItsAffirmativeButtonDoes()
    {
        var src = RepoPaths.ReadSource("Client/Features/Autorun/CampaignAutorunHost.cs", stripComments: true);

        var action = src.IndexOf("data.AffirmativeAction?.Invoke();", System.StringComparison.Ordinal);
        var close = src.IndexOf("InformationManager.HideInquiry();", action, System.StringComparison.Ordinal);
        Assert.IsTrue(action > 0 && close > action, "the affirmative action runs first, then the close");
        StringAssert.Contains(src, "GetCampaignBehavior<StoryOfferBehavior>()");
        StringAssert.Contains(src, "BotPlaysNextMission = botPlays");
        StringAssert.Contains(src, "InformationManager.OnShowInquiry += (data, _, _) =>");
        StringAssert.Contains(src, "Choose(StoryOfferBehavior.MenuId, id)");
    }

    // A second ShowInquiry in the same campaign tick is queued by the engine but still fires OnShowInquiry, so the accept never reads the newest one.
    [TestMethod]
    public void HostSource_AcceptsOnlyTheInquiryTheOffersPendingOfferRaised()
    {
        var src = RepoPaths.ReadSource("Client/Features/Autorun/CampaignAutorunHost.cs", stripComments: true);

        StringAssert.Contains(src, "StoryOfferPopup.Line(offer.Window.Title, offer.Window).ToString()");
        StringAssert.Contains(src, "_offerInquiry = data;");
        var from = src.IndexOf("public bool AcceptOfferPopup", System.StringComparison.Ordinal);
        var accept = src.Substring(from, src.IndexOf("public bool OfferMenuShown", from, System.StringComparison.Ordinal) - from);
        Assert.IsFalse(accept.Contains("_lastInquiry"), "the accept presses the offer's own inquiry, never the newest one shown");
        StringAssert.Contains(accept, "_offerInquiry");
    }

    [TestMethod]
    public void HostSource_MapsMissionStateToMission()
    {
        var src = RepoPaths.ReadSource("Client/Features/Autorun/CampaignAutorunHost.cs", stripComments: true);

        StringAssert.Contains(src, "MissionState => AutorunScreen.Mission,");
    }

    [TestMethod]
    public void HostSource_AnEventWindowWaitingCountsAsOpen()
    {
        var src = RepoPaths.ReadSource("Client/Features/Autorun/CampaignAutorunHost.cs", stripComments: true);

        StringAssert.Contains(src, "public bool EventWindowOpenOrWaiting => EventWindows.IsOpen;");
    }
}
