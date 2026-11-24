using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Client;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Infrastructure;

/// <summary>
/// The server JIT ratchet (ADR-013, B7). The JIT fails a method whose IL names a type from an assembly that does
/// not resolve before its first line runs, so a core method that names a client-only type is a dedicated-server
/// hazard even when the type loads. <c>ServerJitBaseline.txt</c> lists the user-level methods of TAOM.dll that
/// still do; the list may only shrink. A new offender under a new key fails
/// <see cref="ServerReachableMethods_ReferenceNoClientAssembly"/> (one under a key already listed, such as another
/// overload or constructor, does not: keys merge), a fixed one fails <see cref="Baseline_HasNoStaleEntriesAndIsSortedUnique"/> until it is pruned. TAOM.Client is
/// out of scope (it never loads headless) but is scanned as a positive control, and TAOM.Dependencies has its own
/// no-baseline gate (<c>DependenciesClientIlTests</c>). Every member of a class marked <c>ClientOnlyAttribute</c>
/// (B6a: Harmony patch classes a server never indexes) is exempt, its helpers included, so their callers' server
/// checks are what keep those helpers off a server (<c>SubModuleServerGuardTests</c>).
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class ServerJitGateTests
{
    private const string WriteEnvVar = "TAOM_WRITE_SERVER_JIT_BASELINE";

    /// <summary>
    /// The baseline only shrinks, and this must equal its line count: a prune fails
    /// <see cref="Baseline_HasNoStaleEntriesAndIsSortedUnique"/> until this is lowered to match, and a net addition
    /// fails unless this is raised. A line added in the same change as a prune keeps the count, so only review sees it.
    /// </summary>
    private const int MaxBaselineEntries = 264;

    private static string BaselinePath => RepoPaths.RepoPath("TAOM.Tests", "Infrastructure", "ServerJitBaseline.txt");

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => GameAssemblies.EnsureLoaded();

    private static void RequireGame()
    {
        if (!GameAssemblies.EnsureLoaded())
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
    }

    private static bool IsClient(Assembly a) => ClientAssemblies.IsClientOnly(a.GetName().Name ?? "");

    /// <summary>Every test that scans core goes through here, so an unreadable body fails all of them.</summary>
    private static SortedSet<string> LiveCore(out int scanned, out Dictionary<string, string> first)
    {
        var live = ServerJitScan.Offenders(typeof(SubModule).Assembly.GetTypes(), IsClient, out var unreadable, out scanned, out first);
        Assert.AreEqual(0, unreadable.Count, "methods whose IL could not be read: " + string.Join("; ", unreadable));
        return live;
    }

    /// <summary>The MethodDef rows of the manifest module: ResolveMethod fails past the last row of the table.</summary>
    private static int MethodDefCount(Module module)
    {
        var count = 0;
        while (true)
        {
            try
            {
                module.ResolveMethod(0x06000001 + count);
                count++;
            }
            catch (ArgumentOutOfRangeException)
            {
                return count;
            }
        }
    }

    private static List<string> LoadBaselineLines()
    {
        Assert.IsTrue(File.Exists(BaselinePath), "Baseline not found at " + BaselinePath);
        return File.ReadAllLines(BaselinePath).Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.StartsWith("#", StringComparison.Ordinal)).ToList();
    }

    [TestMethod]
    public void ServerReachableMethods_ReferenceNoClientAssembly()
    {
        RequireGame();
        var live = LiveCore(out _, out var first);
        var (added, _) = ServerJitScan.Compare(live, LoadBaselineLines());

        Assert.AreEqual(0, added.Count,
            "These core methods name a client-only type, so the JIT fails them whenever that assembly is unresolvable "
            + "(always at TAOM's boot on the dedicated server). Move the code to TAOM.Client behind a core interface with a "
            + "null (ADR-013), or, for a Harmony patch class whose target or patch signature names a client assembly, mark "
            + "the class [ClientOnly] (gate 4). Never add a line to ServerJitBaseline.txt:" + Environment.NewLine
            + string.Join(Environment.NewLine, added.Select(k => k + " -> " + first[k])));
    }

    [TestMethod]
    public void Baseline_HasNoStaleEntriesAndIsSortedUnique()
    {
        RequireGame();
        var lines = LoadBaselineLines();
        var live = LiveCore(out _, out _);
        var (_, stale) = ServerJitScan.Compare(live, lines);

        Assert.AreEqual(0, stale.Count,
            "These baseline lines no longer name a client type (fixed, moved or renamed). Prune them: set "
            + WriteEnvVar + "=1 and run Baseline_Regenerate_WhenAsked:" + Environment.NewLine
            + string.Join(Environment.NewLine, stale));
        CollectionAssert.AreEqual(lines.OrderBy(l => l, StringComparer.Ordinal).Distinct().ToList(), lines,
            "ServerJitBaseline.txt must be sorted (ordinal) with no duplicates.");
        Assert.IsFalse(lines.Any(l => l.Contains('<')), "a compiler-generated name is in the baseline: fix the key folding");
        // Equal, not at most: a prune that leaves the cap high would let the pruned lines come back unnoticed.
        Assert.AreEqual(MaxBaselineEntries, lines.Count,
            "The baseline has " + lines.Count + " lines and MaxBaselineEntries is " + MaxBaselineEntries + ". It only "
            + "shrinks: never add a line. When you prune, lower MaxBaselineEntries to the new count.");
    }

    [TestMethod]
    public void Scan_ReadsEveryCoreMethodDef_AndFailsClosed()
    {
        RequireGame();
        LiveCore(out var scanned, out _);

        var module = typeof(SubModule).Assembly.ManifestModule;
        Assert.AreEqual(MethodDefCount(module), scanned,
            "the walk must read exactly the MethodDef rows of TAOM.dll; a type or method it skips is a blind spot");
    }

    [TestMethod]
    public void TaomClient_IsOutOfScope_YetTheDetectorFiresOnIt()
    {
        RequireGame();
        CollectionAssert.AreEqual(new[] { typeof(IoC).Assembly, typeof(ClientSubModule).Assembly }, TaomAssemblies.All,
            "a new TAOM assembly needs a scope decision here");

        var live = ServerJitScan.Offenders(typeof(ClientSubModule).Assembly.GetTypes(), IsClient, out var unreadable, out _, out _);

        CollectionAssert.AreEqual(new List<string>(), unreadable);
        foreach (var key in new[]
                 {
                     "TAOM.Features.SupplyLines.SupplyRouteVisualService::Update",
                     "TAOM.Features.FieldCamp.CampVisualService::Show",
                     "TAOM.Features.Refuge.Visuals.RefugeVisualService::Show",
                 })
            Assert.IsTrue(live.Contains(key), "the detector did not find " + key + " in TAOM.Client");
    }

    [TestMethod]
    public void Baseline_Regenerate_WhenAsked()
    {
        if (Environment.GetEnvironmentVariable(WriteEnvVar) != "1")
            Assert.Inconclusive("Set " + WriteEnvVar + "=1 to prune ServerJitBaseline.txt: it keeps only the lines still live "
                + "(it can only remove lines), then lower MaxBaselineEntries.");
        RequireGame();

        var live = LiveCore(out var scanned, out _);

        if (!File.Exists(BaselinePath))
            Assert.Fail("ServerJitBaseline.txt is missing: restore it from git. The writer only prunes; it never creates the list.");
        var keys = live.Intersect(LoadBaselineLines(), StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal).ToList();
        Assert.IsFalse(keys.Any(k => k.Contains('<')), "a compiler-generated name reached the baseline: fix the key folding");

        var header = new List<string>
        {
            "# Core (TAOM.dll) methods whose IL names a client-only assembly: the JIT fails them whenever that assembly is unresolvable",
            "# (always at TAOM's boot on the dedicated server).",
            "# One line per user-level method: <owner type FullName>::<method name>. Closures, local functions and iterator or",
            "# async state machines fold into the method that declares them; overloads merge (ServerJitScan.KeyOf).",
            "# The list may only shrink. Never add a line: move the code to TAOM.Client behind a core seam (ADR-013), or mark a",
            "# Harmony patch class whose target or signature names a client assembly [ClientOnly] (gate 4). To prune: set",
            "# " + WriteEnvVar + "=1, run ServerJitGateTests.Baseline_Regenerate_WhenAsked, then lower MaxBaselineEntries.",
            "# Entries: " + keys.Count + " (methods scanned: " + scanned + ").",
        };
        File.WriteAllLines(BaselinePath, header.Concat(keys));
    }
}
