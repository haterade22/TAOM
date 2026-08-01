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
    private IModLogger _logger;
    private ICoopPresenceProvider _coop;
    private AllianceActionHook _sut;

    [TestInitialize]
    public void Setup()
    {
        _diplomacyService = Substitute.For<IDiplomacyService>();
        _logger = Substitute.For<IModLogger>();
        _coop = Substitute.For<ICoopPresenceProvider>();
        _coop.IsCoopActive.Returns(false);
        _sut = new AllianceActionHook(_diplomacyService, _logger, _coop);
    }

    [TestMethod]
    public void ShouldPreventAllianceEnd_PermanentAllies_ReturnsTrue()
    {
        _diplomacyService.GetRelationshipTier("empire_w", "vlandia")
            .Returns(AllianceTier.Permanent);

        Assert.IsTrue(_sut.ShouldPreventAllianceEnd("empire_w", "vlandia"));
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

    [TestMethod]
    public void ShouldPreventWarDeclaration_WarBlocked_ReturnsTrue()
    {
        _diplomacyService.IsWarAllowed("empire_w", "vlandia").Returns(false);

        Assert.IsTrue(_sut.ShouldPreventWarDeclaration("empire_w", "vlandia"));
    }

    [TestMethod]
    public void ShouldPreventWarDeclaration_WarAllowed_ReturnsFalse()
    {
        _diplomacyService.IsWarAllowed("empire_w", "empire_s").Returns(true);

        Assert.IsFalse(_sut.ShouldPreventWarDeclaration("empire_w", "empire_s"));
    }

    [TestMethod]
    public void ShouldPreventWarDeclaration_NeutralKingdoms_ReturnsFalse()
    {
        _diplomacyService.IsWarAllowed("battania", "aserai").Returns(true);

        Assert.IsFalse(_sut.ShouldPreventWarDeclaration("battania", "aserai"));
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
        _coop.IsCoopActive.Returns(true);
        _diplomacyService.IsWarAllowed("empire_w", "vlandia").Returns(false);

        // Act
        var result = _sut.ShouldPreventWarDeclaration("empire_w", "vlandia");

        // Assert
        Assert.IsFalse(result, "co-op session must defer war declarations to the host");
    }

    [TestMethod]
    public void ShouldPreventAllianceEnd_CoopActive_ReturnsFalseEvenWhenPermanent()
    {
        // Arrange
        _coop.IsCoopActive.Returns(true);
        _diplomacyService.GetRelationshipTier("empire_w", "vlandia")
            .Returns(AllianceTier.Permanent);

        // Act
        var result = _sut.ShouldPreventAllianceEnd("empire_w", "vlandia");

        // Assert
        Assert.IsFalse(result, "co-op session must defer alliance ends to the host");
    }

    [TestMethod]
    public void ShouldPreventWarDeclaration_CoopActive_DoesNotConsultDiplomacyService()
    {
        // Arrange — the veto is skipped outright, not computed and discarded.
        _coop.IsCoopActive.Returns(true);

        // Act
        _sut.ShouldPreventWarDeclaration("empire_w", "vlandia");

        // Assert
        _diplomacyService.DidNotReceive().IsWarAllowed(Arg.Any<string>(), Arg.Any<string>());
    }
}
