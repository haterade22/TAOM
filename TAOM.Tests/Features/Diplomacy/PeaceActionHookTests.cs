using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.Diplomacy;
using TAOM.Features.Diplomacy.Hooks;

namespace TAOM.Tests.Features.Diplomacy;

[TestClass]
public class PeaceActionHookTests
{
    private IWarOfTheRingService _wotrService;
    private IModLogger _logger;
    private PeaceActionHook _sut;

    [TestInitialize]
    public void Setup()
    {
        _wotrService = Substitute.For<IWarOfTheRingService>();
        _logger = Substitute.For<IModLogger>();
        _sut = new PeaceActionHook(_wotrService, _logger);
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
}
