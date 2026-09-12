using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment;
using TAOM.Features.Enlistment.Domain;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// The enlisted verdict for the Order of Battle deployment screen (#576). The service may only
/// ever CLOSE the screen: it answers false when the player is an enlisted soldier in someone
/// else's battle and null (vanilla decides) for everything else, including the player's own
/// battles. It is the third consumer of <see cref="BattleCommandPolicy.ShouldStripPlayerCommand"/>
/// and the matrix test at the bottom pins that it cannot gate apart from the role strip.
/// </summary>
[TestClass]
public class EnlistmentDeploymentServiceTests
{
    private IEnlistmentStateQuery _query = null!;
    private IEncounterAdapter _encounter = null!;
    private IModLogger _logger = null!;
    private EnlistmentDeploymentService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _query = Substitute.For<IEnlistmentStateQuery>();
        _encounter = Substitute.For<IEncounterAdapter>();
        _logger = Substitute.For<IModLogger>();
        _service = new EnlistmentDeploymentService(_query, _encounter, _logger);
    }

    private bool? Ask(EnlistmentState state, bool? leads)
    {
        _query.State.Returns(state);
        _encounter.IsMainPartyLeadingItsBattleSide.Returns(leads);
        return _service.CanPlayerSideDeployWithOrderOfBattle();
    }

    [TestMethod]
    public void CanPlayerSideDeployWithOrderOfBattle_EnlistedBattle_NotLeadingSide_ReturnsFalse()
        => Assert.AreEqual(false, Ask(EnlistmentState.EnlistedBattle, leads: false));

    [TestMethod]
    public void CanPlayerSideDeployWithOrderOfBattle_EnlistedBattle_LeadingSide_ReturnsNull()
        // Never strip the actual leader of the side, and never take his deployment screen either.
        => Assert.IsNull(Ask(EnlistmentState.EnlistedBattle, leads: true));

    [TestMethod]
    public void CanPlayerSideDeployWithOrderOfBattle_NotEnlisted_ReturnsNull()
        => Assert.IsNull(Ask(EnlistmentState.NotEnlisted, leads: false));

    [TestMethod]
    public void CanPlayerSideDeployWithOrderOfBattle_EnlistedAttached_ReturnsNull()
        => Assert.IsNull(Ask(EnlistmentState.EnlistedAttached, leads: false));

    [TestMethod]
    public void CanPlayerSideDeployWithOrderOfBattle_DetachedOnDuty_ReturnsNull()
        => Assert.IsNull(Ask(EnlistmentState.EnlistedDetachedOnDuty, leads: false));

    [TestMethod]
    public void CanPlayerSideDeployWithOrderOfBattle_NoMapEvent_ReturnsNull()
        // The adapter reports null when the main party is in no map event at all. The same early
        // return the two mission behaviors take; there is nothing to decide about.
        => Assert.IsNull(Ask(EnlistmentState.EnlistedBattle, leads: null));

    [TestMethod]
    public void CanPlayerSideDeployWithOrderOfBattle_NeverReturnsTrue()
    {
        foreach (EnlistmentState state in Enum.GetValues(typeof(EnlistmentState)))
        {
            foreach (var leads in new bool?[] { true, false, null })
            {
                Assert.AreNotEqual(true, Ask(state, leads),
                    $"The service opened the deployment screen for state {state}, leads={leads}. It may only ever close it; opening is vanilla's call.");
            }
        }
    }

    [TestMethod]
    public void CanPlayerSideDeployWithOrderOfBattle_MatchesShouldStripPlayerCommand_ForEveryStateAndSide()
    {
        // The cannot-gate-apart pin. If the screen ever opened where the role strip fires, the
        // sergeant-choice UI would offer the player a formation that the strip then leaves with
        // nobody to command it (#576).
        foreach (EnlistmentState state in Enum.GetValues(typeof(EnlistmentState)))
        {
            foreach (var leads in new[] { true, false })
            {
                bool? expected = BattleCommandPolicy.ShouldStripPlayerCommand(state, leads) ? false : null;
                Assert.AreEqual(expected, Ask(state, leads),
                    $"Deployment gate and role strip disagree for state {state}, leads={leads}.");
            }
        }
    }

    [TestMethod]
    public void CanPlayerSideDeployWithOrderOfBattle_Suppressed_LogsTheProofLine()
    {
        Ask(EnlistmentState.EnlistedBattle, leads: false);

        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("Order of Battle deployment suppressed")));
    }

    [TestMethod]
    public void CanPlayerSideDeployWithOrderOfBattle_NotEnlisted_LogsNothing()
    {
        Ask(EnlistmentState.NotEnlisted, leads: false);

        _logger.DidNotReceive().LogInfo(Arg.Any<string>());
    }

    [TestMethod]
    public void CanPlayerSideDeployWithOrderOfBattle_EnlistedButVanillaDecides_LogsWhyOnce()
    {
        // Enlisted but leading the side is unusual enough to name in the log, so a report can tell
        // "the model stood aside" from "the model never ran".
        Ask(EnlistmentState.EnlistedBattle, leads: true);

        Assert.AreEqual(1, _logger.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IModLogger.LogInfo)));
    }
}
