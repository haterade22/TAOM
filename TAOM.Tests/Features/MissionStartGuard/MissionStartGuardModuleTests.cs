using System.Linq;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.MissionStartGuard;
using TAOM.Features.MissionStartGuard.Hooks;

namespace TAOM.Tests.Features.MissionStartGuard;

/// <summary>The module's wiring against a real DryIoc container: what resolves, what the helpers are handed, and the install line.</summary>
[TestClass]
[TestCategory("RequiresGame")]
public class MissionStartGuardModuleTests
{
    private Container _container = null!;
    private IModLogger _logger = null!;
    private readonly MissionStartGuardModule _sut = new MissionStartGuardModule();

    [TestInitialize]
    public void Setup()
    {
        _container = new Container();
        _logger = Substitute.For<IModLogger>();
        _container.RegisterInstance(_logger);
        _container.RegisterInstance(Substitute.For<IDedicatedServerProvider>());
        _sut.RegisterServices(_container);
    }

    [TestCleanup]
    public void Cleanup()
    {
        MissionStartGuardCalls.Initialize(null);
        MissionStartGuardSwaps.LastSwapped = 0;
        _container.Dispose();
    }

    [TestMethod]
    public void RegisterServices_TheServiceResolvesWithItsFourDependencies()
    {
        var service = _container.Resolve<IMissionStartGuardService>();

        Assert.IsInstanceOfType(service, typeof(MissionStartGuardService));
        Assert.AreSame(service, _container.Resolve<IMissionStartGuardService>(), "a singleton");
    }

    [TestMethod]
    public void InitializeStatics_HandsTheServiceToTheHelpers()
    {
        _sut.InitializeStatics(_container);

        Assert.AreSame(_container.Resolve<IMissionStartGuardService>(), MissionStartGuardCalls.Service);
    }

    [TestMethod]
    public void OnPhase_GameInitAfterASixSiteSwap_LogsTheOnLine()
    {
        MissionStartGuardSwaps.LastSwapped = 6;

        _sut.OnPhase(ApplyPhase.GameInit, _container);

        var infos = _logger.ReceivedCalls().Where(c => c.GetMethodInfo().Name == nameof(IModLogger.LogInfo))
            .Select(c => (string)c.GetArguments()[0]!).ToList();
        StringAssert.StartsWith(infos.Single(), "[MissionStartGuard] ON: 6 call sites wrapped in Mission.AfterStart");
    }

    [TestMethod]
    public void OnPhase_GameInitAfterASoftFail_LogsTheWarning()
    {
        MissionStartGuardSwaps.LastSwapped = 0;

        _sut.OnPhase(ApplyPhase.GameInit, _container);

        _logger.Received(1).LogWarning(NSubstitute.Arg.Is<string>(s => s.StartsWith("[MissionStartGuard] OFF: wrapped 0 of 6")));
        _logger.DidNotReceive().LogInfo(NSubstitute.Arg.Any<string>());
    }

    [TestMethod]
    public void OnPhase_OtherPhases_LogNothing()
    {
        foreach (var phase in new[] { ApplyPhase.ProcessLoad, ApplyPhase.MainMenu, ApplyPhase.FirstMission })
            _sut.OnPhase(phase, _container);

        Assert.AreEqual(0, _logger.ReceivedCalls().Count());
    }
}
