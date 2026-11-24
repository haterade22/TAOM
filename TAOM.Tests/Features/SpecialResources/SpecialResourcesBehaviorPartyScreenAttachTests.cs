using System;
using System.Reflection;
using System.Runtime.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem.Party;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.SpecialResources;

namespace TAOM.Tests.Features.SpecialResources;

/// <summary>
/// #1516, Codex F1: the map bar applies a party screen's changes and pops it without PartyScreenClosedEvent
/// (MapNavigationHelper.ApplyCurrentChanges, SwitchToANewScreen). The next party screen must take the subscription,
/// or its Reset never reaches the session and a later Done charges upgrades the player reset.
/// </summary>
[TestClass]
public class SpecialResourcesBehaviorPartyScreenAttachTests
{
    private static SpecialResourcesBehavior NewBehavior() => NewBehavior(Substitute.For<ISpecialResourceService>());

    private static SpecialResourcesBehavior NewBehavior(ISpecialResourceService service)
    {
        var storage = Substitute.For<ISpecialResourceStorageService>();
        return new SpecialResourcesBehavior(service, storage, Substitute.For<IModLogger>(),
            new SpecialResourceEarningService(service, storage, Substitute.For<ICoopSessionProvider>(),
                Substitute.For<IDedicatedServerProvider>(), Substitute.For<IModLogger>(),
                new TAOM.Features.CheatMenu.CheatPartyRules()),
            Substitute.For<ISpecialResourceEventAdapter>());
    }

    private static PartyScreenLogic NewLogic() =>
        (PartyScreenLogic)FormatterServices.GetUninitializedObject(typeof(PartyScreenLogic));

    private static Delegate? Handlers(PartyScreenLogic logic, string eventName) =>
        (Delegate?)typeof(PartyScreenLogic)
            .GetField(eventName, BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(logic);

    [TestCategory("RequiresGame")]   // constructs CampaignBehaviorBase
    [TestMethod]
    public void AttachToPartyScreen_ANewScreenAfterAnUnclosedOne_MovesBothSubscriptions()
    {
        var behavior = NewBehavior();
        var first = NewLogic();
        var second = NewLogic();

        behavior.AttachToPartyScreen(first);
        behavior.AttachToPartyScreen(second);

        Assert.IsNull(Handlers(first, "AfterReset"));
        Assert.IsNull(Handlers(first, "PartyScreenClosedEvent"));
        Assert.IsNotNull(Handlers(second, "AfterReset"));
        Assert.IsNotNull(Handlers(second, "PartyScreenClosedEvent"));
    }

    [TestCategory("RequiresGame")]   // constructs CampaignBehaviorBase
    [TestMethod]
    public void PartyScreenClosed_FromCancel_DetachesAndCancelsTheSession()
    {
        var service = Substitute.For<ISpecialResourceService>();
        var behavior = NewBehavior(service);
        var logic = NewLogic();
        behavior.AttachToPartyScreen(logic);

        Handlers(logic, "PartyScreenClosedEvent")!.DynamicInvoke(null, null, null, null, null, null, true);

        Assert.IsNull(Handlers(logic, "AfterReset"));
        Assert.IsNull(Handlers(logic, "PartyScreenClosedEvent"));
        service.Received(1).CancelSession();
    }

    [TestCategory("RequiresGame")]   // constructs CampaignBehaviorBase
    [TestMethod]
    public void AttachToPartyScreen_TheSameScreenTwice_SubscribesOnce()
    {
        var behavior = NewBehavior();
        var logic = NewLogic();

        behavior.AttachToPartyScreen(logic);
        behavior.AttachToPartyScreen(logic);

        Assert.AreEqual(1, Handlers(logic, "AfterReset")!.GetInvocationList().Length);
        Assert.AreEqual(1, Handlers(logic, "PartyScreenClosedEvent")!.GetInvocationList().Length);
    }
}
