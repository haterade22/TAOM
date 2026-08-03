using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.MountAndBlade;
using TAOM.Features.SiegePropDiagnostics.Hooks;

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
        var iocSource = ReadProjectSource("Main", "IoC.cs");
        if (iocSource == null)
            Assert.Inconclusive("Main/IoC.cs not found — run from repo root or check working directory");

        StringAssert.Contains(iocSource,
            "SiegePropDiagnosticsIoC.RegisterSiegePropDiagnosticsFeature(container);",
            "Main/IoC.cs::Configure must register the feature. Without it, the behavior's ctor " +
            "IoC.Resolve<ISiegePropDiagnosticsService>() throws the first time a mission spawns.");
    }

    [TestMethod]
    public void MainSubModule_AddsSiegePropDiagnosticsMissionBehaviorOnMissionInit()
    {
        var subModuleSource = ReadProjectSource("Main", "SubModule.cs");
        if (subModuleSource == null)
            Assert.Inconclusive("Main/SubModule.cs not found — run from repo root or check working directory");

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

    private static string ReadProjectSource(params string[] relativeParts)
    {
        var dir = Directory.GetCurrentDirectory();
        while (dir != null)
        {
            var candidate = Path.Combine(new[] { dir }.Concat(relativeParts).ToArray());
            if (File.Exists(candidate))
                return File.ReadAllText(candidate);
            dir = Directory.GetParent(dir)?.FullName;
        }
        return null;
    }
}
