using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CharacterCreation;
using TAOM.Features.PlayerSwitcher;
using TAOM.Features.PlayerSwitcher.Domain;

namespace TAOM.Tests.Features.PlayerSwitcher;

/// <summary>
/// The 1100 handler records what the handover did for the listeners that run after character
/// creation. Since 2026-10-02 that includes the gold the hero held when the handover finished: the
/// engine assigns the player 1,000 gold right after every handler, and TakeoverTreasuryService puts the
/// lord's own treasury back from this record.
/// </summary>
[TestClass]
public class PlayerSwitchContentHandlerTests
{
    private IHeroSwitchService _switchService = null!;
    private PlayerSwitchSessionStore _session = null!;
    private IPlayerSwitchPolicyProvider _policy = null!;
    private IPlayerIdentityAdapter _identity = null!;
    private PlayerSwitchContentHandler _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _switchService = Substitute.For<IHeroSwitchService>();
        _session = new PlayerSwitchSessionStore();
        _policy = Substitute.For<IPlayerSwitchPolicyProvider>();
        _policy.Current.Returns(PlayerSwitchPolicy.Default);
        _identity = Substitute.For<IPlayerIdentityAdapter>();
        _identity.GetGold("lord_1_75").Returns(18500);
        _sut = new PlayerSwitchContentHandler(
            _switchService, new SwitchPlanner(), _session, _session, _policy,
            Substitute.For<ICareerMenuService>(), Substitute.For<IInquiryAdapter>(), _identity,
            Substitute.For<IModLogger>());
        _session.Select(new HeroPickRow("lord_1_75", "Boromir", HeroPickerGroup.RulingHouse, 0, false, false, true));
    }

    [DataTestMethod]
    [DataRow(SwitchOutcome.Switched)]
    [DataRow(SwitchOutcome.SwitchedWithErrors)]
    public void OnCharacterCreationFinalize_AfterAHandover_RecordsTheGoldTheHeroHolds(SwitchOutcome outcome)
    {
        _switchService.Execute(Arg.Any<SwitchPlan>()).Returns(outcome);

        _sut.OnCharacterCreationFinalize(null!);

        Assert.AreEqual(18500, _session.LastHeroGold);
        Assert.AreEqual("lord_1_75", _session.LastSwitchedHeroId);
    }

    [DataTestMethod]
    [DataRow(SwitchOutcome.NotAttempted)]
    [DataRow(SwitchOutcome.Blocked)]
    [DataRow(SwitchOutcome.Failed)]
    public void OnCharacterCreationFinalize_WhenThePlayerKeepsTheirCharacter_RecordsNoGold(SwitchOutcome outcome)
    {
        _switchService.Execute(Arg.Any<SwitchPlan>()).Returns(outcome);

        _sut.OnCharacterCreationFinalize(null!);

        Assert.AreEqual(-1, _session.LastHeroGold);
        _identity.DidNotReceiveWithAnyArgs().GetGold(default!);
    }
}
