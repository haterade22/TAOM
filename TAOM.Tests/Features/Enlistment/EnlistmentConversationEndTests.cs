using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.Enlistment;
using TAOM.Features.Enlistment.Domain;
using TAOM.Features.Enlistment.Hooks;
using TAOM.Features.Enlistment.Presentation;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// #1099: a conversation that ends inside the player's own battle must leave the encounter menu on screen.
/// MapEventManager.Tick skips that battle, so PlayerEncounter.Update, driven from the menu, is all that closes it.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class EnlistmentConversationEndTests
{
    private EnlistmentStore _store = null!;
    private IServiceAttachmentService _attachment = null!;
    private IGameMenuAdapter _gameMenu = null!;
    private EnlistmentMenuBehavior _behavior = null!;

    [TestInitialize]
    public void Setup()
    {
        _store = new EnlistmentStore(Substitute.For<IModLogger>());
        _store.Record.State = EnlistmentState.EnlistedAttached;
        _store.Record.EnlistedHeroId = "main_hero";
        _store.Record.CommanderHeroId = "lord_1_1";

        _attachment = Substitute.For<IServiceAttachmentService>();
        _gameMenu = Substitute.For<IGameMenuAdapter>();
        _gameMenu.CurrentMenuId.Returns("encounter");

        var coopSession = Substitute.For<ICoopSessionProvider>();
        coopSession.IsAuthority.Returns(true);

        _behavior = new EnlistmentMenuBehavior(
            _store,
            _attachment,
            Substitute.For<IEnlistmentWaitMenuPresenter>(),
            _gameMenu,
            coopSession,
            Substitute.For<IServiceMaintenanceService>(),
            Substitute.For<IEnlistmentWaitMenuOptions>());
    }

    private void PlayerInMapEvent(bool inMapEvent) =>
        _attachment.GetPresenceFlags().Returns(new PlayerPresenceFlags(mainPartyExists: true, isInMapEvent: inMapEvent));

    [TestMethod]
    public void OnConversationEnded_PlayerInMapEvent_LeavesTheBattleMenu()
    {
        PlayerInMapEvent(true);

        _behavior.OnConversationEnded(null!);

        _gameMenu.DidNotReceive().Activate(Arg.Any<string>());
    }

    [TestMethod]
    public void OnConversationEnded_PlayerNotInMapEvent_RestoresTheServiceMenu()
    {
        PlayerInMapEvent(false);

        _behavior.OnConversationEnded(null!);

        _gameMenu.Received(1).Activate(EnlistmentMenuService.ServiceWaitMenuId);
    }
}
