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
/// The CreateMergedXmlFile stamp's void finalizer, applied with real Harmony around a stand-in with
/// the merge's parameter names, beside another owner's prefix that skips the original and supplies
/// its own result (plan 042's fast path does that): the finalizer still marks the merge with the
/// request's counts, and the replacement's result is returned untouched. RequiresGame because the
/// patch class's attributes name an engine type.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class MergeStampPatchShapeTests
{
    private const string StampId = "taom.tests.mergestamp";
    private const string OtherId = "taom.tests.mergestamp.other";
    private static readonly object Replacement = new();
    private static int _targetRuns;

    private FakeStampClock _clock = null!;
    private RecordingLogger _logger = null!;
    private LoadXmlStampService _service = null!;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static object Target(List<Tuple<string, string>> toBeMerged, List<string> xsltList, bool skipValidation)
    {
        _targetRuns++;
        return new object();
    }

    // Another owner's replacement: skips the original and supplies the result.
    public static bool SkipPrefix(ref object __result)
    {
        __result = Replacement;
        return false;
    }

    [TestInitialize]
    public void Setup()
    {
        _targetRuns = 0;
        _clock = new FakeStampClock();
        _logger = new RecordingLogger();
        _service = new LoadXmlStampService(_clock, _logger);
        LoadTimeStampsHooks.InitializeLoadXml(_service);
        var target = AccessTools.Method(typeof(MergeStampPatchShapeTests), nameof(Target));
        new Harmony(OtherId).Patch(target, prefix: new HarmonyMethod(AccessTools.Method(typeof(MergeStampPatchShapeTests), nameof(SkipPrefix))));
        new Harmony(StampId).Patch(target,
            finalizer: new HarmonyMethod(AccessTools.Method(typeof(MBObjectManager_CreateMergedXmlFile_StampPatch), "Finalizer")));
    }

    [TestCleanup]
    public void Cleanup()
    {
        new Harmony(StampId).UnpatchAll(StampId);
        new Harmony(OtherId).UnpatchAll(OtherId);
        LoadTimeStampsHooks.InitializeLoadXml(null);
    }

    [TestMethod]
    public void AnotherPrefixSkipsTheOriginal_TheFinalizerStillMarksTheMerge_AndTheResultIsTheReplacement()
    {
        var call = _service.Begin("Items", "Campaign");
        _clock.Advance(40);

        var result = Target(
            new List<Tuple<string, string>> { Tuple.Create("a.xml", "x"), Tuple.Create("b.xml", "x") },
            new List<string> { "", "t.xslt" },
            false);
        _clock.Advance(5);
        _service.End(call, null);

        Assert.AreEqual(0, _targetRuns, "the other owner's prefix did not skip the original");
        Assert.AreSame(Replacement, result);
        Assert.AreEqual("INFO [LoadXml] id=Items files=2 ms=45.00 xslt=1 merge_ms=40.00 objects_ms=5.00 result=ok", _logger.Lines.Single());
    }
}
