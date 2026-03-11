using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.FactionMap;

namespace TAOM.Tests.Features.FactionMap;

[TestClass]
public class CultureResolverServiceTests
{
    private CultureResolverService _sut = null!;
    private ICultureObjectAdapter _cultureAdapter = null!;
    private IModLogger _logger = null!;

    [TestInitialize]
    public void Setup()
    {
        _cultureAdapter = Substitute.For<ICultureObjectAdapter>();
        _logger = Substitute.For<IModLogger>();
        _sut = new CultureResolverService(_cultureAdapter, _logger);
    }

    [TestMethod]
    public void ResolveCulture_ValidId_ReturnsFromAdapter()
    {
        var fakeCulture = new object();
        _cultureAdapter.ResolveCulture("gondor").Returns(fakeCulture);

        var result = _sut.ResolveCulture("gondor");

        Assert.AreSame(fakeCulture, result);
    }

    [TestMethod]
    public void ResolveCulture_EmptyId_ReturnsNull()
    {
        _cultureAdapter.ResolveCulture("").Returns((object?)null);

        var result = _sut.ResolveCulture("");

        Assert.IsNull(result);
    }

    [TestMethod]
    public void ResolveCulture_NullId_ReturnsNull()
    {
        _cultureAdapter.ResolveCulture(null!).Returns((object?)null);

        var result = _sut.ResolveCulture(null!);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void ResolveCulture_AdapterReturnsNull_LogsError()
    {
        _cultureAdapter.ResolveCulture("unknown_culture").Returns((object?)null);

        _sut.ResolveCulture("unknown_culture");

        _logger.Received(1).LogError(Arg.Is<string>(s => s.Contains("unknown_culture")));
    }

    [TestMethod]
    public void ResolveCulture_ValidId_ReturnsObjectWithoutLogging()
    {
        var fakeCulture = new object();
        _cultureAdapter.ResolveCulture("gondor").Returns(fakeCulture);

        _sut.ResolveCulture("gondor");

        _logger.DidNotReceive().LogError(Arg.Any<string>());
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void IsCultureAvailable_ValidId_ReturnsTrue()
    {
        _cultureAdapter.ResolveCulture("gondor").Returns(new object());

        var result = _sut.IsCultureAvailable("gondor");

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsCultureAvailable_UnknownId_ReturnsFalse()
    {
        _cultureAdapter.ResolveCulture("nonexistent").Returns((object?)null);

        var result = _sut.IsCultureAvailable("nonexistent");

        Assert.IsFalse(result);
    }
}
