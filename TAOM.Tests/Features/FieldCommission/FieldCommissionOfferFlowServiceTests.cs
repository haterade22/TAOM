using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.FieldCommission;
using TAOM.Features.FieldCommission.Domain;

namespace TAOM.Tests.Features.FieldCommission;

[TestClass]
public class FieldCommissionOfferFlowServiceTests
{
    private IFieldCommissionMeritService _merit = null!;
    private ITroopRosterQueryAdapter _roster = null!;
    private IHeroCommissionAdapter _heroCommission = null!;
    private IInquiryPresenterAdapter _presenter = null!;
    private IFieldCommissionConfigProvider _configProvider = null!;
    private IModLogger _logger = null!;
    private FieldCommissionConfig _config = null!;
    private FieldCommissionOfferFlowService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _merit = Substitute.For<IFieldCommissionMeritService>();
        _roster = Substitute.For<ITroopRosterQueryAdapter>();
        _heroCommission = Substitute.For<IHeroCommissionAdapter>();
        _presenter = Substitute.For<IInquiryPresenterAdapter>();
        _configProvider = Substitute.For<IFieldCommissionConfigProvider>();
        _logger = Substitute.For<IModLogger>();

        _config = new FieldCommissionConfig { SkillPointsPerLevel = 5, RetainerAllowance = 0 };
        _configProvider.GetConfig().Returns(_config);

        _sut = new FieldCommissionOfferFlowService(
            _merit, _roster, _heroCommission, _presenter, _configProvider, new CommissionSkillBudget(), _logger);
    }

    private static PendingPromotionOffer Offer() => new PendingPromotionOffer("troop_a", "Troop A");

    private void SetupDequeue() =>
        _merit.TryDequeueOffer(out Arg.Any<PendingPromotionOffer>()).Returns(x => { x[0] = Offer(); return true; });

    private Action CapturePromoteDecline()
    {
        SetupDequeue();
        Action decline = null;
        _presenter.ShowPromotionOffer(Arg.Any<string>(), Arg.Any<Action>(), Arg.Do<Action>(a => decline = a));
        _sut.PumpNextOffer();
        return decline;
    }

    private Action CapturePromoteAccept()
    {
        SetupDequeue();
        Action accept = null;
        _presenter.ShowPromotionOffer(Arg.Any<string>(), Arg.Do<Action>(a => accept = a), Arg.Any<Action>());
        _sut.PumpNextOffer();
        return accept;
    }

    [TestMethod]
    public void PumpNextOffer_NoPendingOffers_DoesNothing()
    {
        _merit.TryDequeueOffer(out Arg.Any<PendingPromotionOffer>()).Returns(false);

        _sut.PumpNextOffer();

        _presenter.DidNotReceiveWithAnyArgs().ShowPromotionOffer(null, null, null);
        Assert.IsFalse(_sut.IsShowingOffer);
    }

    [TestMethod]
    public void PumpNextOffer_AlreadyShowingOffer_DoesNotDequeueAnother()
    {
        SetupDequeue();

        _sut.PumpNextOffer();
        _sut.PumpNextOffer();

        _merit.Received(1).TryDequeueOffer(out Arg.Any<PendingPromotionOffer>());
    }

    [TestMethod]
    public void PumpNextOffer_DequeuesOfferAndShowsPromotionOffer_SetsIsShowingOffer()
    {
        SetupDequeue();

        _sut.PumpNextOffer();

        _presenter.Received(1).ShowPromotionOffer("Troop A", Arg.Any<Action>(), Arg.Any<Action>());
        Assert.IsTrue(_sut.IsShowingOffer);
    }

    [TestMethod]
    public void Decline_ClosesOfferWithoutCreatingHeroOrCompletingMerit()
    {
        var decline = CapturePromoteDecline();

        decline();

        _heroCommission.DidNotReceiveWithAnyArgs().CreateCompanionFromTroop(null, null, null);
        _merit.DidNotReceiveWithAnyArgs().CompleteOffer(null);
        Assert.IsFalse(_sut.IsShowingOffer);
    }

    [TestMethod]
    public void Accept_NoCompanionRoomAndNoRetainerAllowance_ShowsNoRoomAndDoesNotCreateHero()
    {
        _heroCommission.GetCompanionRoomInfo().Returns(new CompanionRoomInfo(5, 5)); // full, allowance 0
        var accept = CapturePromoteAccept();

        accept();

        _presenter.Received(1).ShowNoCompanionRoom("Troop A", Arg.Any<Action>());
        _heroCommission.DidNotReceiveWithAnyArgs().CreateCompanionFromTroop(null, null, null);
        _merit.DidNotReceiveWithAnyArgs().CompleteOffer(null);
    }

    [TestMethod]
    public void Accept_NoCompanionRoomAcknowledged_ClosesOffer()
    {
        _heroCommission.GetCompanionRoomInfo().Returns(new CompanionRoomInfo(5, 5));

        Action acknowledge = null;
        _presenter.ShowNoCompanionRoom(Arg.Any<string>(), Arg.Do<Action>(a => acknowledge = a));

        var accept = CapturePromoteAccept();
        accept();
        acknowledge();

        Assert.IsFalse(_sut.IsShowingOffer);
    }

    [TestMethod]
    public void Accept_RoomFullButRetainerAllowanceCoversIt_ProceedsToRename()
    {
        _config.RetainerAllowance = 2;
        _heroCommission.GetCompanionRoomInfo().Returns(new CompanionRoomInfo(5, 5)); // full base limit, +2 allowance covers it
        var accept = CapturePromoteAccept();

        accept();

        _presenter.Received(1).ShowRenamePrompt("Troop A", Arg.Any<Action<string>>(), Arg.Any<Action>());
    }

    [TestMethod]
    public void Accept_WithRoom_ShowsRenamePromptPrefilledWithTroopName()
    {
        _heroCommission.GetCompanionRoomInfo().Returns(new CompanionRoomInfo(0, 5));
        var accept = CapturePromoteAccept();

        accept();

        _presenter.Received(1).ShowRenamePrompt("Troop A", Arg.Any<Action<string>>(), Arg.Any<Action>());
    }

    private Action<string> CaptureRenameConfirm()
    {
        _heroCommission.GetCompanionRoomInfo().Returns(new CompanionRoomInfo(0, 5));

        Action<string> confirm = null;
        _presenter.ShowRenamePrompt(Arg.Any<string>(), Arg.Do<Action<string>>(a => confirm = a), Arg.Any<Action>());

        var accept = CapturePromoteAccept();
        accept();
        return confirm;
    }

    [TestMethod]
    public void RenameConfirm_HeroCreated_CompletesOfferRemovesRosterAndRecordsHero()
    {
        var confirm = CaptureRenameConfirm();
        _roster.GetTroopInfo("troop_a").Returns(new TroopInfo("troop_a", "Troop A", false, false, 0, 12));
        _roster.GetTroopSkillValues("troop_a").Returns(new Dictionary<string, int> { ["OneHanded"] = 40 });
        _heroCommission.CreateCompanionFromTroop("troop_a", Arg.Any<CommissionSkillPlan>(), "Sir Chosen").Returns("hero_1");

        confirm("Sir Chosen");

        _heroCommission.Received(1).CreateCompanionFromTroop(
            "troop_a",
            Arg.Is<CommissionSkillPlan>(p => p.HeroLevel == 12 && p.SkillValues["OneHanded"] == 40),
            "Sir Chosen");
        _roster.Received(1).RemoveOneFromRoster("troop_a");
        _merit.Received(1).CompleteOffer("troop_a");
        _merit.Received(1).RecordPromotedHero("hero_1");
        Assert.IsFalse(_sut.IsShowingOffer);
    }

    [TestMethod]
    public void RenameConfirm_BlankInput_FallsBackToTroopName()
    {
        var confirm = CaptureRenameConfirm();
        _roster.GetTroopInfo("troop_a").Returns(new TroopInfo("troop_a", "Troop A", false, false, 0, 10));
        _roster.GetTroopSkillValues("troop_a").Returns(new Dictionary<string, int>());
        _heroCommission.CreateCompanionFromTroop("troop_a", Arg.Any<CommissionSkillPlan>(), "Troop A").Returns("hero_1");

        confirm("   ");

        _heroCommission.Received(1).CreateCompanionFromTroop("troop_a", Arg.Any<CommissionSkillPlan>(), "Troop A");
    }

    [TestMethod]
    public void RenameSkip_CreatesCompanionWithTroopNameFallback()
    {
        _heroCommission.GetCompanionRoomInfo().Returns(new CompanionRoomInfo(0, 5));
        _roster.GetTroopInfo("troop_a").Returns(new TroopInfo("troop_a", "Troop A", false, false, 0, 10));
        _roster.GetTroopSkillValues("troop_a").Returns(new Dictionary<string, int>());
        _heroCommission.CreateCompanionFromTroop("troop_a", Arg.Any<CommissionSkillPlan>(), "Troop A").Returns("hero_1");

        Action skip = null;
        _presenter.ShowRenamePrompt(Arg.Any<string>(), Arg.Any<Action<string>>(), Arg.Do<Action>(a => skip = a));

        var accept = CapturePromoteAccept();
        accept();
        skip();

        _heroCommission.Received(1).CreateCompanionFromTroop("troop_a", Arg.Any<CommissionSkillPlan>(), "Troop A");
        _merit.Received(1).CompleteOffer("troop_a");
    }

    [TestMethod]
    public void RenameConfirm_HeroCreationFails_DoesNotCompleteOfferOrTouchRoster()
    {
        var confirm = CaptureRenameConfirm();
        _roster.GetTroopInfo("troop_a").Returns(new TroopInfo("troop_a", "Troop A", false, false, 0, 10));
        _roster.GetTroopSkillValues("troop_a").Returns(new Dictionary<string, int>());
        _heroCommission.CreateCompanionFromTroop("troop_a", Arg.Any<CommissionSkillPlan>(), Arg.Any<string>()).Returns((string)null);

        confirm("Sir Chosen");

        _roster.DidNotReceiveWithAnyArgs().RemoveOneFromRoster(null);
        _merit.DidNotReceiveWithAnyArgs().CompleteOffer(null);
        _merit.DidNotReceiveWithAnyArgs().RecordPromotedHero(null);
        Assert.IsFalse(_sut.IsShowingOffer);
    }
}
