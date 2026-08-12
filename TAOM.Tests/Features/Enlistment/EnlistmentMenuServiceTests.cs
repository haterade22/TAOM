using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment;
using TAOM.Features.Enlistment.Domain;

namespace TAOM.Tests.Features.Enlistment;

[TestClass]
public class EnlistmentMenuServiceTests
{
    private IModLogger _logger = null!;
    private EnlistmentStore _store = null!;
    private EnlistmentMenuService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _store = new EnlistmentStore(_logger);
        _service = new EnlistmentMenuService(_store, new EnlistmentConfigProvider(_logger), Substitute.For<IMobilePartyAttachmentAdapter>(), _logger);
    }

    private void MakeEnlisted(EnlistmentState state = EnlistmentState.EnlistedAttached)
    {
        _store.Record.State = state;
        _store.Record.EnlistedHeroId = "main_hero";
        _store.Record.CommanderHeroId = "lord_1_1";
    }

    [DataTestMethod]
    [DataRow("army_wait")]
    [DataRow("army_wait_at_settlement")]
    [DataRow("town_wait_menus")]
    [DataRow("village_wait_menus")]
    [DataRow("town_outside")]
    [DataRow("castle_outside")]
    [DataRow("encounter")]
    [DataRow("join_encounter")]
    [DataRow("port_menu")]
    [DataRow("naval_town_outside")]
    public void TryRedirect_AttachedState_RedirectsDefaultListToWaitMenu(string menuId)
    {
        MakeEnlisted();

        var redirected = _service.TryRedirectMenu(menuId, out var target);

        Assert.IsTrue(redirected, $"'{menuId}' should redirect while attached");
        Assert.AreEqual(EnlistmentMenuService.ServiceWaitMenuId, target);
    }

    [TestMethod]
    public void TryRedirect_NotEnlisted_NeverRedirects()
    {
        Assert.IsFalse(_service.TryRedirectMenu("army_wait", out _));
    }

    [TestMethod]
    public void TryRedirect_PetitionPending_NeverRedirects()
    {
        _store.Record.State = EnlistmentState.PetitionPending;
        _store.Record.PetitionCommanderId = "lord_1_1";

        Assert.IsFalse(_service.TryRedirectMenu("army_wait", out _));
    }

    [DataTestMethod]
    [DataRow(EnlistmentState.EnlistedBattle)]
    [DataRow(EnlistmentState.EnlistedDetachedOnDuty)]
    [DataRow(EnlistmentState.EnlistedPlayerCaptive)]
    [DataRow(EnlistmentState.CommanderUnavailable)]
    public void TryRedirect_NonAttachedServiceStates_NeverRedirect(EnlistmentState state)
    {
        // EnlistedBattle: the battle service transitions BEFORE pushing encounter menus,
        // so battle menus flow through. Duty/captive/grace: the player roams free.
        MakeEnlisted(state);

        Assert.IsFalse(_service.TryRedirectMenu("encounter", out _));
        Assert.IsFalse(_service.TryRedirectMenu("army_wait", out _));
    }

    [TestMethod]
    public void TryRedirect_WaitMenuItself_NeverRedirects()
    {
        MakeEnlisted();

        Assert.IsFalse(_service.TryRedirectMenu(EnlistmentMenuService.ServiceWaitMenuId, out _));
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void TryRedirect_NullOrEmpty_NeverRedirects(string menuId)
    {
        MakeEnlisted();

        Assert.IsFalse(_service.TryRedirectMenu(menuId, out _));
    }

    [TestMethod]
    public void TryRedirect_UnknownMenuWhileAttached_FailsOpenAndLogsOncePerId()
    {
        // Fail-open is the policy (a fail-closed allowlist could eat quest menus); the
        // once-per-id log line is the diagnostic funnel for extending the redirect list.
        MakeEnlisted();

        Assert.IsFalse(_service.TryRedirectMenu("some_quest_menu", out _));
        Assert.IsFalse(_service.TryRedirectMenu("some_quest_menu", out _));
        Assert.IsFalse(_service.TryRedirectMenu("other_menu", out _));

        _logger.Received(1).LogInfo(Arg.Is<string>(s => s.Contains("some_quest_menu")));
        _logger.Received(1).LogInfo(Arg.Is<string>(s => s.Contains("other_menu")));
    }

    [TestMethod]
    public void TryRedirect_UnknownIdFloodPastCap_ClearsSetInsteadOfLeaking()
    {
        MakeEnlisted();
        for (var i = 0; i <= 101; i++)
            _service.TryRedirectMenu($"generated_menu_{i}", out _);

        // The 102nd distinct id crossed the cap: the set was cleared and logging resumed —
        // re-reporting an already-seen id afterwards proves the reset happened.
        _logger.ClearReceivedCalls();
        _service.TryRedirectMenu("generated_menu_0", out _);
        _logger.Received(1).LogInfo(Arg.Is<string>(s => s.Contains("generated_menu_0")));
    }

    // ---- settlement menus (Batch 7: settlement following) --------------------------------

    [DataTestMethod]
    [DataRow("town")]
    [DataRow("castle")]
    [DataRow("village")]
    public void TryRedirectMenu_VanillaSettlementMenus_RedirectedWhileAttached(string menuId)
    {
        // Settlement following places the player INSIDE the settlement, so vanilla will try to
        // open the real town menu — which would hand a serving soldier the keep, the market and
        // the recruiter. Held in the service menu instead.
        MakeEnlisted();

        Assert.IsTrue(_service.TryRedirectMenu(menuId, out var redirected));
        Assert.AreEqual(EnlistmentMenuService.ServiceWaitMenuId, redirected);
    }

    [DataTestMethod]
    [DataRow("town")]
    [DataRow("castle")]
    [DataRow("village")]
    public void TryRedirectMenu_VanillaSettlementMenus_NotRedirectedWhenNotEnlisted(string menuId)
    {
        // The proof that discharge restores town access with no extra teardown: the redirect is
        // gated on state, so the moment service ends the player's own town flow is back.
        Assert.IsFalse(_service.TryRedirectMenu(menuId, out var redirected));
        Assert.IsNull(redirected);
    }

    [DataTestMethod]
    [DataRow("town")]
    [DataRow("village")]
    public void TryRedirectMenu_SettlementMenus_NotRedirectedWhileOnDuty(string menuId)
    {
        // Duties send the player to visit settlements on their own. Redirecting there would
        // make the objective unreachable — the whole point is that they walk in and do a thing.
        MakeEnlisted(EnlistmentState.EnlistedDetachedOnDuty);

        Assert.IsFalse(_service.TryRedirectMenu(menuId, out _));
    }

    // ---- shore leave (field report 1) ----

    [DataTestMethod]
    [DataRow("town")]
    [DataRow("castle")]
    [DataRow("village")]
    public void TryRedirectMenu_OnShoreLeave_SettlementMenusPassThrough(string menuId)
    {
        // The whole fix for "you can't enter towns when your lord does". The player was already
        // physically inside the settlement; this redirect was the only thing between him and it.
        MakeEnlisted(EnlistmentState.EnlistedAttached);
        _store.Record.OnTownLeave = true;

        Assert.IsFalse(_service.TryRedirectMenu(menuId, out _));
    }

    [DataTestMethod]
    [DataRow("town")]
    [DataRow("castle")]
    [DataRow("village")]
    public void TryRedirectMenu_WithoutTheLeavePass_SettlementMenusStillRedirect(string menuId)
    {
        // The other half: without the pass this must behave exactly as before, or the pass is not a
        // gate at all. Pairs with the test above so a change to the flag check cannot pass both.
        MakeEnlisted(EnlistmentState.EnlistedAttached);

        Assert.IsTrue(_service.TryRedirectMenu(menuId, out var redirected));
        Assert.AreEqual(EnlistmentMenuService.ServiceWaitMenuId, redirected);
    }

    [DataTestMethod]
    [DataRow("town_outside")]
    [DataRow("castle_outside")]
    [DataRow("naval_town_outside")]
    public void TryRedirectMenu_OnShoreLeave_ApproachMenusStillRedirect(string menuId)
    {
        // Deliberately NARROW. The pass releases the three menus of the settlement the player is
        // standing IN, not the approach menus that put him back on the map deciding whether to
        // besiege the place. A soldier on a pass is already inside.
        MakeEnlisted(EnlistmentState.EnlistedAttached);
        _store.Record.OnTownLeave = true;

        Assert.IsTrue(_service.TryRedirectMenu(menuId, out var redirected));
        Assert.AreEqual(EnlistmentMenuService.ServiceWaitMenuId, redirected);
    }
}
