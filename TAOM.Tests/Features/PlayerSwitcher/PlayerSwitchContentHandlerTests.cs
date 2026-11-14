using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
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
    private TAOM.Features.CareerSystem.ICareerDataService _careerData = null!;
    private IPlayerPartyAdapter _mainHero = null!;
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
        _careerData = Substitute.For<TAOM.Features.CareerSystem.ICareerDataService>();
        _mainHero = Substitute.For<IPlayerPartyAdapter>();
        _mainHero.GetMainHeroId().Returns("main_hero");
        _sut = new PlayerSwitchContentHandler(
            _switchService, new SwitchPlanner(), _session, _session, _policy,
            _careerData, _mainHero, Substitute.For<IInquiryAdapter>(), _identity,
            Substitute.For<IModLogger>());
        _session.Select(new HeroPickRow("lord_1_75", "Boromir", HeroPickerGroup.RulingHouse, 0, false, false, true));
    }

    // #1217: with no career-menu pick, TAOM's 1050 handler auto-assigns the culture's first career
    // to the created hero, so the plan carries the career that hero holds, not the menu's.
    [TestMethod]
    public void OnCharacterCreationFinalize_NoMenuPickAndAnAutoAssignedCareer_PlansThatCareer()
    {
        _careerData.GetCareerStringId("main_hero").Returns("avanc_luth_raider");

        _sut.OnCharacterCreationFinalize(null!);

        _switchService.Received(1).Execute(Arg.Is<SwitchPlan>(p => p.CareerId == "avanc_luth_raider"));
    }

    [TestMethod]
    public void OnCharacterCreationFinalize_CreatedHeroHoldsNoCareer_PlansNoCareer()
    {
        _careerData.GetCareerStringId("main_hero").Returns((string)null);

        _sut.OnCharacterCreationFinalize(null!);

        _switchService.Received(1).Execute(Arg.Is<SwitchPlan>(p => p.CareerId == string.Empty));
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
