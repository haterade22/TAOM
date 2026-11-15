using System.Linq;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Client;
using TAOM.Client.Features.Autorun;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.Autorun.Scenario;
using TAOM.Tests.Infrastructure;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.Autorun.Scenario;

/// <summary>
/// Wiring of the campaign scenario autorun's client half (#1075, ADR-013): the Client host replaces core's null host, and the client's
/// application tick builds the driver only through the start helper and ticks it. The host is never constructed here: it subscribes to
/// engine events, so the composition check resolves the registration's implementation type only.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class ScenarioClientWiringTests
{
    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => GameAssemblies.EnsureLoaded();

    [TestCleanup]
    public void Cleanup() => ClientComposition.Current = null;

    [TestMethod]
    public void Apply_OverCoresNullHost_RegistersTheClientHost()
    {
        var container = new Container();
        container.Register<ICampaignAutorunHost, NullCampaignAutorunHost>(Reuse.Singleton);
        container.RegisterInstance(Substitute.For<IModLogger>());
        ClientComposition.Current = new TaomClientComposition();

        ClientComposition.Apply(container);

        var registration = container.GetServiceRegistrations()
            .Single(r => r.ServiceType == typeof(ICampaignAutorunHost));
        Assert.AreEqual(typeof(CampaignAutorunHost), registration.Factory.ImplementationType);
    }

    [TestMethod]
    public void ClientSubModuleSource_BuildsTheDriverThroughTheStartHelper_AndTicksIt()
    {
        var src = RepoPaths.ReadSource("Client/ClientSubModule.cs", stripComments: true);

        StringAssert.Contains(src,
            "_scenarioAutorun = ScenarioAutorunStart.Driver(IoC.Resolve<ScenarioPlan>(), IoC.Resolve<ScenarioAutorunDriver>, IoC.Resolve<IModLogger>());");
        StringAssert.Contains(src, "_scenarioAutorun?.Tick(dt);");
    }

    [TestMethod]
    public void ClientSubModuleSource_RestoresTheFocusSettingAtEveryGameEnd()
    {
        var src = RepoPaths.ReadSource("Client/ClientSubModule.cs", stripComments: true);

        StringAssert.Contains(src, "public override void OnGameEnd(Game game)");
        StringAssert.Contains(src, "_scenarioAutorun?.GameEnded();");
    }

    [TestMethod]
    public void HostSource_TakesItsPlanByConstructor_AndResolvesNothingItself()
    {
        var src = RepoPaths.ReadSource("Client/Features/Autorun/CampaignAutorunHost.cs", stripComments: true);

        Assert.IsFalse(src.Contains("IoC."), "the host takes its plan by constructor");
        StringAssert.Contains(src, "public CampaignAutorunHost(ScenarioPlan plan)");
    }
}
