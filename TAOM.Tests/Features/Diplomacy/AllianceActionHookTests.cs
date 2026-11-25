using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.Diplomacy;
using TAOM.Features.Diplomacy.Hooks;
using TAOM.Features.Diplomacy.Models;

namespace TAOM.Tests.Features.Diplomacy;

[TestClass]
public class AllianceActionHookTests
{
    private IDiplomacyService _diplomacyService;
    private IWarOfTheRingService _wotr;
    private IModLogger _logger;
    private ICoopSessionProvider _coop;
    private AllianceActionHook _sut;

    [TestInitialize]
    public void Setup()
    {
        _diplomacyService = Substitute.For<IDiplomacyService>();
        _wotr = Substitute.For<IWarOfTheRingService>();
        _logger = Substitute.For<IModLogger>();
        _coop = Substitute.For<ICoopSessionProvider>();
        _coop.ShouldDeferToHost.Returns(false);
        _sut = new AllianceActionHook(_diplomacyService, _wotr, _logger, _coop);
    }

    [TestMethod]
    public void ShouldPreventAllianceEnd_PermanentAllies_ReturnsTrue()
    {
        _diplomacyService.GetRelationshipTier("empire_w", "vlandia")
            .Returns(AllianceTier.Permanent);

        Assert.IsTrue(_sut.ShouldPreventAllianceEnd("empire_w", "vlandia"));
    }

    // #1105 slice 5: EndPermanentAlliance records the pair before it asks the engine, so the real service answers Neutral and the
    // prefix lets the engine's EndAlliance through.
    [TestMethod]
    public void ShouldPreventAllianceEnd_ABrokenPermanentPair_ReturnsFalse()
    {
        var provider = Substitute.For<IDiplomacyConfigProvider>();
        provider.LoadConfig().Returns(new DiplomacyConfig
        {
            Relationships = new System.Collections.Generic.List<KingdomRelationship>
            {
                new() { KingdomA = "isengard", KingdomB = "empire_s", Tier = AllianceTier.Permanent },
            },
        });
        var service = new DiplomacyService(provider, Substitute.For<TAOM.Adapters.IAllianceAdapter>(), Substitute.For<TAOM.Features.Execution.IAlignmentService>(), _logger);
        var sut = new AllianceActionHook(service, _wotr, _logger, _coop);
        Assert.IsTrue(sut.ShouldPreventAllianceEnd("isengard", "empire_s"), "before the end");

        service.EndPermanentAlliance("isengard", "empire_s", "test");

        Assert.IsFalse(sut.ShouldPreventAllianceEnd("empire_s", "isengard"));
    }

    [TestMethod]
    public void ShouldPreventAllianceEnd_NaturalAllies_ReturnsFalse()
    {
        _diplomacyService.GetRelationshipTier("erebor", "mirkwood")
            .Returns(AllianceTier.Natural);

        Assert.IsFalse(_sut.ShouldPreventAllianceEnd("erebor", "mirkwood"));
    }

    [TestMethod]
    public void ShouldPreventAllianceEnd_NeutralKingdoms_ReturnsFalse()
    {
        _diplomacyService.GetRelationshipTier("battania", "aserai")
            .Returns(AllianceTier.Neutral);

        Assert.IsFalse(_sut.ShouldPreventAllianceEnd("battania", "aserai"));
    }

    [DataTestMethod]
    [DataRow(WarVeto.Permanent)]
    [DataRow(WarVeto.SameAlignment)]
    [DataRow(WarVeto.PhasePeace)]
    [DataRow(WarVeto.PhaseNotScripted)]
    public void ShouldPreventWarDeclaration_AnyVeto_ReturnsTrue(WarVeto veto)
    {
        _wotr.GetWarVeto("isengard", "rohan", false).Returns(veto);

        Assert.IsTrue(_sut.ShouldPreventWarDeclaration("isengard", "rohan"));
    }

    [TestMethod]
    public void ShouldPreventWarDeclaration_NoVeto_ReturnsFalse()
    {
        _wotr.GetWarVeto("isengard", "rohan", false).Returns(WarVeto.None);

        Assert.IsFalse(_sut.ShouldPreventWarDeclaration("isengard", "rohan"));
    }

    [TestMethod]
    public void ShouldPreventWarDeclaration_PlayerCaused_AsksTheServiceForThePlayerVeto()
    {
        _wotr.GetWarVeto("gondor", "isengard", false).Returns(WarVeto.PhasePeace);
        _wotr.GetWarVeto("gondor", "isengard", true).Returns(WarVeto.None);

        Assert.IsFalse(_sut.ShouldPreventWarDeclaration("gondor", "isengard", causedByPlayer: true));
        Assert.IsTrue(_sut.ShouldPreventWarDeclaration("gondor", "isengard", causedByPlayer: false));
    }

    [TestMethod]
    public void ShouldPreventWarDeclaration_PlayerCausedLoreVeto_ReturnsTrue()
    {
        _wotr.GetWarVeto("rivendell", "lothlorien", true).Returns(WarVeto.Permanent);

        Assert.IsTrue(_sut.ShouldPreventWarDeclaration("rivendell", "lothlorien", causedByPlayer: true));
    }

    [TestMethod]
    public void ShouldPreventWarDeclaration_PlayerCausedCoopDefer_ReturnsFalseWithoutConsultingTheService()
    {
        _coop.ShouldDeferToHost.Returns(true);

        Assert.IsFalse(_sut.ShouldPreventWarDeclaration("rivendell", "lothlorien", causedByPlayer: true));
        _wotr.DidNotReceive().GetWarVeto(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>());
    }

    // --- GetWarRefusalText: the permission model's single call ---------------------------------

    [DataTestMethod]
    [DataRow(WarVeto.Permanent)]
    [DataRow(WarVeto.SameAlignment)]
    [DataRow(WarVeto.PhasePeace)]
    [DataRow(WarVeto.PhaseNotScripted)]
    public void GetWarRefusalText_AnyVeto_ReturnsThatVetosText(WarVeto veto)
    {
        _wotr.GetWarVeto("isengard", "rohan", false).Returns(veto);

        Assert.AreEqual(WarVetoReasons.Text(veto), _sut.GetWarRefusalText("isengard", "rohan"));
    }

    [TestMethod]
    public void GetWarRefusalText_NoVeto_ReturnsNull()
    {
        _wotr.GetWarVeto("isengard", "rohan", false).Returns(WarVeto.None);

        Assert.IsNull(_sut.GetWarRefusalText("isengard", "rohan"));
    }

    [TestMethod]
    public void GetWarRefusalText_CoopDefer_ReturnsNullWithoutConsultingTheService()
    {
        _coop.ShouldDeferToHost.Returns(true);
        _wotr.GetWarVeto("isengard", "rohan", false).Returns(WarVeto.PhasePeace);

        Assert.IsNull(_sut.GetWarRefusalText("isengard", "rohan"));
        _wotr.DidNotReceive().GetWarVeto(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>());
    }

    [TestMethod]
    public void GetWarRefusalText_Vetoed_LogsNothingAtInfo()
    {
        // The engine asks for every AI proposal; an Info line per ask would flood the log.
        _wotr.GetWarVeto("isengard", "rohan", false).Returns(WarVeto.PhasePeace);

        _sut.GetWarRefusalText("isengard", "rohan");

        _logger.DidNotReceive().LogInfo(Arg.Any<string>());
    }

    // --- Co-op interop (#370) -------------------------------------------------------------
    // Under a co-op session the host's diplomacy is authoritative. If TAOM vetoes a war that the
    // host already applied, the client ends up at peace while the host is at war — a silent,
    // unattributable save divergence. TAOM's prefix is Priority.High and runs BEFORE
    // BannerlordTogether's own suppression prefix, so the veto MUST be off under co-op.

    [TestMethod]
    public void ShouldPreventWarDeclaration_CoopActive_ReturnsFalseEvenWhenWarDisallowed()
    {
        // Arrange
        _coop.ShouldDeferToHost.Returns(true);
        _wotr.GetWarVeto("empire_w", "vlandia", false).Returns(WarVeto.Permanent);

        // Act
        var result = _sut.ShouldPreventWarDeclaration("empire_w", "vlandia");

        // Assert
        Assert.IsFalse(result, "co-op session must defer war declarations to the host");
    }

    [TestMethod]
    public void ShouldPreventAllianceEnd_CoopActive_ReturnsFalseEvenWhenPermanent()
    {
        // Arrange
        _coop.ShouldDeferToHost.Returns(true);
        _diplomacyService.GetRelationshipTier("empire_w", "vlandia")
            .Returns(AllianceTier.Permanent);

        // Act
        var result = _sut.ShouldPreventAllianceEnd("empire_w", "vlandia");

        // Assert
        Assert.IsFalse(result, "co-op session must defer alliance ends to the host");
    }

    [TestMethod]
    public void ShouldPreventWarDeclaration_CoopActive_DoesNotConsultWarOfTheRing()
    {
        // The veto is skipped outright, not computed and discarded.
        _coop.ShouldDeferToHost.Returns(true);

        _sut.ShouldPreventWarDeclaration("empire_w", "vlandia");

        _wotr.DidNotReceive().GetWarVeto(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>());
    }

    [TestMethod]
    public void GetDeclareWarScorePenalty_CoopDefersToHost_ReturnsZero()
    {
        _coop.ShouldDeferToHost.Returns(true);
        _wotr.GetDeclareWarScorePenalty("gondor", "rohan").Returns(5000f);

        Assert.AreEqual(0f, _sut.GetDeclareWarScorePenalty("gondor", true, "rohan", true));
    }

    [DataTestMethod]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(false, false)]
    public void GetDeclareWarScorePenalty_EitherSideNotKingdom_ReturnsZero(bool aIsKingdom, bool bIsKingdom)
    {
        _wotr.GetDeclareWarScorePenalty("gondor", "rohan").Returns(5000f);

        Assert.AreEqual(0f, _sut.GetDeclareWarScorePenalty("gondor", aIsKingdom, "rohan", bIsKingdom));
    }

    [TestMethod]
    public void GetDeclareWarScorePenalty_TwoKingdoms_ReturnsServiceValue()
    {
        _wotr.GetDeclareWarScorePenalty("gondor", "rohan").Returns(5000f);

        Assert.AreEqual(5000f, _sut.GetDeclareWarScorePenalty("gondor", true, "rohan", true));
    }

    // #1283: a call to war is refused for the war veto's reasons or for a configured refusal.
    [TestMethod]
    public void ShouldPreventCallToWar_RefusedPair_ReturnsTrueAndLogs()
    {
        _wotr.GetWarVeto("empire", "empire_w", false).Returns(WarVeto.None);
        _diplomacyService.RefusesCallToWar("empire", "empire_w").Returns(true);

        Assert.IsTrue(_sut.ShouldPreventCallToWar("empire", "empire_w"));
        _logger.Received(1).LogInfo("[Diplomacy] Call to war refused: empire will not join a war against empire_w");
    }

    [TestMethod]
    public void ShouldPreventCallToWar_WarVetoed_ReturnsTrue()
    {
        _wotr.GetWarVeto("rohan", "gondor", false).Returns(WarVeto.Permanent);

        Assert.IsTrue(_sut.ShouldPreventCallToWar("rohan", "gondor"));
    }

    [TestMethod]
    public void ShouldPreventCallToWar_AllowedAndNotRefused_ReturnsFalse()
    {
        _wotr.GetWarVeto("empire", "vlandia", false).Returns(WarVeto.None);

        Assert.IsFalse(_sut.ShouldPreventCallToWar("empire", "vlandia"));
    }

    [TestMethod]
    public void ShouldPreventCallToWar_CoopDefersToHost_ReturnsFalseWithoutConsultingTheList()
    {
        _coop.ShouldDeferToHost.Returns(true);
        _diplomacyService.RefusesCallToWar("empire", "empire_w").Returns(true);

        Assert.IsFalse(_sut.ShouldPreventCallToWar("empire", "empire_w"));
        _diplomacyService.DidNotReceive().RefusesCallToWar(Arg.Any<string>(), Arg.Any<string>());
    }

    // #1350: the lord dialogue's sponsor option asks for the text of a refused call, every time it is drawn.
    [TestMethod]
    public void GetCallToWarRefusalText_RefusedPair_ReturnsTheRefusalTextAndLogsNothing()
    {
        _wotr.GetWarVeto("empire", "empire_w", false).Returns(WarVeto.None);
        _diplomacyService.RefusesCallToWar("empire", "empire_w").Returns(true);

        Assert.AreEqual(WarVetoReasons.CallToWarRefused, _sut.GetCallToWarRefusalText("empire", "empire_w"));
        _logger.DidNotReceive().LogInfo(Arg.Any<string>());
    }

    [TestMethod]
    public void GetCallToWarRefusalText_WarVetoed_ReturnsThatVetosText()
    {
        _wotr.GetWarVeto("rohan", "gondor", false).Returns(WarVeto.Permanent);
        _diplomacyService.RefusesCallToWar("rohan", "gondor").Returns(true);

        Assert.AreEqual(WarVetoReasons.Text(WarVeto.Permanent), _sut.GetCallToWarRefusalText("rohan", "gondor"));
    }

    [TestMethod]
    public void GetCallToWarRefusalText_AllowedAndNotRefused_ReturnsNull()
    {
        _wotr.GetWarVeto("empire", "vlandia", false).Returns(WarVeto.None);

        Assert.IsNull(_sut.GetCallToWarRefusalText("empire", "vlandia"));
    }

    [TestMethod]
    public void GetCallToWarRefusalText_CoopDefersToHost_ReturnsNullWithoutConsultingEitherRule()
    {
        _coop.ShouldDeferToHost.Returns(true);
        _diplomacyService.RefusesCallToWar("empire", "empire_w").Returns(true);

        Assert.IsNull(_sut.GetCallToWarRefusalText("empire", "empire_w"));
        _wotr.DidNotReceive().GetWarVeto(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>());
        _diplomacyService.DidNotReceive().RefusesCallToWar(Arg.Any<string>(), Arg.Any<string>());
    }
}
