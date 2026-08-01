using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TAOM.Dependencies.Foundation;

// Fully-qualified alias, deliberately. This file's namespace nests under `TAOM`, so a bare
// `SubModule` binds to `TAOM.SubModule` (Main) before any `using TAOM.Dependencies` is considered —
// which fails with a confusing "does not contain a definition for RedirectedSimpleNames".
using DepsSubModule = TAOM.Dependencies.SubModule;

namespace TAOM.Tests.Infrastructure.Dependencies;

/// <summary>
/// Pins two co-op interop invariants found on 2026-08-01, both of which are guarded by DELETIONS or
/// single-line additions that leave nothing behind to explain themselves.
///
/// 1. <c>SubModule.RedirectBundledDependencies</c> matches on SIMPLE NAME ONLY and returns the first
///    already-loaded assembly with that name, discarding the requested version. Safe only while
///    TAOM's bundled copy is the newest in the process — BannerlordCoop ships five of those names at
///    HIGHER versions, so the redirect handed Coop's callers our older assembly, producing a
///    MissingMethodException at an arbitrary later point attributed to neither mod.
///
/// 2. <c>CoopPresence.CompiledModuleDefaults</c> and the shipped <c>coop-modules.txt</c> are two
///    copies of the same list. <c>BundledDependencyManifestTests</c> already pins the txt against
///    both SubModule.xml manifests, but nothing pinned the compiled array — so adding an id to the
///    array alone produced a DETECTED co-op module with NO load-order pin and no failing test.
///
/// The list's own history is the argument for these guards: it was expanded 4 → 22 as speculative
/// "BetaDeps parity", not in response to any observed bug. The next such sweep would re-add them.
/// Evidence: docs/research/bannerlordcoop-internals.md
/// </summary>
[TestClass]
public class AssemblyRedirectListTests
{
    [TestMethod]
    public void RedirectedSimpleNames_DoesNotContainAnyDeliberatelyExcludedName()
    {
        var offenders = DepsSubModule.RedirectedSimpleNames
            .Intersect(DepsSubModule.DeliberatelyNotRedirected, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Assert.AreEqual(0, offenders.Count,
            $"'{string.Join("', '", offenders)}' are shipped by BannerlordCoop at a HIGHER version " +
            "than TAOM's copy, and the redirect is version-blind — re-adding one down-shims Coop's " +
            "callers to our older assembly. See the block comment in Dependencies/SubModule.cs.");
    }

    [TestMethod]
    public void DeliberatelyNotRedirected_ListsExactlyTheFiveMeasuredCollisions()
    {
        // Measured 2026-08-01 with [Reflection.AssemblyName]::GetAssemblyName over both bin folders:
        //   Serilog                                 ours 2.0.0.0  coop 4.2.0.0
        //   System.Runtime.CompilerServices.Unsafe  ours 4.0.4.1  coop 6.0.1.0
        //   System.Memory                           ours 4.0.1.1  coop 4.0.2.0
        //   System.Buffers                          ours 4.0.3.0  coop 4.0.4.0
        //   System.Numerics.Vectors                 ours 4.1.4.0  coop 4.1.5.0
        // Names Coop ships that TAOM does NOT (Mono.Cecil, MonoMod.*, Newtonsoft.Json,
        // System.ValueTuple, System.Threading.Tasks.Extensions, 0Harmony) stay redirected on
        // purpose: with no TAOM copy loaded the handler resolves to Coop's own assembly, which is
        // the correct outcome.
        CollectionAssert.AreEquivalent(
            new[]
            {
                "Serilog",
                "System.Buffers",
                "System.Memory",
                "System.Numerics.Vectors",
                "System.Runtime.CompilerServices.Unsafe",
            },
            DepsSubModule.DeliberatelyNotRedirected);
    }

    [TestMethod]
    public void RedirectedSimpleNames_StillProtectsTheLoadBearingButrStack()
    {
        // The original entries are why the shim exists at all: a consumer mod bundling its own
        // MCMv5 / UIExtenderEx / ButterLib copy must be redirected to ours. The 2026-08-01 deletion
        // must not have collaterally removed these.
        foreach (var required in new[]
                 {
                     "0Harmony",
                     "MCMv5",
                     "Bannerlord.UIExtenderEx",
                     "Bannerlord.ButterLib",
                     "Bannerlord.Harmony",
                 })
        {
            CollectionAssert.Contains(DepsSubModule.RedirectedSimpleNames, required,
                $"'{required}' is load-bearing BUTR-stack protection and must stay redirected.");
        }
    }

    [TestMethod]
    public void RedirectedSimpleNames_HasNoDuplicates()
    {
        var dupes = DepsSubModule.RedirectedSimpleNames
            .GroupBy(n => n, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        Assert.AreEqual(0, dupes.Count, $"duplicate redirect entries: '{string.Join("', '", dupes)}'");
    }

    [TestMethod]
    public void CompiledModuleDefaults_MatchesTheShippedCoopModulesFile()
    {
        // The drift this closes: CoopPresence.CompiledModuleDefaults is the fallback used when
        // coop-modules.txt is missing or unreadable, and BundledDependencyManifestTests only pins
        // the FILE against the manifests. An id added to the array alone would be detected as a
        // co-op module while having no <ModulesToLoadAfterThis> pin in either SubModule.xml.
        var file = Path.Combine(RepoRoot, @"Dependencies\_Module\coop-modules.txt");
        Assert.IsTrue(File.Exists(file), $"coop-modules.txt not found: {file}");

        CollectionAssert.AreEquivalent(
            ReadCoopModulesSection(file),
            CoopPresence.CompiledModuleDefaults.ToList(),
            "CoopPresence.CompiledModuleDefaults and coop-modules.txt [modules] have drifted. Both " +
            "must list the same ids, or the compiled fallback detects a co-op module that has no " +
            "load-order pin (the manifest test only sees the file).");
    }

    private static string RepoRoot
    {
        get
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "CLAUDE.md")))
                dir = dir.Parent;
            Assert.IsNotNull(dir, "could not locate repo root (no CLAUDE.md found walking upward)");
            return dir!.FullName;
        }
    }

    /// <summary>Minimal reader for the `[modules]` section — mirrors CoopModuleList's format.</summary>
    private static List<string> ReadCoopModulesSection(string path)
    {
        var ids = new List<string>();
        var inModules = true;   // entries before any header are module ids
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#")) continue;
            if (line.StartsWith("[") && line.EndsWith("]"))
            {
                inModules = string.Equals(line, "[modules]", StringComparison.OrdinalIgnoreCase);
                continue;
            }
            if (inModules) ids.Add(line);
        }
        return ids;
    }
}
