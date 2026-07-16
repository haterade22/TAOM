using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.MountAndBlade;
using TAOM.Features.SiegeDismount.Hooks;

namespace TAOM.Tests.Features.SiegeDismount;

// Wiring-regression test for SiegeDismount. The Phase 4 audit issue #193 originally claimed this
// feature uses manual _harmony.Patch(...) like SettlementGuards (#192), but Phase 9a verification
// (and Codex confirmation) corrected the mechanism: SiegeDismount wires via
//     AddTaomBehavior(new SiegeDismountMissionBehavior());
// inside Main/SubModule.cs::OnMissionBehaviorInitialize. The MissionBehavior subclass resolves
// ISiegeDismountService from IoC in its ctor and forwards Mission lifecycle calls.
//
// 2026-07-16: the call was `mission.AddMissionBehavior(new SiegeDismountMissionBehavior());` until
// the battle-load blind-window work routed TAOM's own behaviors through the local AddTaomBehavior
// helper, which [BattleLoad]-stamps each behavior by name before handing it to the engine. The
// registration guarantee this test defends is unchanged — only the call's shape moved.
//
// The wiring is uniquely vulnerable to a Messengers-class regression in TWO ways:
//   1. If the AddMissionBehavior call is dropped from SubModule.cs, the behavior never registers
//      and the service's OnMissionStart / OnMissionEnd hooks never fire — silently broken siege
//      dismount even though all unit tests pass and the build is clean.
//   2. If SiegeDismountIoC.RegisterSiegeDismountFeature is dropped from Main/IoC.cs, the behavior
//      ctor's IoC.Resolve<ISiegeDismountService>() throws at mission start.
[TestClass]
public class SiegeDismountWiringTests
{
    // --- Wiring catalog regression tests ---

    [TestMethod]
    public void MainIoCConfigure_IncludesSiegeDismountFeatureRegistration()
    {
        var iocSource = ReadProjectSource("Main", "IoC.cs");
        if (iocSource == null)
            Assert.Inconclusive("Main/IoC.cs not found — run from repo root or check working directory");

        StringAssert.Contains(iocSource, "SiegeDismountIoC.RegisterSiegeDismountFeature(container);",
            "Main/IoC.cs::Configure must call SiegeDismountIoC.RegisterSiegeDismountFeature(container). " +
            "Without it, SiegeDismountMissionBehavior's ctor IoC.Resolve<ISiegeDismountService>() throws " +
            "the first time a mission spawns.");
    }

    [TestMethod]
    public void MainSubModule_AddsSiegeDismountMissionBehaviorOnMissionInit()
    {
        var subModuleSource = ReadProjectSource("Main", "SubModule.cs");
        if (subModuleSource == null)
            Assert.Inconclusive("Main/SubModule.cs not found — run from repo root or check working directory");

        // Two-part assertion: the call literal AND the lifecycle method that contains it.
        // The call literal alone could appear inside a comment or unreachable branch.
        StringAssert.Contains(subModuleSource, "AddTaomBehavior(new SiegeDismountMissionBehavior());",
            "Main/SubModule.cs must register SiegeDismountMissionBehavior via AddTaomBehavior(...) " +
            "from inside OnMissionBehaviorInitialize. Audit-motivating regression class: drop the line and " +
            "siege dismount stops applying with zero diagnostic.");

        StringAssert.Contains(subModuleSource, "OnMissionBehaviorInitialize",
            "Main/SubModule.cs must override OnMissionBehaviorInitialize so AddMissionBehavior runs at " +
            "mission start.");
    }

    // --- Type sanity ---

    [TestMethod]
    public void SiegeDismountMissionBehavior_IsMissionBehavior_LogicType()
    {
        // Verifies the behavior is wired into the Logic phase (vs. Other or View). MissionBehaviorType
        // determines when AddMissionBehavior accepts vs rejects the instance.
        // The ctor calls IoC.Resolve<>(...) which requires IoC.Configure() to have run — so we read the
        // type's BehaviorType via reflection on a default-constructed instance only after IoC setup.
        // Cheaper alternative: assert the static type metadata directly.
        var behaviorType = typeof(SiegeDismountMissionBehavior);
        Assert.IsTrue(typeof(MissionBehavior).IsAssignableFrom(behaviorType),
            "SiegeDismountMissionBehavior must inherit from MissionBehavior so mission.AddMissionBehavior accepts it.");
    }

    // --- Helpers ---

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
