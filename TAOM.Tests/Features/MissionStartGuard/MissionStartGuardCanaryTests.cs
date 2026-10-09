using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.MissionStartGuard;
using TAOM.Features.MissionStartGuard.Hooks;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.MissionStartGuard;

/// <summary>
/// The lost-guard canary against real Harmony 2.4.2, with Patch103's real prefix and finalizer attached to an instance
/// dummy method. Harmony runs the finalizers inside the try and runs every one of them again when a later finalizer
/// throws; the finalizer sorts first, so a flag the finalizer clears would read false on that rerun and write a false
/// "the guard's prefix did not run" warning (which also uses up the once-per-process latch). The prefix's own flag
/// travels in Harmony's <c>__state</c> instead.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class MissionStartGuardCanaryTests
{
    private const string HarmonyId = "taom.tests.missionstartguard.canary";

    private static bool _gameLoaded;
    private static int _sink;

    private IModLogger _logger = null!;
    private Harmony _harmony = null!;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private sealed class DummyMission
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void AfterStart() => _sink++;
    }

    // A patch class whose apply fails after its transpiler ran, with a void [HarmonyCleanup] taking the Exception (the shape
    // of Patch103's Cleanup): Harmony 2.4.2's PatchClassProcessor must call it with the failure and still throw its own.
    [HarmonyPatch(typeof(DummyMission), nameof(DummyMission.AfterStart))]
    private static class FailingApply
    {
        internal static Exception? Seen;
        internal static int Calls, CallsWithoutFailure;

        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
            throw new InvalidOperationException("the transpiler failed");

        [HarmonyCleanup]
        public static void Cleanup(Exception? ex)
        {
            Calls++;
            if (ex == null) CallsWithoutFailure++;
            Seen = ex;
        }
    }

    private static void ThrowingFinalizer() => throw new InvalidOperationException("a finalizer after the guard's");

    [TestInitialize]
    public void Setup()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
        _logger = Substitute.For<IModLogger>();
        var settings = Substitute.For<IMissionStartGuardSettingsProvider>();
        settings.SurviveMissionStartFailures.Returns(true);
        var service = new MissionStartGuardService(settings, Substitute.For<IMissionStartGuardAdapter>(),
            Substitute.For<IDedicatedServerProvider>(), _logger);
        MissionStartGuardCalls.Initialize(service);
        MissionStartGuardSwaps.LastSwapped = MissionStartGuardService.ExpectedSites;
        _harmony = new Harmony(HarmonyId);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _harmony?.UnpatchAll(HarmonyId);
        MissionStartGuardCalls.Initialize(null);
        MissionStartGuardSwaps.LastSwapped = 0;
    }

    private static HarmonyMethod Fix(string name) =>
        new HarmonyMethod(typeof(Mission_AfterStart_MissionStartGuard_Patch).GetMethod(name));

    private static MethodInfo Dummy() => typeof(DummyMission).GetMethod(nameof(DummyMission.AfterStart))!;

    private string[] PrefixWarnings() => _logger.ReceivedCalls()
        .Where(c => c.GetMethodInfo().Name == nameof(IModLogger.LogWarning))
        .Select(c => (string)c.GetArguments()[0]!)
        .Where(w => w.Contains("prefix"))
        .ToArray();

    [TestMethod]
    public void LaterFinalizerThatThrows_HarmonyRerunsEveryFinalizer_ButThePrefixRanSoNothingIsWarned()
    {
        _harmony.Patch(Dummy(), prefix: Fix("Prefix"), finalizer: Fix("Finalizer"));
        _harmony.Patch(Dummy(), finalizer: new HarmonyMethod(typeof(MissionStartGuardCanaryTests)
            .GetMethod(nameof(ThrowingFinalizer), BindingFlags.NonPublic | BindingFlags.Static)) { priority = Priority.Last });
        var finalizers = Harmony.GetPatchInfo(Dummy()).Finalizers;
        Assert.AreEqual(2, finalizers.Count);
        Assert.AreEqual(typeof(Mission_AfterStart_MissionStartGuard_Patch).GetMethod("Finalizer"), finalizers[0].PatchMethod,
            "the guard's finalizer sorts first, the thrower after it");

        Assert.ThrowsException<InvalidOperationException>(() => new DummyMission().AfterStart());

        Assert.AreEqual(0, PrefixWarnings().Length, string.Join(" | ", PrefixWarnings()));
    }

    [TestMethod]
    public void AfterAHarmonyRerun_APrefixStrippedLaterIsStillReportedOnce()
    {
        _harmony.Patch(Dummy(), prefix: Fix("Prefix"), finalizer: Fix("Finalizer"));
        _harmony.Patch(Dummy(), finalizer: new HarmonyMethod(typeof(MissionStartGuardCanaryTests)
            .GetMethod(nameof(ThrowingFinalizer), BindingFlags.NonPublic | BindingFlags.Static)) { priority = Priority.Last });
        Assert.ThrowsException<InvalidOperationException>(() => new DummyMission().AfterStart());
        Assert.AreEqual(0, PrefixWarnings().Length);

        // PatchShield strips a shielded owner's prefix and never its finalizers.
        _harmony.Unpatch(Dummy(), HarmonyPatchType.Prefix, HarmonyId);
        Assert.ThrowsException<InvalidOperationException>(() => new DummyMission().AfterStart());

        Assert.AreEqual(1, PrefixWarnings().Length, "one warning for the real strip, none for the rerun of the same call");
    }

    [TestMethod]
    public void VoidCleanupTakingTheException_IsCalledByHarmonyWhenTheApplyFails_AndHarmonyStillThrows()
    {
        FailingApply.Seen = null;
        FailingApply.Calls = 0;
        FailingApply.CallsWithoutFailure = 0;

        Exception? thrown = null;
        try { _harmony.CreateClassProcessor(typeof(FailingApply)).Patch(); }
        catch (Exception ex) { thrown = ex; }

        Assert.IsNotNull(thrown, "a void cleanup leaves Harmony's own exception in place");
        // Harmony calls it once per patch job and once more for the class; every call here is a failed apply.
        Assert.IsTrue(FailingApply.Calls >= 1);
        Assert.AreEqual(0, FailingApply.CallsWithoutFailure, "the cleanup received the failure");
        Assert.IsNotNull(FailingApply.Seen);
        Assert.AreEqual(0, Harmony.GetPatchInfo(Dummy())?.Transpilers.Count ?? 0, "nothing was registered");
    }

    [TestMethod]
    public void Patch103sCleanup_AfterATranspilerThatRan_ZeroesTheSwapCountOnlyOnFailure()
    {
        MissionStartGuardSwaps.LastSwapped = 6;
        Mission_AfterStart_MissionStartGuard_Patch.Cleanup(null);
        Assert.AreEqual(6, MissionStartGuardSwaps.LastSwapped);

        Mission_AfterStart_MissionStartGuard_Patch.Cleanup(new InvalidOperationException("apply failed"));

        Assert.AreEqual(0, MissionStartGuardSwaps.LastSwapped);
    }

    [TestMethod]
    public void HealthyCall_PrefixAndFinalizerPaired_WritesNothing()
    {
        _harmony.Patch(Dummy(), prefix: Fix("Prefix"), finalizer: Fix("Finalizer"));

        new DummyMission().AfterStart();
        new DummyMission().AfterStart();

        Assert.AreEqual(0, _logger.ReceivedCalls().Count());
    }
}
