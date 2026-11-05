using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.CoopInterop;
using TAOM.Features.Enlistment;
using TAOM.Features.Enlistment.Domain;
using TAOM.Features.Enlistment.Hooks;
using TAOM.Features.Enlistment.Presentation;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// The arrival offer is once per stop (#656, maintainer decision 6), and a stop ends when the column
/// leaves the town. The exit sweep (<c>ServiceAttachmentService.ExitSettlementForService</c>) is not
/// enough on its own: a shore-leave pass suspends that sweep, and the player then walks out through
/// the vanilla Leave option, so an accepted offer never reached it and the next stop in the same
/// town stayed silent. The commander's settlement-left edge covers every way the player leaves.
/// </summary>
[TestClass]
public class EnlistmentStopEndTests
{
    private const string Commander = "lord_1_1";

    private sealed class ReachedTheStopEnd : System.Exception { }

    private static EnlistmentMaintenanceBehavior NewBehavior(
        IEnlistmentWaitMenuPresenter presenter, bool isAuthority = true)
    {
        var store = Substitute.For<IEnlistmentStore>();
        store.Record.Returns(new EnlistmentRecord { CommanderHeroId = Commander });
        var coop = Substitute.For<ICoopSessionProvider>();
        coop.IsAuthority.Returns(isAuthority);
        return new EnlistmentMaintenanceBehavior(
            Substitute.For<IServiceMaintenanceService>(),
            Substitute.For<IEnlistmentReconciler>(),
            store,
            coop,
            presenter);
    }

    [TestMethod]
    public void CommanderLeavesTheSettlement_EndsTheStop_BeforeTheReconcile()
    {
        // The reconcile reads CampaignTime.Now, which needs a live campaign, so the presenter call
        // throws a sentinel: reaching it proves the stop ended on the commander's edge, and before
        // the engine read.
        var presenter = Substitute.For<IEnlistmentWaitMenuPresenter>();
        presenter.When(p => p.OnStopEnded()).Do(_ => throw new ReachedTheStopEnd());
        var sut = NewBehavior(presenter);

        Assert.ThrowsException<ReachedTheStopEnd>(() => sut.OnPartyLeftSettlement(Commander),
            "the commander left the town and the offer's settlement latch was not cleared");
    }

    [TestMethod]
    public void AnotherPartyLeavesTheSettlement_DoesNotEndTheStop()
    {
        var presenter = Substitute.For<IEnlistmentWaitMenuPresenter>();
        var sut = NewBehavior(presenter);

        sut.OnPartyLeftSettlement("lord_2_1");
        sut.OnPartyLeftSettlement(null!);

        presenter.DidNotReceive().OnStopEnded();
    }

    [TestMethod]
    public void OnACoopClient_TheCommanderLeaving_DoesNotEndTheStop()
    {
        // The offer and its latch are host-only (OfferTownLeave returns on a client), like the
        // reconcile this edge drives.
        var presenter = Substitute.For<IEnlistmentWaitMenuPresenter>();
        var sut = NewBehavior(presenter, isAuthority: false);

        sut.OnPartyLeftSettlement(Commander);

        presenter.DidNotReceive().OnStopEnded();
    }

    [TestMethod]
    public void BothStopEndEdges_AreWiredToTheHooks()
    {
        // A source-presence pin: RegisterEvents reads CampaignEvents.Instance (a live campaign),
        // and the engine handler takes a MobileParty, so neither wiring line can run in a test.
        // Comment lines are ignored, so a commented-out line fails.
        AssertHasCodeLine(
            RepoPath("Main", "Features", "Enlistment", "Hooks", "EnlistmentMaintenanceBehavior.cs"),
            "CampaignEvents.OnSettlementLeftEvent.AddNonSerializedListener(this, OnSettlementLeft);");
        AssertHasCodeLine(
            RepoPath("Main", "Features", "Enlistment", "Hooks", "EnlistmentMaintenanceBehavior.cs"),
            "OnPartyLeftSettlement(party?.LeaderHero?.StringId)");
        AssertHasCodeLine(
            RepoPath("Main", "Features", "Enlistment", "Hooks", "EnlistmentMenuBehavior.cs"),
            "_attachment.ColumnLeftSettlement += _presenter.OnStopEnded;");
    }

    private static void AssertHasCodeLine(string path, string fragment)
    {
        var found = File.ReadAllLines(path)
            .Where(line => !line.TrimStart().StartsWith("//", System.StringComparison.Ordinal))
            .Any(line => line.Contains(fragment));
        Assert.IsTrue(found, $"{Path.GetFileName(path)} no longer contains: {fragment}");
    }
}
