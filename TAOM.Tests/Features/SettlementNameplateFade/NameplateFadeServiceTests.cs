using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.SettlementNameplateFade;

namespace TAOM.Tests.Features.SettlementNameplateFade;

[TestClass]
public class NameplateFadeServiceTests
{
    private INameplateFadeSettingsProvider _settings = null!;
    private NameplateFadeService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _settings = Substitute.For<INameplateFadeSettingsProvider>();
        _settings.Enabled.Returns(true);
        _settings.NearDistance.Returns(80f);
        _settings.FarDistance.Returns(200f);
        _sut = new NameplateFadeService(_settings);
    }

    [TestMethod]
    public void ComputeAlphaMultiplier_FeatureDisabled_ReturnsOne()
    {
        _settings.Enabled.Returns(false);

        Assert.AreEqual(1f, _sut.ComputeAlphaMultiplier(0f));
        Assert.AreEqual(1f, _sut.ComputeAlphaMultiplier(50f));
        Assert.AreEqual(1f, _sut.ComputeAlphaMultiplier(500f));
        Assert.AreEqual(1f, _sut.ComputeAlphaMultiplier(float.NaN));
    }

    [TestMethod]
    public void ComputeAlphaMultiplier_DistanceBelowNear_ReturnsOne()
    {
        Assert.AreEqual(1f, _sut.ComputeAlphaMultiplier(0f));
        Assert.AreEqual(1f, _sut.ComputeAlphaMultiplier(40f));
        Assert.AreEqual(1f, _sut.ComputeAlphaMultiplier(79.9f));
    }

    [TestMethod]
    public void ComputeAlphaMultiplier_DistanceAtNear_ReturnsOne()
    {
        Assert.AreEqual(1f, _sut.ComputeAlphaMultiplier(80f));
    }

    [TestMethod]
    public void ComputeAlphaMultiplier_DistanceAtFar_ReturnsZero()
    {
        Assert.AreEqual(0f, _sut.ComputeAlphaMultiplier(200f));
    }

    [TestMethod]
    public void ComputeAlphaMultiplier_DistanceAboveFar_ReturnsZero()
    {
        Assert.AreEqual(0f, _sut.ComputeAlphaMultiplier(200.1f));
        Assert.AreEqual(0f, _sut.ComputeAlphaMultiplier(500f));
        Assert.AreEqual(0f, _sut.ComputeAlphaMultiplier(10000f));
    }

    [TestMethod]
    public void ComputeAlphaMultiplier_DistanceAtMidpoint_ReturnsHalf()
    {
        // near=80, far=200, span=120, midpoint=140. (140-80)/120 = 0.5, so multiplier = 0.5.
        Assert.AreEqual(0.5f, _sut.ComputeAlphaMultiplier(140f), 0.0001f);
    }

    [TestMethod]
    public void ComputeAlphaMultiplier_DistanceQuarterIntoBand_ReturnsThreeQuarters()
    {
        // near=80, far=200, span=120, quarter-in=110. (110-80)/120 = 0.25, multiplier = 0.75.
        Assert.AreEqual(0.75f, _sut.ComputeAlphaMultiplier(110f), 0.0001f);
    }

    [TestMethod]
    public void ComputeAlphaMultiplier_NaNDistance_ReturnsOne()
    {
        Assert.AreEqual(1f, _sut.ComputeAlphaMultiplier(float.NaN));
    }

    [TestMethod]
    public void ComputeAlphaMultiplier_PositiveInfinityDistance_ReturnsOne()
    {
        Assert.AreEqual(1f, _sut.ComputeAlphaMultiplier(float.PositiveInfinity));
    }

    [TestMethod]
    public void ComputeAlphaMultiplier_NegativeInfinityDistance_ReturnsOne()
    {
        Assert.AreEqual(1f, _sut.ComputeAlphaMultiplier(float.NegativeInfinity));
    }

    [TestMethod]
    public void ComputeAlphaMultiplier_NegativeDistance_ReturnsOne()
    {
        Assert.AreEqual(1f, _sut.ComputeAlphaMultiplier(-1f));
        Assert.AreEqual(1f, _sut.ComputeAlphaMultiplier(-100f));
    }

    [TestMethod]
    public void ComputeAlphaMultiplier_NaNNearDistance_ReturnsOne()
    {
        _settings.NearDistance.Returns(float.NaN);
        Assert.AreEqual(1f, _sut.ComputeAlphaMultiplier(100f));
    }

    [TestMethod]
    public void ComputeAlphaMultiplier_NaNFarDistance_ReturnsOne()
    {
        _settings.FarDistance.Returns(float.NaN);
        Assert.AreEqual(1f, _sut.ComputeAlphaMultiplier(100f));
    }

    [TestMethod]
    public void ComputeAlphaMultiplier_InfinityFarDistance_ReturnsOne()
    {
        _settings.FarDistance.Returns(float.PositiveInfinity);
        Assert.AreEqual(1f, _sut.ComputeAlphaMultiplier(100f));
    }

    [TestMethod]
    public void ComputeAlphaMultiplier_InfinityNearDistance_ReturnsOne()
    {
        _settings.NearDistance.Returns(float.PositiveInfinity);
        Assert.AreEqual(1f, _sut.ComputeAlphaMultiplier(100f));
    }

    [TestMethod]
    public void ComputeAlphaMultiplier_FarLessThanNear_ReturnsOne()
    {
        _settings.NearDistance.Returns(200f);
        _settings.FarDistance.Returns(100f);
        Assert.AreEqual(1f, _sut.ComputeAlphaMultiplier(150f));
    }

    [TestMethod]
    public void ComputeAlphaMultiplier_FarEqualsNear_ReturnsOne()
    {
        // Avoid divide-by-zero on collapsed range.
        _settings.NearDistance.Returns(100f);
        _settings.FarDistance.Returns(100f);
        Assert.AreEqual(1f, _sut.ComputeAlphaMultiplier(50f));
        Assert.AreEqual(1f, _sut.ComputeAlphaMultiplier(100f));
        Assert.AreEqual(1f, _sut.ComputeAlphaMultiplier(150f));
    }

    [TestMethod]
    public void ComputeAlphaMultiplier_CustomBand_LinearInterpolatesCorrectly()
    {
        _settings.NearDistance.Returns(50f);
        _settings.FarDistance.Returns(150f);

        Assert.AreEqual(1f, _sut.ComputeAlphaMultiplier(50f));
        Assert.AreEqual(0.75f, _sut.ComputeAlphaMultiplier(75f), 0.0001f);
        Assert.AreEqual(0.5f, _sut.ComputeAlphaMultiplier(100f), 0.0001f);
        Assert.AreEqual(0.25f, _sut.ComputeAlphaMultiplier(125f), 0.0001f);
        Assert.AreEqual(0f, _sut.ComputeAlphaMultiplier(150f));
    }
}
