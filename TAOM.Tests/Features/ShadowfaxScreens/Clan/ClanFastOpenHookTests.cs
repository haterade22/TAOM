using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.MissionPerf;
using TAOM.Features.ShadowfaxScreens;
using TAOM.Features.ShadowfaxScreens.Clan;
using TAOM.Features.ShadowfaxScreens.Clan.Hooks;
using TAOM.Features.ShadowfaxScreens.Diagnostics;
using TAOM.Tests.Features.LoadTimeStamps;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.ShadowfaxScreens.Clan;

/// <summary>
/// The Clan fast open hooks against real Harmony 2.4.2 on dummy classes of vanilla's shape: a RefreshValues that calls
/// RefreshPartiesList, and a screen constructor that builds the VM. The real transpiler cannot name a dummy, so these
/// tests swap the dummy's call with <see cref="SkipOrBuild"/>, which makes the same decision as
/// <see cref="ClanPartiesVM_RefreshValues_ClanFastOpen_Patch.RefreshPartiesListInRefresh"/> through the same service; the swap itself is checked
/// against the real IL in ClanFastOpenBindingTests.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class ClanFastOpenHookTests
{
    private const string HarmonyId = "taom.tests.clanfastopen.hooks";
    private const BindingFlags Static = BindingFlags.NonPublic | BindingFlags.Static;

    private static bool _gameLoaded;
    private static int _postfixRuns;

    private IShadowfaxScreensSettingsProvider _settings = null!;
    private FakeStampClock _clock = null!;
    private RecordingLogger _logger = null!;
    private ClanScreenFastPathService _service = null!;
    private Harmony _harmony = null!;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private sealed class DummyParties
    {
        public int Builds;
        public int Refreshes;
        public bool ThrowInBuild;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void RefreshValues()
        {
            Refreshes++;
            RefreshPartiesList();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void RefreshPartiesList()
        {
            if (ThrowInBuild) throw new InvalidOperationException("build failed");
            Builds++;
        }
    }

    private sealed class DummyScreen
    {
        public DummyScreen()
        {
            var parties = new DummyParties();
            parties.RefreshPartiesList();
            parties.RefreshValues();
            parties.RefreshValues();
        }
    }

    // Stands in for ClanPartiesVM_RefreshValues_ClanFastOpen_Patch.RefreshPartiesListInRefresh: the same service decision, over the dummy.
    private static void SkipOrBuild(DummyParties parties)
    {
        if (ClanFastOpenCalls.Service?.ShouldSkipListBuild() == true) return;
        parties.RefreshPartiesList();
    }

    private static MethodInfo SkipOrBuildMethod => typeof(ClanFastOpenHookTests).GetMethod(nameof(SkipOrBuild), Static)!;

    private static IEnumerable<CodeInstruction> SwapTranspiler(IEnumerable<CodeInstruction> instructions) =>
        TickProfilerTranspiler.Rewrite(instructions,
            new[] { new CallSwap(typeof(DummyParties).GetMethod(nameof(DummyParties.RefreshPartiesList))!, SkipOrBuildMethod) },
            "Dummy.RefreshValues", null, out _);

    // Two helpers for the one occurrence: the site count does not match, so the rewrite bails.
    private static IEnumerable<CodeInstruction> MismatchedTranspiler(IEnumerable<CodeInstruction> instructions) =>
        TickProfilerTranspiler.Rewrite(instructions,
            new[] { new CallSwap(typeof(DummyParties).GetMethod(nameof(DummyParties.RefreshPartiesList))!, new[] { SkipOrBuildMethod, SkipOrBuildMethod }) },
            "Dummy.RefreshValues", null, out _);

    private static void CountingPostfix() => _postfixRuns++;

    [TestInitialize]
    public void Setup()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
        _settings = Substitute.For<IShadowfaxScreensSettingsProvider>();
        _settings.ClanFastOpen.Returns(true);
        _settings.LogScreenOpenTimes.Returns(true);
        var coop = Substitute.For<ICoopPresenceProvider>();
        _clock = new FakeStampClock { Frequency = 10000, Now = 5000 };
        _logger = new RecordingLogger();
        _service = new ClanScreenFastPathService(_settings, coop, _clock, Substitute.For<IModLogger>());
        ClanFastOpenCalls.Initialize(_service);
        ScreenOpenTiming.Initialize(new ScreenOpenTimer(_clock, _logger, _settings));
        _harmony = new Harmony(HarmonyId);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _harmony?.UnpatchAll(HarmonyId);
        ClanFastOpenCalls.Initialize(null);
        ScreenOpenTiming.Initialize(null);
    }

    private static HarmonyMethod Fix(Type hook, string name) => new HarmonyMethod(hook.GetMethod(name));

    private void PatchRefreshValues(string transpiler = nameof(SwapTranspiler)) => _harmony.Patch(
        typeof(DummyParties).GetMethod(nameof(DummyParties.RefreshValues)),
        transpiler: new HarmonyMethod(typeof(ClanFastOpenHookTests).GetMethod(transpiler, Static)));

    private void PatchRefreshPartiesList() => _harmony.Patch(
        typeof(DummyParties).GetMethod(nameof(DummyParties.RefreshPartiesList)),
        prefix: Fix(typeof(ClanPartiesVM_RefreshPartiesList_ClanFastOpen_Patch), "Prefix"),
        finalizer: Fix(typeof(ClanPartiesVM_RefreshPartiesList_ClanFastOpen_Patch), "Finalizer"));

    private void PatchCtor(bool prefix = true) => _harmony.Patch(
        typeof(DummyScreen).GetConstructor(Type.EmptyTypes),
        prefix: prefix ? Fix(typeof(ClanManagementVM_Ctor_ClanFastOpen_Patch), "Prefix") : null,
        finalizer: Fix(typeof(ClanManagementVM_Ctor_ClanFastOpen_Patch), "Finalizer"));

    [TestMethod]
    public void APostfixOnTheListBuild_RunsOncePerRealBuild_NotForTheSkippedCall()
    {
        _postfixRuns = 0;
        PatchRefreshValues();
        PatchRefreshPartiesList();
        _harmony.Patch(typeof(DummyParties).GetMethod(nameof(DummyParties.RefreshPartiesList)),
            postfix: new HarmonyMethod(typeof(ClanFastOpenHookTests).GetMethod(nameof(CountingPostfix), Static)));
        var parties = new DummyParties();

        parties.RefreshValues();
        parties.RefreshPartiesList();

        Assert.AreEqual(1, parties.Builds);
        Assert.AreEqual(1, _postfixRuns, "the skip leaves the call site, so no postfix of the list build fires for it");
    }

    [TestMethod]
    public void InnerBuildInsideRefreshValues_IsSkipped_ADirectCallStillBuilds()
    {
        PatchRefreshValues();
        PatchRefreshPartiesList();
        var parties = new DummyParties();

        parties.RefreshValues();
        Assert.AreEqual(0, parties.Builds, "the build inside RefreshValues is skipped");
        Assert.AreEqual(1, parties.Refreshes);

        parties.RefreshPartiesList();
        Assert.AreEqual(1, parties.Builds, "a direct call builds");
    }

    [TestMethod]
    public void ToggleOff_EveryBuildRuns()
    {
        _settings.ClanFastOpen.Returns(false);
        PatchRefreshValues();
        PatchRefreshPartiesList();
        var parties = new DummyParties();

        parties.RefreshValues();

        Assert.AreEqual(1, parties.Builds);
    }

    [TestMethod]
    public void TranspilerWithTheWrongSiteCount_LeavesRefreshValuesVanilla()
    {
        PatchRefreshValues(nameof(MismatchedTranspiler));
        PatchRefreshPartiesList();
        var parties = new DummyParties();

        parties.RefreshValues();

        Assert.AreEqual(1, parties.Builds, "the soft-fail keeps vanilla's build");
    }

    [TestMethod]
    public void TheRealSwap_FindingNoClanPartiesCall_LeavesTheMethodVanilla()
    {
        var il = PatchProcessor.GetOriginalInstructions(typeof(DummyParties).GetMethod(nameof(DummyParties.RefreshValues))).ToList();

        var result = TickProfilerTranspiler.Rewrite(il, ClanPartiesVM_RefreshValues_ClanFastOpen_Patch.BuildSwaps(), "Dummy", null, out var swapped);

        Assert.AreEqual(0, swapped, "the real swap targets ClanPartiesVM, which the dummy never calls");
        Assert.AreEqual(il.Count, result.Count);
    }

    [TestMethod]
    public void ListBuildThatThrows_PropagatesAsInVanilla()
    {
        PatchRefreshValues();
        PatchRefreshPartiesList();
        var parties = new DummyParties { ThrowInBuild = true };

        parties.RefreshValues();
        Assert.ThrowsException<InvalidOperationException>(() => parties.RefreshPartiesList());
    }

    [TestMethod]
    public void Ctor_WritesOneOpenLine_WithTheBuildCounts()
    {
        PatchRefreshValues();
        PatchRefreshPartiesList();
        PatchCtor();

        new DummyScreen();

        var line = _logger.Lines.Single(l => l.Contains("[ScreenOpen]"));
        StringAssert.Contains(line, "[ScreenOpen] Clan open: ");
        StringAssert.EndsWith(line, "(fast open on; list builds: 1 run, 2 skipped, 0.0 ms)");
    }

    [TestMethod]
    public void Ctor_TimerOff_WritesNothing_AndOpensNoCountWindow()
    {
        _settings.LogScreenOpenTimes.Returns(false);
        PatchRefreshValues();
        PatchRefreshPartiesList();
        PatchCtor();

        new DummyScreen();

        Assert.AreEqual(0, _logger.Lines.Count);
        Assert.AreEqual("", _service.EndOpen(), "no window was opened");
    }

    [TestMethod]
    public void Ctor_FinalizerWithoutItsPrefix_WritesNothing()
    {
        PatchCtor(prefix: false);

        new DummyScreen();

        Assert.AreEqual(0, _logger.Lines.Count);
    }
}
