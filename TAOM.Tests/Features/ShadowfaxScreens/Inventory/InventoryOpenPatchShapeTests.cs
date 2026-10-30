using System;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem.Inventory;
using TAOM.Features.ShadowfaxScreens;
using TAOM.Features.ShadowfaxScreens.Diagnostics;
using TAOM.Features.ShadowfaxScreens.Inventory;
using TAOM.Features.ShadowfaxScreens.Inventory.Hooks;
using TAOM.Tests.Features.LoadTimeStamps;

namespace TAOM.Tests.Features.ShadowfaxScreens.Inventory;

/// <summary>
/// The SPInventoryVM constructor bracket and the InitializeInventory phase, applied with real Harmony around
/// stand-ins that carry the engine's parameter and field names. A stripped prefix leaves the finalizer a no-op and
/// a throw still ends the window and reaches the caller with its throw site. RequiresGame because the hook classes'
/// attributes name engine types.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class InventoryOpenPatchShapeTests
{
    private const string HarmonyId = "taom.tests.shadowfaxinventory";

    private FakeStampClock _clock = null!;
    private RecordingLogger _logger = null!;
    private InventoryOpenFastPathService _service = null!;

    public sealed class FakeInventoryVm
    {
        public static Action? Work;

        public InventoryScreenHelper.InventoryMode _usageType = InventoryScreenHelper.InventoryMode.Default;
        public bool Throws;

        public FakeInventoryVm(InventoryLogic? inventoryLogic, bool isInCivilianModeByDefault, Func<int, int>? getItemUsageSetFlags)
        {
            _usageType = InventoryScreenHelper.InventoryMode.Trade;
            Work?.Invoke();
            if (inventoryLogic == null && isInCivilianModeByDefault) Thrower();
        }

        public void InitializeInventory() => Work?.Invoke();

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Thrower() => throw new InvalidOperationException("ctor failed");
    }

    [TestInitialize]
    public void Setup()
    {
        _clock = new FakeStampClock();
        _logger = new RecordingLogger();
        var settings = Substitute.For<IShadowfaxScreensSettingsProvider>();
        settings.LogScreenOpenTimes.Returns(true);
        settings.InventoryFastOpen.Returns(true);
        _service = new InventoryOpenFastPathService(settings, _clock, _logger);
        ScreenOpenTiming.Initialize(new ScreenOpenTimer(_clock, _logger, settings));
        ShadowfaxInventoryCalls.Initialize(_service);
    }

    [TestCleanup]
    public void Cleanup()
    {
        new Harmony(HarmonyId).UnpatchAll(HarmonyId);
        FakeInventoryVm.Work = null;
        ScreenOpenTiming.Initialize(null);
        ShadowfaxInventoryCalls.Initialize(null);
    }

    private static void PatchCtor(bool withPrefix)
    {
        var ctor = AccessTools.Constructor(typeof(FakeInventoryVm), new[] { typeof(InventoryLogic), typeof(bool), typeof(Func<int, int>) });
        new Harmony(HarmonyId).Patch(ctor,
            prefix: withPrefix ? new HarmonyMethod(typeof(SPInventoryVM_Ctor_ShadowfaxPatch).GetMethod("Prefix")) : null,
            finalizer: new HarmonyMethod(typeof(SPInventoryVM_Ctor_ShadowfaxPatch).GetMethod("Finalizer")));
    }

    [TestMethod]
    public void Ctor_LogsOneOpenLine_WithTheModeTheVmRead_AndUnknownCountsForANullLogic()
    {
        PatchCtor(withPrefix: true);

        _ = new FakeInventoryVm(null, false, null);

        Assert.AreEqual("INFO [ScreenOpen] Inventory open: 0.0 ms (fast open on; mode=Trade, left=n/a, right=n/a, rows=0.0 ms, sorts skipped=0)",
            _logger.Lines.Single());
        Assert.IsFalse(_service.OpenActive);
    }

    [TestMethod]
    public void Ctor_Throws_StillEndsTheWindow_AndRethrowsWithTheThrowSite()
    {
        PatchCtor(withPrefix: true);

        var thrown = Assert.ThrowsException<InvalidOperationException>(() => new FakeInventoryVm(null, true, null));

        Assert.AreEqual("ctor failed", thrown.Message);
        StringAssert.Contains(thrown.StackTrace, "Thrower");
        StringAssert.Contains(_logger.Lines.Single(), "Inventory open: 0.0 ms, threw InvalidOperationException (fast open on; mode=Trade");
        Assert.IsFalse(_service.OpenActive);
    }

    [TestMethod]
    public void Ctor_WithThePrefixStripped_TheFinalizerDoesNothing()
    {
        PatchCtor(withPrefix: false);

        _ = new FakeInventoryVm(null, false, null);
        Assert.ThrowsException<InvalidOperationException>(() => new FakeInventoryVm(null, true, null));

        Assert.AreEqual(0, _logger.Lines.Count);
    }

    [TestMethod]
    public void Ctor_TimerOff_LogsNothing_AndTheServiceStaysClosed()
    {
        var off = Substitute.For<IShadowfaxScreensSettingsProvider>();
        off.LogScreenOpenTimes.Returns(false);
        ScreenOpenTiming.Initialize(new ScreenOpenTimer(_clock, _logger, off));
        PatchCtor(withPrefix: true);

        _ = new FakeInventoryVm(null, false, null);

        Assert.AreEqual(0, _logger.Lines.Count);
        Assert.IsFalse(_service.OpenActive);
    }

    [TestMethod]
    public void RowsPhase_InsideAnOpen_IsReportedOnTheOpenLine_AndOutsideOneIsNot()
    {
        var harmony = new Harmony(HarmonyId);
        harmony.Patch(AccessTools.Method(typeof(FakeInventoryVm), nameof(FakeInventoryVm.InitializeInventory)),
            prefix: new HarmonyMethod(typeof(SPInventoryVM_InitializeInventory_ShadowfaxPatch).GetMethod("Prefix")),
            finalizer: new HarmonyMethod(typeof(SPInventoryVM_InitializeInventory_ShadowfaxPatch).GetMethod("Finalizer")));
        var vm = new FakeInventoryVm(null, false, null);

        vm.InitializeInventory();
        Assert.AreEqual(0, _logger.Lines.Count, "A Reset outside an open logs nothing.");

        var window = ShadowfaxInventoryCalls.BeginOpen();
        FakeInventoryVm.Work = () => _clock.Advance(7);
        vm.InitializeInventory();
        ShadowfaxInventoryCalls.EndOpen(window, null, "Trade", 1, 2);

        Assert.AreEqual(1, _logger.Lines.Count);
        StringAssert.Contains(_logger.Lines.Single(), "Inventory open: 7.0 ms");
        StringAssert.Contains(_logger.Lines.Single(), "rows=7.0 ms");
    }
}
