using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CharacterCreation;

namespace TAOM.Tests.Features.CharacterCreation;

[TestClass]
public class CCBodyPropertiesServiceTests
{
    private const string SampleXml =
        "<BodyProperties version=\"4\" key=\"0005280140001242947E068A709500460C7250703EB70F135C85021887733A070089B6030822BA9000000000000000000000000000000000000000003F1C7002\" />";

    private ICCBodyPropertiesProvider _provider;
    private IPlayerBodyPropertiesAdapter _adapter;
    private IModLogger _logger;
    private CCBodyPropertiesService _sut;

    [TestInitialize]
    public void Setup()
    {
        _provider = Substitute.For<ICCBodyPropertiesProvider>();
        _adapter = Substitute.For<IPlayerBodyPropertiesAdapter>();
        _logger = Substitute.For<IModLogger>();
        _sut = new CCBodyPropertiesService(_provider, _adapter, _logger);
    }

    [TestMethod]
    public void ApplyForCulture_ConfiguredCulture_AppliesToAdapter()
    {
        _provider.GetBodyPropertiesXml("vlandia").Returns(SampleXml);
        _adapter.TryApplyFromXml(SampleXml).Returns(true);

        _sut.ApplyForCulture("vlandia");

        _adapter.Received(1).TryApplyFromXml(SampleXml);
    }

    [TestMethod]
    public void ApplyForCulture_NotConfigured_DoesNotCallAdapter()
    {
        _provider.GetBodyPropertiesXml("sturgia").Returns((string)null);

        _sut.ApplyForCulture("sturgia");

        _adapter.DidNotReceive().TryApplyFromXml(Arg.Any<string>());
    }

    [TestMethod]
    public void ApplyForCulture_AdapterParseFailure_LogsWarning()
    {
        _provider.GetBodyPropertiesXml("vlandia").Returns(SampleXml);
        _adapter.TryApplyFromXml(SampleXml).Returns(false);

        _sut.ApplyForCulture("vlandia");

        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("vlandia") && s.Contains("apply")));
    }

    [TestMethod]
    public void ApplyForCulture_NullCultureId_DoesNotCallProviderOrAdapter()
    {
        _sut.ApplyForCulture(null);

        _provider.DidNotReceive().GetBodyPropertiesXml(Arg.Any<string>());
        _adapter.DidNotReceive().TryApplyFromXml(Arg.Any<string>());
    }

    [TestMethod]
    public void ApplyForCulture_EmptyCultureId_DoesNotCallProviderOrAdapter()
    {
        _sut.ApplyForCulture("");

        _provider.DidNotReceive().GetBodyPropertiesXml(Arg.Any<string>());
        _adapter.DidNotReceive().TryApplyFromXml(Arg.Any<string>());
    }

    [TestMethod]
    public void ApplyForCulture_AdapterThrows_LogsErrorAndDoesNotPropagate()
    {
        _provider.GetBodyPropertiesXml("vlandia").Returns(SampleXml);
        _adapter.TryApplyFromXml(SampleXml).Returns(_ => throw new System.Exception("boom"));

        _sut.ApplyForCulture("vlandia");

        _logger.Received().LogError(Arg.Is<string>(s => s.Contains("vlandia") && s.Contains("boom")));
    }

    [TestMethod]
    public void ApplyForCulture_SuccessfulApply_LogsInfo()
    {
        _provider.GetBodyPropertiesXml("vlandia").Returns(SampleXml);
        _adapter.TryApplyFromXml(SampleXml).Returns(true);

        _sut.ApplyForCulture("vlandia");

        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("vlandia")));
    }
}
