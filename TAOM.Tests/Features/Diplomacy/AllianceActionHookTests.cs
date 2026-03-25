using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.Diplomacy;
using TAOM.Features.Diplomacy.Hooks;
using TAOM.Features.Diplomacy.Models;

namespace TAOM.Tests.Features.Diplomacy;

[TestClass]
public class AllianceActionHookTests
{
    private IDiplomacyService _diplomacyService;
    private AllianceActionHook _sut;

    [TestInitialize]
    public void Setup()
    {
        _diplomacyService = Substitute.For<IDiplomacyService>();
        _sut = new AllianceActionHook(_diplomacyService);
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
    public void ShouldPreventWarDeclaration_PermanentAllies_ReturnsTrue()
    {
        _diplomacyService.GetRelationshipTier("empire_w", "vlandia")
            .Returns(AllianceTier.Permanent);

        Assert.IsTrue(_sut.ShouldPreventWarDeclaration("empire_w", "vlandia"));
    }

    [TestMethod]
    public void ShouldPreventWarDeclaration_HostileKingdoms_ReturnsFalse()
    {
        _diplomacyService.GetRelationshipTier("empire_w", "empire_s")
            .Returns(AllianceTier.Hostile);

        Assert.IsFalse(_sut.ShouldPreventWarDeclaration("empire_w", "empire_s"));
    }

    [TestMethod]
    public void ShouldPreventWarDeclaration_NeutralKingdoms_ReturnsFalse()
    {
        _diplomacyService.GetRelationshipTier("battania", "aserai")
            .Returns(AllianceTier.Neutral);

        Assert.IsFalse(_sut.ShouldPreventWarDeclaration("battania", "aserai"));
    }
}
