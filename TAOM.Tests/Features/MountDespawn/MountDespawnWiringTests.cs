using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;
using TAOM.Features.MountDespawn;
using TAOM.Features.MountDespawn.Hooks;

namespace TAOM.Tests.Features.MountDespawn;

/// <summary>
/// Wiring-regression cover for dead-mount despawn. The behavior is the feature's only entry point:
/// drop its registration and killed mounts silently lie on the field again with every unit test
/// still green and the build clean.
/// </summary>
[TestClass]
public class MountDespawnWiringTests
{
    [TestMethod]
    public void MainIoCConfigure_IncludesMountDespawnFeatureRegistration()
    {
        var iocSource = ReadProjectSource("Main", "IoC.cs");
        if (iocSource == null)
            Assert.Inconclusive("Main/IoC.cs not found — run from repo root or check working directory");

        StringAssert.Contains(iocSource, "MountDespawnIoC.RegisterMountDespawnFeature(container);",
            "Main/IoC.cs::Configure must call MountDespawnIoC.RegisterMountDespawnFeature(container). " +
            "Without it the SubModule's IoC.Resolve<IDeadMountDespawnService>() throws at mission start.");
    }

    [TestMethod]
    public void MainSubModule_AddsMountDespawnMissionBehaviorOnMissionInit()
    {
        var subModuleSource = ReadProjectSource("Main", "SubModule.cs");
        if (subModuleSource == null)
            Assert.Inconclusive("Main/SubModule.cs not found — run from repo root or check working directory");

        StringAssert.Contains(subModuleSource, "new Features.MountDespawn.Hooks.MountDespawnMissionBehavior(",
            "Main/SubModule.cs must register MountDespawnMissionBehavior via AddTaomBehavior(...) from " +
            "inside OnMissionBehaviorInitialize.");

        StringAssert.Contains(subModuleSource, "OnMissionBehaviorInitialize",
            "Main/SubModule.cs must override OnMissionBehaviorInitialize so AddMissionBehavior runs at mission start.");
    }

    [TestMethod]
    public void MountDespawnMissionBehavior_IsMissionBehavior()
    {
        Assert.IsTrue(typeof(MissionBehavior).IsAssignableFrom(typeof(MountDespawnMissionBehavior)),
            "MountDespawnMissionBehavior must inherit MissionBehavior so mission.AddMissionBehavior accepts it.");
    }

    [TestMethod]
    public void MountDespawnMissionBehavior_BehaviorType_IsOther()
    {
        // Returning Logic here makes vanilla AddMissionBehavior run `MissionLogics.Add(this as MissionLogic)`,
        // which evaluates to null and NREs on the next CheckMissionEnded tick. Four TAOM behaviors carry
        // the same comment; this pins it for the fifth.
        var sut = new MountDespawnMissionBehavior(
            Substitute.For<IDeadMountDespawnService>(),
            Substitute.For<IModLogger>());

        Assert.AreEqual(MissionBehaviorType.Other, sut.BehaviorType);
    }

    [TestMethod]
    public void MissionGate_NullMission_IsNotEligible()
    {
        Assert.IsFalse(MountDespawnMissionGate.IsEligible(null));
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
