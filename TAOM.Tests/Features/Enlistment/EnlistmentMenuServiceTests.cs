using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
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
        _service = new EnlistmentMenuService(_store, new EnlistmentConfigProvider(_logger), _logger);
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
}
