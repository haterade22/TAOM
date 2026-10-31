using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.MountAndBlade;
using TAOM.Features.FordsOfWar;
using TAOM.Features.FordsOfWar.Hooks;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.FordsOfWar;

/// <summary>The prefix body, run directly. The ranged branch needs a real item and is covered by the binding test and the in-game check.</summary>
[TestClass]
[TestCategory("RequiresGame")]
public class FordsTowerHitPatchTests
{
    private IFordsTowerHitHook? _installed;

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => GameAssemblies.EnsureLoaded();

    [TestCleanup]
    public void Uninstall()
    {
        if (_installed != null)
            FordsTowerHitPatch.Uninstall(_installed);
    }

    private IFordsTowerHitHook Install(Func<int> damage)
    {
        var hook = Substitute.For<IFordsTowerHitHook>();
        hook.AdjustTowerDamage(Arg.Any<object>(), Arg.Any<object?>(), Arg.Any<bool>(), Arg.Any<int>()).Returns(_ => damage());
        FordsTowerHitPatch.Install(hook);
        _installed = hook;
        return hook;
    }

    [TestMethod]
    public void Prefix_NoHookInstalled_LeavesTheDamage()
    {
        var damage = 40;
        var weapon = default(MissionWeapon);

        FordsTowerHitPatch.Prefix(new object(), null!, ref damage, ref weapon);

        Assert.AreEqual(40, damage);
    }

    [TestMethod]
    public void Prefix_HookInstalledAndAnEmptyWeapon_LeavesTheDamageForVanillaToZero()
    {
        var hook = Install(() => 7);
        var damage = 40;
        var weapon = default(MissionWeapon);

        FordsTowerHitPatch.Prefix(new object(), null!, ref damage, ref weapon);

        Assert.AreEqual(40, damage);
        hook.DidNotReceiveWithAnyArgs().AdjustTowerDamage(default!, default, default, default);
    }

    [TestMethod]
    public void Run_ArmedBlow_WritesBackTheHooksDamageAndPassesTheCallersThrough()
    {
        var hook = Substitute.For<IFordsTowerHitHook>();
        var component = new object();
        var attacker = new object();
        hook.AdjustTowerDamage(component, attacker, true, 40).Returns(7);
        var damage = 40;

        FordsTowerHitPatch.Run(hook, component, attacker, false, true, ref damage);

        Assert.AreEqual(7, damage);
    }

    [TestMethod]
    public void Run_UnarmedBlow_LeavesTheDamageWithoutAskingTheHook()
    {
        var hook = Substitute.For<IFordsTowerHitHook>();
        var damage = 40;

        FordsTowerHitPatch.Run(hook, new object(), null, true, false, ref damage);

        Assert.AreEqual(40, damage);
        hook.DidNotReceiveWithAnyArgs().AdjustTowerDamage(default!, default, default, default);
    }

    [TestMethod]
    public void Run_HookThrows_LeavesTheDamageAndDoesNotThrow()
    {
        var hook = Substitute.For<IFordsTowerHitHook>();
        hook.AdjustTowerDamage(Arg.Any<object>(), Arg.Any<object?>(), Arg.Any<bool>(), Arg.Any<int>())
            .Returns(_ => throw new InvalidOperationException("boom"));
        var damage = 40;

        FordsTowerHitPatch.Run(hook, new object(), null, false, false, ref damage);

        Assert.AreEqual(40, damage);
    }

    [TestMethod]
    public void Uninstall_AnotherHooksInstall_LeavesTheCurrentHookInPlace()
    {
        Install(() => 5);

        FordsTowerHitPatch.Uninstall(Substitute.For<IFordsTowerHitHook>());

        Assert.IsTrue(FordsTowerHitPatch.HasHook);
    }

    [TestMethod]
    public void Uninstall_TheInstalledHook_RemovesIt()
    {
        var hook = Install(() => 5);
        Assert.IsTrue(FordsTowerHitPatch.HasHook);

        FordsTowerHitPatch.Uninstall(hook);

        Assert.IsFalse(FordsTowerHitPatch.HasHook);
    }
}
