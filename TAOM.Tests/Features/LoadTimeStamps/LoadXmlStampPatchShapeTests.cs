using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LoadTimeStamps;
using TAOM.Features.LoadTimeStamps.Hooks;

namespace TAOM.Tests.Features.LoadTimeStamps;

/// <summary>
/// The LoadXML stamp's prefix and void finalizer, applied with real Harmony around a stand-in that
/// has LoadXML's parameter names: the target runs once, and an exception leaves with its type,
/// message and throw site intact (a void finalizer keeps Harmony's rethrow). RequiresGame because
/// the patch class's attributes name an engine type.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class LoadXmlStampPatchShapeTests
{
    private const string HarmonyId = "taom.tests.loadxmlstamp";
    private static readonly List<string> Calls = new();

    private FakeStampClock _clock = null!;
    private RecordingLogger _logger = null!;

    public static void Target(string id, bool isDevelopment, string gameType, bool skipXmlFilterForEditor)
    {
        Calls.Add("target:" + id);
        if (id == "Throw")
            Thrower();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Thrower() => throw new InvalidOperationException("merge failed");

    [TestInitialize]
    public void Setup()
    {
        Calls.Clear();
        _clock = new FakeStampClock();
        _logger = new RecordingLogger();
        LoadTimeStampsHooks.InitializeLoadXml(new LoadXmlStampService(_clock, _logger));
        new Harmony(HarmonyId).Patch(
            AccessTools.Method(typeof(LoadXmlStampPatchShapeTests), nameof(Target)),
            prefix: new HarmonyMethod(AccessTools.Method(typeof(MBObjectManager_LoadXML_StampPatch), "Prefix")),
            finalizer: new HarmonyMethod(AccessTools.Method(typeof(MBObjectManager_LoadXML_StampPatch), "Finalizer")));
    }

    [TestCleanup]
    public void Cleanup()
    {
        new Harmony(HarmonyId).UnpatchAll(HarmonyId);
        LoadTimeStampsHooks.InitializeLoadXml(null);
    }

    [TestMethod]
    public void Patched_TargetSucceeds_RunsOnceAndLogsResultOk()
    {
        Target("Items", false, "Campaign", false);

        CollectionAssert.AreEqual(new[] { "target:Items" }, Calls);
        CollectionAssert.AreEqual(new[]
        {
            "INFO [LoadXml] id=Items files=0 ms=0.00 xslt=0 merge_ms=none objects_ms=none result=ok",
        }, _logger.Lines);
    }

    [TestMethod]
    public void Patched_TargetThrows_RethrowsTheSameExceptionWithItsThrowSite()
    {
        var thrown = Assert.ThrowsException<InvalidOperationException>(() => Target("Throw", false, "Campaign", false));

        Assert.AreEqual("merge failed", thrown.Message);
        CollectionAssert.AreEqual(new[] { "target:Throw" }, Calls);
        StringAssert.Contains(thrown.StackTrace, nameof(Thrower));
        StringAssert.EndsWith(_logger.Lines.Single(), "result=InvalidOperationException");
    }

    [TestMethod]
    public void Patched_NoServiceWired_StillRunsTheTargetAndPropagates()
    {
        LoadTimeStampsHooks.InitializeLoadXml(null);

        Target("Items", false, "Campaign", false);
        var thrown = Assert.ThrowsException<InvalidOperationException>(() => Target("Throw", false, "Campaign", false));

        Assert.AreEqual("merge failed", thrown.Message);
        CollectionAssert.AreEqual(new[] { "target:Items", "target:Throw" }, Calls);
        Assert.AreEqual(0, _logger.Lines.Count);
    }
}
