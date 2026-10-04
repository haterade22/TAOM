using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using XmlMergeEngineAdapter = TAOM.Adapters.XmlMergeEngineAdapter;
using TAOM.Core.Logging;
using TAOM.Features.XmlMerge;
using TAOM.Tests.Migration;
using TaleWorlds.ObjectSystem;

namespace TAOM.Tests.Features.XmlMerge;

/// <summary>
/// The XmlMerge gate (plan 042, Step 7a): for every XML type of the live install's module set, the engine's own
/// <c>MBObjectManager.CreateMergedXmlFile</c> and the fast path build the same document, character for character,
/// from the lists the engine builds. Also the timing baseline the fast path is judged on: run it with
/// <c>--logger "console;verbosity=detailed"</c> to see the table. Module order: <c>TAOM_XMLMERGE_MODULES</c>
/// (semicolon-separated) or the run's default. Both tests share one service and one adapter, so whichever runs
/// second sees XSLT cache hits, as the second load of one process does.
///
/// <para>What passes is decided by <see cref="LiveGateRules"/> (pinned without the game by LiveGateRulesTests): every
/// type merges on both sides into the same document, an engine exception is a failure even when the fast path throws
/// the same type, every requested module exists (FastMode may be absent from the default order only), the four heavy
/// types are present, and their fast total is at most half their engine total. The bar is the plan's, measured in this
/// process on this host; absolute times are printed, not asserted.</para>
///
/// <para>Opt-in, as the repo's other slow checks are: a default run reports both tests skipped unless
/// <c>TAOM_RUN_BENCHMARKS=1</c> (they add about 40 s), and a skipped harness checked nothing. /verify-bindings runs
/// them: the class carries <c>BindingVerification</c>, and binding-gate.runsettings sets the variable for the gate's
/// test host, because the gate fails a skip. XmlMergeLiveEquivalenceOptInTests and BindingGateRunSettingsTests pin those
/// two halves.</para>
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
[TestCategory("LiveInstall")]
[TestCategory("BindingVerification")]
public class XmlMergeLiveEquivalenceTests
{
    private const string OptInVariable = "TAOM_RUN_BENCHMARKS";

    private static XmlMergeService? _service;
    private static string? _skipReason;

    public TestContext TestContext { get; set; } = null!;

    [ClassInitialize]
    public static void Init(TestContext _)
    {
        if (Environment.GetEnvironmentVariable(OptInVariable) != "1")
        {
            _skipReason = "Opt-in: set " + OptInVariable + "=1 to run the live merge harness (about 40 s); " +
                          "/verify-bindings sets it through TAOM.Tests/binding-gate.runsettings.";
            return;
        }
        if (!GameAssemblies.EnsureLoaded())
        {
            _skipReason = "Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics);
            return;
        }
        if (!File.Exists(Path.Combine(GameAssemblies.GameDir, "Modules", "TAOM", "SubModule.xml")))
        {
            _skipReason = "No Modules/TAOM/SubModule.xml under " + GameAssemblies.GameDir;
            return;
        }

        var info = Harmony.GetPatchInfo(AccessTools.Method(typeof(MBObjectManager), nameof(MBObjectManager.CreateMergedXmlFile)));
        Assert.IsTrue(info == null || info.Prefixes.Count == 0, "the engine side must run unpatched");

        _service = new XmlMergeService(new XmlMergeEngineAdapter(), new XsltTransformCache(), Substitute.For<IModLogger>());
    }

    [TestMethod]
    public void EveryCampaignType_FastMergeMatchesTheEngine() => CompareEveryType("Campaign");

    [TestMethod]
    public void EveryCustomBattleType_FastMergeMatchesTheEngine() => CompareEveryType("CustomGame");

    private void CompareEveryType(string gameType)
    {
        if (_skipReason != null)
            Assert.Inconclusive(_skipReason);

        var selection = LiveMergeListBuilder.Select();
        var build = LiveMergeListBuilder.Build(GameAssemblies.GameDir, gameType, selection.Modules);
        foreach (var xsd in build.DefaultXsdPaths)
            XmlResource.ReadXsdFileAndExtractInformation(xsd);

        var outcomes = new List<LiveTypeOutcome>();
        var rows = new List<string>();
        TestContext.WriteLine($"game type {gameType}; modules {string.Join(", ", build.Modules)}; " +
                              $"absent {(build.Missing.Count == 0 ? "none" : string.Join(", ", build.Missing))}");
        TestContext.WriteLine("id | entries | files | xslts | engine ms | fast ms | identical");

        foreach (var list in build.Lists)
        {
            var (engineDoc, engineError, engineMs) = Timed(() => MBObjectManager.CreateMergedXmlFile(list.Entries, list.Xslts, false));
            var (fastDoc, fastError, fastMs) = Timed(() => _service!.MergeFast(list.Entries, list.Xslts, false, new XmlMergeCounters()));

            string identical;
            string? difference = null;
            if (engineError != null)
            {
                identical = "engine threw " + engineError.GetType().Name + ", fast " +
                            (fastError == null ? "merged" : "threw " + fastError.GetType().Name);
            }
            else if (fastError != null)
            {
                identical = "fast threw " + fastError.GetType().Name;
            }
            else
            {
                string engineXml = engineDoc!.OuterXml;
                string fastXml = fastDoc!.OuterXml;
                try
                {
                    XmlMergeAssert.SameDocument(engineXml, fastXml, list.Id);
                    identical = "yes, " + engineXml.Length.ToString("N0", CultureInfo.InvariantCulture) + " chars";
                }
                catch (AssertFailedException ex)
                {
                    identical = "NO";
                    difference = ex.Message;
                }
            }

            outcomes.Add(new LiveTypeOutcome(list.Id, engineMs, fastMs, engineError, fastError, difference));
            rows.Add($"{list.Id} | {list.Entries.Count} | {list.Files} | {list.XsltCount} | {engineMs:0} | {fastMs:0} | {identical}");
        }

        foreach (var row in rows)
            TestContext.WriteLine(row);
        var (heavyEngine, heavyFast) = LiveGateRules.HeavyTotals(outcomes);
        TestContext.WriteLine(string.Format(CultureInfo.InvariantCulture,
            "heavy types ({0}): engine {1:0} ms, fast {2:0} ms, fast/engine {3:0.0}% (the bar: at most {4:0}%)",
            string.Join(", ", LiveGateRules.HeavyTypes), heavyEngine, heavyFast,
            heavyEngine > 0 ? 100 * heavyFast / heavyEngine : 0, 100 * LiveGateRules.MaxHeavyFastShare));

        var failures = new List<string>(LiveGateRules.CheckModules(build.Missing, selection.Explicit));
        failures.AddRange(LiveGateRules.CheckTypes(outcomes));
        if (failures.Count > 0)
            Assert.Fail($"{failures.Count} problem(s) for {gameType}:\n" + string.Join("\n", failures));
    }

    private static (XmlDocument? Document, Exception? Error, double Ms) Timed(Func<XmlDocument> merge)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        var watch = Stopwatch.StartNew();
        try
        {
            var document = merge();
            watch.Stop();
            return (document, null, watch.Elapsed.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            watch.Stop();
            return (null, ex, watch.Elapsed.TotalMilliseconds);
        }
    }
}
