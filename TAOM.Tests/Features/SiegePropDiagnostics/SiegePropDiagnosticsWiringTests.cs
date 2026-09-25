using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.MountAndBlade;
using TAOM.Features.SiegePropDiagnostics.Hooks;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.SiegePropDiagnostics;

// Wiring-regression tests. This feature is registered the same way SiegeDismount is:
//     AddTaomBehavior(new SiegePropDiagnostics.Hooks.SiegePropDiagnosticsMissionBehavior());
// inside Main/SubModule.cs::OnMissionBehaviorInitialize, with the service graph registered from
// Main/IoC.cs. Drop either line and the diagnostic silently never runs — which for a diagnostic is
// the worst failure mode available, since a clean log then reads as "no faults found".
[TestClass]
public class SiegePropDiagnosticsWiringTests
{
    [TestMethod]
    public void MainIoCConfigure_IncludesSiegePropDiagnosticsRegistration()
    {
        var iocSource = RepoPaths.ReadSource("Main/IoC.cs", stripComments: true);

        StringAssert.Contains(iocSource,
            "SiegePropDiagnosticsIoC.RegisterSiegePropDiagnosticsFeature(container);",
            "Main/IoC.cs::Configure must register the feature. Without it, the behavior's ctor " +
            "IoC.Resolve<ISiegePropDiagnosticsService>() throws the first time a mission spawns.");
    }

    [TestMethod]
    public void MainSubModule_AddsSiegePropDiagnosticsMissionBehaviorOnMissionInit()
    {
        var subModuleSource = RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);

        StringAssert.Contains(subModuleSource, "SiegePropDiagnosticsMissionBehavior());",
            "Main/SubModule.cs must register SiegePropDiagnosticsMissionBehavior via AddTaomBehavior(...).");

        StringAssert.Contains(subModuleSource, "OnMissionBehaviorInitialize",
            "Registration must happen from OnMissionBehaviorInitialize so it runs at mission start.");
    }

    [TestMethod]
    public void SiegePropDiagnosticsMissionBehavior_IsMissionBehavior()
    {
        Assert.IsTrue(typeof(MissionBehavior).IsAssignableFrom(typeof(SiegePropDiagnosticsMissionBehavior)),
            "Must inherit MissionBehavior so AddMissionBehavior accepts it.");
    }
}
