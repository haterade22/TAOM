using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Diagnostics;
using TAOM.Core.Logging;
using TAOM.Features.BattleLoadDiagnostics;
using TAOM.Features.LoadTimeStamps;

namespace TAOM.Tests.Features.LoadTimeStamps;

/// <summary>
/// The module's registrations resolve. A service that does not resolve makes ModuleRunner mark the
/// module faulted and skip it, which silently turns off every stamp, the always-on ones included.
/// The listener adapter is swapped for the fake: the real one reflects into engine types.
/// </summary>
[TestClass]
public class LoadTimeStampsModuleTests
{
    [TestMethod]
    public void RegisterServices_ResolvesEveryServiceAsASingleton()
    {
        var container = new Container();
        container.RegisterInstance(Substitute.For<IModLogger>());
        container.RegisterInstance(Substitute.For<IBattleLoadDiagnosticsSettingsProvider>());
        new LoadTimeStampsModule().RegisterServices(container);
        container.RegisterInstance<ICampaignListenerAdapter>(new FakeCampaignListenerAdapter(), IfAlreadyRegistered.Replace);

        Assert.IsInstanceOfType(container.Resolve<IStampClock>(), typeof(StopwatchStampClock));
        Assert.IsNotNull(container.Resolve<HookStampService>());
        Assert.IsNotNull(container.Resolve<LoadXmlStampService>());
        Assert.IsNotNull(container.Resolve<LifecycleTimingService>());
        Assert.AreSame(container.Resolve<LoadStampDetailGate>(), container.Resolve<LoadStampDetailGate>(),
            "the gate's last-logged value only works as a singleton");
        Assert.AreSame(container.Resolve<LoadXmlStampService>(), container.Resolve<LoadXmlStampService>(),
            "the summary aggregates across calls only as a singleton");
        Assert.AreSame(container.Resolve<LifecycleTimingService>(), container.Resolve<LifecycleTimingService>(),
            "the once-per-process latches only work as a singleton");
    }
}
