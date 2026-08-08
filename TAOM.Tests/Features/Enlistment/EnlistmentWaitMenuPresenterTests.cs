using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.CoopInterop;
using TAOM.Features.Enlistment;
using TAOM.Features.Enlistment.Domain;
using TAOM.Features.Enlistment.Hooks;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// The release flow the player actually touches. The presenter had no tests at all before this
/// batch, which is how "ask to be released" shipped as an unannounced instant discharge.
/// </summary>
[TestClass]
public class EnlistmentWaitMenuPresenterTests
{
    private IEnlistmentStore _store;
    private ICommanderLordAdapter _commander;
    private IEnlistmentDialogGateService _gate;
    private IEnlistmentService _service;
    private IInquiryAdapter _inquiry;
    private ICoopSessionProvider _coop;
    private EnlistmentWaitMenuPresenter _sut;

    [TestInitialize]
    public void Setup()
    {
        _store = Substitute.For<IEnlistmentStore>();
        _commander = Substitute.For<ICommanderLordAdapter>();
        _gate = Substitute.For<IEnlistmentDialogGateService>();
        _service = Substitute.For<IEnlistmentService>();
        _inquiry = Substitute.For<IInquiryAdapter>();
        _coop = Substitute.For<ICoopSessionProvider>();

        _store.Record.Returns(new EnlistmentRecord());
        _coop.IsAuthority.Returns(true);
        _gate.EvaluateReleaseRequest(Arg.Any<double>()).Returns(ReleaseRequest.Granted);
        _gate.ClassifyLeaveReason(Arg.Any<double>()).Returns(DischargeReason.PlayerRequest);

        _sut = new EnlistmentWaitMenuPresenter(_store, _commander, _gate, _service, _inquiry, _coop,
            Substitute.For<IServiceStatusService>());
    }

    [TestMethod]
    public void RequestRelease_TermServed_DischargesHonourably_NoPopup()
    {
        _sut.RequestRelease(50.0);

        _service.Received(1).RequestDischarge(DischargeReason.PlayerRequest);
        _inquiry.DidNotReceiveWithAnyArgs().ShowTwoOptionInquiry(
            default, default, default, default, default, default, default, default, default, default);
    }

    [TestMethod]
    public void RequestRelease_InBattle_ShowsMessageAndDoesNotDischarge()
    {
        _gate.EvaluateReleaseRequest(Arg.Any<double>()).Returns(ReleaseRequest.RefusedInBattle);

        _sut.RequestRelease(50.0);

        _service.DidNotReceiveWithAnyArgs().RequestDischarge(default);
        _inquiry.ReceivedWithAnyArgs(1).ShowMessage(default, default, default, default);
    }

    [TestMethod]
    public void RequestRelease_TooSoon_PromptsWithTheRealDayCount_AndDoesNotDischargeYet()
    {
        _gate.EvaluateReleaseRequest(Arg.Any<double>()).Returns(ReleaseRequest.TooSoon(18));

        _sut.RequestRelease(3.0);

        // Nothing happens until the player answers — the popup is the decision point.
        _service.DidNotReceiveWithAnyArgs().RequestDischarge(default);

        // The number has to reach the popup, or the warning is unactionable.
        _inquiry.Received(1).ShowTwoOptionInquiry(
            Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<Action>(), Arg.Any<Action>(),
            "DAYS", "18");
    }

    [TestMethod]
    public void RequestRelease_TooSoon_ConfirmingDesertion_DischargesAsDesertion()
    {
        _gate.EvaluateReleaseRequest(Arg.Any<double>()).Returns(ReleaseRequest.TooSoon(18));
        Action confirm = null;
        _inquiry.WhenForAnyArgs(a => a.ShowTwoOptionInquiry(
                default, default, default, default, default, default, default, default, default, default))
            .Do(call => confirm = call.ArgAt<Action>(8));

        _sut.RequestRelease(3.0);
        confirm();

        _service.Received(1).RequestDischarge(DischargeReason.Desertion);
    }

    [TestMethod]
    public void RequestRelease_TooSoon_DecliningLeavesPlayerEnlisted()
    {
        // "Stay and serve" is wired to no callback at all — the proof that choosing it cannot
        // discharge is that the negative action is null, not that it does something harmless.
        _gate.EvaluateReleaseRequest(Arg.Any<double>()).Returns(ReleaseRequest.TooSoon(18));
        Action decline = null;
        _inquiry.WhenForAnyArgs(a => a.ShowTwoOptionInquiry(
                default, default, default, default, default, default, default, default, default, default))
            .Do(call => decline = call.ArgAt<Action>(9));

        _sut.RequestRelease(3.0);

        Assert.IsNull(decline);
        _service.DidNotReceiveWithAnyArgs().RequestDischarge(default);
    }

    [TestMethod]
    public void RequestRelease_NonAuthority_DoesNothing()
    {
        _coop.IsAuthority.Returns(false);

        _sut.RequestRelease(50.0);

        _service.DidNotReceiveWithAnyArgs().RequestDischarge(default);
        _inquiry.DidNotReceiveWithAnyArgs().ShowTwoOptionInquiry(
            default, default, default, default, default, default, default, default, default, default);
    }

    [TestMethod]
    public void RequestRelease_RefusedConfirm_NonAuthority_DoesNotDischarge()
    {
        // THE pin for this batch. The confirmation callback runs on a LATER frame, outside the
        // menu-option consequence that checked authority — so the check at the option site does
        // not cover the moment the discharge actually runs. Authority can be lost in between.
        _gate.EvaluateReleaseRequest(Arg.Any<double>()).Returns(ReleaseRequest.TooSoon(18));
        Action confirm = null;
        _inquiry.WhenForAnyArgs(a => a.ShowTwoOptionInquiry(
                default, default, default, default, default, default, default, default, default, default))
            .Do(call => confirm = call.ArgAt<Action>(8));

        _sut.RequestRelease(3.0);
        _coop.IsAuthority.Returns(false);
        confirm();

        _service.DidNotReceiveWithAnyArgs().RequestDischarge(default);
    }
}
