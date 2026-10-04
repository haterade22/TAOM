using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.BattleLoadDiagnostics;
using TAOM.Features.MapPerf;
using TAOM.Features.MapPerf.Hooks;
using TAOM.Features.MissionPerf;
using TAOM.Features.TimeAcceleration;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.MapPerf;

/// <summary>
/// The Patch101 installer's decisions: off applies nothing and says so; on, it applies its two categories
/// once per process, creates the profiler only when the core category applied and all six core targets are
/// patched, and names what is missing; a later game init only reports. The category apply is a fake that sets
/// the <c>CampaignEvents.Tick</c> site count the real transpiler would; the patched check is a fake. Binds the
/// real listener walk and reflects over TAOM's map views (SandBox.View, loaded from the game folder), so it
/// needs the game assemblies.
/// </summary>
[TestCategory("RequiresGame")]
[TestClass]
public class MapFrameProfilerInstallerTests
{
    private IModLogger _logger = null!;
    private IBattleLoadDiagnosticsSettingsProvider _settings = null!;
    private ITimeControlAdapter _time = null!;
    private ITimeAccelerationSettingsProvider _acceleration = null!;
    private List<string> _applied = null!;
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    [TestInitialize]
    public void Setup()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
        MapFrameProfilerInstaller.ResetForTests();
        _logger = Substitute.For<IModLogger>();
        _settings = Substitute.For<IBattleLoadDiagnosticsSettingsProvider>();
        _time = Substitute.For<ITimeControlAdapter>();
        _acceleration = Substitute.For<ITimeAccelerationSettingsProvider>();
        _applied = new List<string>();
    }

    [TestCleanup]
    public void Cleanup() => MapFrameProfilerInstaller.ResetForTests();

    private Func<string, bool> Apply(bool core = true, bool views = true, int sites = 1) => category =>
    {
        _applied.Add(category);
        if (category == MapFrameProfilerInstaller.Category)
        {
            MapFrameProfilerHooks.TickEventSites = sites;
            return core;
        }
        Assert.AreEqual(MapFrameProfilerInstaller.ViewsCategory, category);
        return views;
    };

    private void Install(Func<string, bool> apply, Func<MethodBase, bool>? isPatched = null) =>
        MapFrameProfilerInstaller.OnGameInitialized(_settings, _time, _acceleration, _logger, apply, isPatched ?? (_ => true));

    private static int ViewCount => MapProfilerTargets.MapViewTargets().Count;

    [TestMethod]
    public void OnGameInitialized_ToggleOff_LogsTheOffLineAndAppliesNothing()
    {
        _settings.MapProfilerEnabled.Returns(false);

        Install(Apply());

        Assert.AreEqual(0, _applied.Count);
        _logger.Received(1).LogInfo(MapProfileLines.OffLine);
        Assert.IsNull(MapFrameProfilerHooks.Profiler);
    }

    [TestMethod]
    public void OnGameInitialized_AllPatched_IsInstalledAndLogsThePinnedInstallLine()
    {
        _settings.MapProfilerEnabled.Returns(true);
        var v = ViewCount;

        Install(Apply());

        CollectionAssert.AreEqual(new[] { MapFrameProfilerInstaller.Category, MapFrameProfilerInstaller.ViewsCategory }, _applied);
        _logger.Received(1).LogInfo(MapProfileLines.BuildInstallLine(true, true, 6, 6, Array.Empty<string>(), v, v, 1, true,
            AllocationCounter.Available));
        Assert.IsNotNull(MapFrameProfilerHooks.Profiler);
        Assert.IsTrue(TickEventListenerWalker.Bound);
        Assert.AreSame(_logger, MapFrameProfilerHooks.Logger);
        Assert.AreSame(_settings, MapSessionHooks.Settings);
        Assert.AreSame(_time, MapSessionHooks.TimeControl);
        Assert.AreSame(_acceleration, MapSessionHooks.Acceleration);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void OnGameInitialized_CoreCategoryFails_IsNotInstalled()
    {
        _settings.MapProfilerEnabled.Returns(true);
        var v = ViewCount;

        Install(Apply(core: false));

        Assert.IsNull(MapFrameProfilerHooks.Profiler);
        _logger.Received(1).LogInfo(MapProfileLines.BuildInstallLine(false, true, 6, 6, Array.Empty<string>(), v, v, 1, true,
            AllocationCounter.Available));
    }

    // DECISIONS D6: a failed install says at that same game start that nothing will be measured, not only
    // at a later one (a player who runs one campaign per process would never see it).
    [TestMethod]
    public void OnGameInitialized_FirstInstallFails_WarnsNothingIsMeasured()
    {
        _settings.MapProfilerEnabled.Returns(true);

        Install(Apply(), _ => false);

        Assert.IsNull(MapFrameProfilerHooks.Profiler);
        _logger.Received(1).LogWarning(MapProfileLines.NotInstalledLine);
    }

    [TestMethod]
    public void IsPatchedByThisProfiler_RealPatch101PrefixOnly_True()
    {
        var harmony = new Harmony("taom.tests.mapprofiler.ispatched");
        try
        {
            var ours = AccessTools.Method(typeof(MapFrameProfilerInstallerTests), nameof(PatchedByProfiler));
            var foreign = AccessTools.Method(typeof(MapFrameProfilerInstallerTests), nameof(PatchedByOther));
            var bare = AccessTools.Method(typeof(MapFrameProfilerInstallerTests), nameof(Unpatched));
            harmony.Patch(ours, prefix: new HarmonyMethod(AccessTools.Method(typeof(Campaign_Tick_MapProfiler_Patch), "Prefix")));
            harmony.Patch(foreign, prefix: new HarmonyMethod(AccessTools.Method(typeof(MapFrameProfilerInstallerTests), nameof(ForeignPrefix))));

            Assert.IsTrue(MapFrameProfilerInstaller.IsPatchedByThisProfiler(ours));
            Assert.IsFalse(MapFrameProfilerInstaller.IsPatchedByThisProfiler(foreign));
            Assert.IsFalse(MapFrameProfilerInstaller.IsPatchedByThisProfiler(bare));
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
    }

    // PatchShield's strip (PatchShield.cs:405-407) unpatches every prefix, postfix and transpiler of each
    // non-protected owner on the method that caught a swallowed throw; com.taom.mod is one, so Patch101's pair
    // goes with the rest. The patched check, which the installer runs once and the session hooks run again at
    // each session start, window start and measuring session end, must see that. Same Harmony calls, on a test method.
    [TestMethod]
    public void IsPatchedByThisProfiler_AfterThePatchShieldStripCalls_False()
    {
        var harmony = new Harmony("taom.tests.mapprofiler.strip");
        try
        {
            var target = AccessTools.Method(typeof(MapFrameProfilerInstallerTests), nameof(StrippedByShield));
            harmony.Patch(target,
                prefix: new HarmonyMethod(AccessTools.Method(typeof(Campaign_RealTick_MapProfiler_Patch), "Prefix")),
                postfix: new HarmonyMethod(AccessTools.Method(typeof(Campaign_RealTick_MapProfiler_Patch), "Postfix")));
            Assert.IsTrue(MapFrameProfilerInstaller.IsPatchedByThisProfiler(target), "Patched before the strip.");

            foreach (var type in new[] { HarmonyPatchType.Prefix, HarmonyPatchType.Postfix, HarmonyPatchType.Transpiler })
                harmony.Unpatch(target, type, harmony.Id);

            Assert.IsFalse(MapFrameProfilerInstaller.IsPatchedByThisProfiler(target), "The strip leaves nothing the check can find.");
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int StrippedByShield() => 4;

    [TestMethod]
    public void UnpatchedCore_NamesEveryCoreTargetTheCheckRejects_InCoreOrder()
    {
        var names = MapProfilerTargets.CoreTargetNames.ToList();

        CollectionAssert.AreEqual(new[] { "Campaign.RealTick", "MapScreen.OnFrameTick" }, MapProfilerTargets.UnpatchedCore(
            m => !(m.Name == "RealTick" || (m.Name == "OnFrameTick" && m.DeclaringType?.Name == "MapScreen"))));
        Assert.AreEqual(0, MapProfilerTargets.UnpatchedCore(_ => true).Count);
        CollectionAssert.AreEqual(names, MapProfilerTargets.UnpatchedCore(_ => false));
    }

    // The installer hands the session hooks the very check it installed with, so the hooks' later look at the
    // six core targets cannot disagree with the install line about what "patched" means.
    [TestMethod]
    public void OnGameInitialized_Installed_HandsTheSessionHooksTheSamePatchedCheck()
    {
        _settings.MapProfilerEnabled.Returns(true);
        var patched = true;

        Install(Apply(), _ => patched);

        Assert.IsNotNull(MapSessionHooks.LostHooks);
        Assert.AreEqual(0, MapSessionHooks.LostHooks!().Count);
        patched = false;
        CollectionAssert.AreEqual(MapProfilerTargets.CoreTargetNames.ToList(), MapSessionHooks.LostHooks!().ToList());
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int PatchedByProfiler() => 1;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int PatchedByOther() => 2;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Unpatched() => 3;

    private static void ForeignPrefix() { }

    [TestMethod]
    public void OnGameInitialized_ATargetUnpatched_IsNotInstalledAndTheLineNamesIt()
    {
        _settings.MapProfilerEnabled.Returns(true);

        Install(Apply(), m => !(m.Name == "OnFrameTick" && m.DeclaringType?.Name == "MapScreen"));

        Assert.IsNull(MapFrameProfilerHooks.Profiler);
        _logger.Received(1).LogInfo(Arg.Is<string>(s => s.Contains("targets 5/6 patched (missing MapScreen.OnFrameTick)")
            && s.Contains("category applied,")));
    }

    [TestMethod]
    public void OnGameInitialized_ViewsCategoryFails_StillInstalls()
    {
        _settings.MapProfilerEnabled.Returns(true);
        var v = ViewCount;

        Install(Apply(views: false), m => !m.Name.StartsWith("OnMapScreenUpdate", StringComparison.Ordinal));

        Assert.IsNotNull(MapFrameProfilerHooks.Profiler);
        _logger.Received(1).LogInfo(MapProfileLines.BuildInstallLine(true, false, 6, 6, Array.Empty<string>(), 0, v, 1, true,
            AllocationCounter.Available));
        _logger.Received(1).LogWarning(MapProfileLines.BuildViewsShortLine(0, v));
    }

    [TestMethod]
    public void OnGameInitialized_TickEventSiteMissing_StillInstallsAndLogsTheVanillaLine()
    {
        _settings.MapProfilerEnabled.Returns(true);

        Install(Apply(sites: 0));

        Assert.IsNotNull(MapFrameProfilerHooks.Profiler);
        _logger.Received(1).LogWarning(MapProfileLines.BuildTickEventVanillaLine(0));
    }

    [TestMethod]
    public void OnGameInitialized_SecondCall_AppliesNothingAgain()
    {
        _settings.MapProfilerEnabled.Returns(true);

        Install(Apply());
        var profiler = MapFrameProfilerHooks.Profiler;
        Install(Apply());

        Assert.AreEqual(2, _applied.Count, "Each category is applied once per process.");
        Assert.AreSame(profiler, MapFrameProfilerHooks.Profiler);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
        _logger.Received(1).LogInfo(Arg.Any<string>());
    }

    [TestMethod]
    public void OnGameInitialized_LaterCall_OffAtFirstOnNow_LogsRestartNeeded()
    {
        _settings.MapProfilerEnabled.Returns(false);
        Install(Apply());
        _settings.MapProfilerEnabled.Returns(true);

        Install(Apply());

        Assert.AreEqual(0, _applied.Count);
        _logger.Received(1).LogInfo(MapProfileLines.RestartNeededLine);
    }

    [TestMethod]
    public void OnGameInitialized_LaterCall_InstallFailedToggleOn_LogsNotInstalled()
    {
        _settings.MapProfilerEnabled.Returns(true);
        Install(Apply(core: false));
        _logger.ClearReceivedCalls();

        Install(Apply(core: false));

        Assert.AreEqual(2, _applied.Count);
        _logger.Received(1).LogWarning(MapProfileLines.NotInstalledLine);
    }

    [TestMethod]
    public void OnGameInitialized_ApplyThrows_LogsOneFault()
    {
        _settings.MapProfilerEnabled.Returns(true);
        var boom = new InvalidOperationException("apply broke");

        Install(_ => throw boom);

        Assert.IsNull(MapFrameProfilerHooks.Profiler);
        _logger.Received(1).LogError(MapProfileLines.BuildInstallFault(boom));
    }
}
