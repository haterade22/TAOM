using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.Diplomacy;
using TAOM.Features.Diplomacy.Hooks;

namespace TAOM.Tests.Features.Diplomacy;

[TestClass]
public class PeaceActionHookTests
{
    private IWarOfTheRingService _wotrService;
    private IModLogger _logger;
    private ICoopPresenceProvider _coop;
    private PeaceActionHook _sut;

    [TestInitialize]
    public void Setup()
    {
        _wotrService = Substitute.For<IWarOfTheRingService>();
        _logger = Substitute.For<IModLogger>();
        _coop = Substitute.For<ICoopPresenceProvider>();
        _coop.IsCoopActive.Returns(false);
        _sut = new PeaceActionHook(_wotrService, _logger, _coop);
    }

    [TestMethod]
    public void ShouldPreventPeace_WotRActive_HostilePair_ReturnsTrue()
    {
        _wotrService.IsWarOfTheRingActive.Returns(true);
        _wotrService.ShouldBlockPeace("empire_w", "empire_s").Returns(true);

        Assert.IsTrue(_sut.ShouldPreventPeace("empire_w", "empire_s"));
    }

    [TestMethod]
    public void ShouldPreventPeace_WotRActive_NeutralPair_ReturnsFalse()
    {
        _wotrService.IsWarOfTheRingActive.Returns(true);
        _wotrService.ShouldBlockPeace("aserai", "umbar").Returns(false);

        Assert.IsFalse(_sut.ShouldPreventPeace("aserai", "umbar"));
    }

    [TestMethod]
    public void ShouldPreventPeace_WotRNotActive_ReturnsFalse()
    {
        _wotrService.IsWarOfTheRingActive.Returns(false);

        Assert.IsFalse(_sut.ShouldPreventPeace("empire_w", "empire_s"));
    }

    // --- Co-op interop (#370) -------------------------------------------------------------
    // Mirror of the war-declaration gate in AllianceActionHookTests: vetoing a peace the host
    // already applied leaves the client at war and the host at peace.

    [TestMethod]
    public void ShouldPreventPeace_CoopActive_ReturnsFalseEvenWhenWotRBlocks()
    {
        // Arrange
        _coop.IsCoopActive.Returns(true);
        _wotrService.IsWarOfTheRingActive.Returns(true);
        _wotrService.ShouldBlockPeace("empire_w", "empire_s").Returns(true);

        // Act
        var result = _sut.ShouldPreventPeace("empire_w", "empire_s");

        // Assert
        Assert.IsFalse(result, "co-op session must defer peace to the host");
    }

    [TestMethod]
    public void ShouldPreventPeace_CoopActive_DoesNotConsultWotRService()
    {
        // Arrange
        _coop.IsCoopActive.Returns(true);

        // Act
        _sut.ShouldPreventPeace("empire_w", "empire_s");

        // Assert
        _wotrService.DidNotReceive().ShouldBlockPeace(Arg.Any<string>(), Arg.Any<string>());
    }
}
