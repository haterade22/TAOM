using System;
using System.IO;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Infrastructure;

/// <summary>
/// Pins the framework-shim pairing the TAOM.Dependencies module ships. The vendored
/// <c>System.Memory</c> (assembly 4.0.1.1) binds to <c>System.Runtime.CompilerServices.Unsafe</c>
/// 4.0.4.1 EXACTLY (.NET Framework strict versioning; a module folder cannot carry a binding
/// redirect), and ButterLib's very first <c>Trace.WriteLine</c> runs System.Memory's cctor at
/// startup. Shipping any other Unsafe version kills ButterLib with a TypeInitializationException
/// on every application tick, which presents as MCM's Mod Options hanging at open and NRE-ing at
/// teardown (2026-08-23; the csproj's 6.0.0 pin overwrote the correct vendored file on every
/// deploy). Nothing else in the module consumes Unsafe at runtime: 0Harmony carries no reference
/// to it. This is the exact pair upstream ButterLib distributes.
/// </summary>
[TestClass]
public class DependenciesPairingTests
{
    private static readonly Version RequiredUnsafeVersion = new Version(4, 0, 4, 1);
    private static readonly Version VendoredSystemMemoryVersion = new Version(4, 0, 1, 1);

    private static string RepoRoot => Path.GetFullPath(
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\.."));

    [TestMethod]
    public void VendoredUnsafe_MatchesSystemMemorysExactBind_InEveryShippedVariant()
    {
        var problems = new System.Collections.Generic.List<string>();
        var binRoot = Path.Combine(RepoRoot, @"Dependencies\_Module\bin");

        foreach (var variant in Directory.GetDirectories(binRoot))
        {
            var unsafePath = Path.Combine(variant, "System.Runtime.CompilerServices.Unsafe.dll");
            if (!File.Exists(unsafePath))
                continue;

            var version = AssemblyName.GetAssemblyName(unsafePath).Version;
            if (version != RequiredUnsafeVersion)
            {
                problems.Add(
                    $"{Path.GetFileName(variant)}: Unsafe is {version}, System.Memory binds to "
                    + $"{RequiredUnsafeVersion} exactly - ButterLib dies at startup with this pair");
            }

            var memoryPath = Path.Combine(variant, "System.Memory.dll");
            if (File.Exists(memoryPath))
            {
                var memoryVersion = AssemblyName.GetAssemblyName(memoryPath).Version;
                if (memoryVersion != VendoredSystemMemoryVersion)
                {
                    problems.Add(
                        $"{Path.GetFileName(variant)}: System.Memory is {memoryVersion}, not the "
                        + $"vendored {VendoredSystemMemoryVersion} this pairing pin was derived "
                        + "from - re-derive the required Unsafe version from its metadata before "
                        + "trusting this test");
                }
            }
        }

        Assert.AreEqual(0, problems.Count,
            "TAOM.Dependencies ships a broken framework-shim pair:\n" + string.Join("\n", problems));
    }

    [TestMethod]
    public void BuildOutputUnsafe_WhenPresent_CannotRegressTheDeployedPair()
    {
        // The deploy copies build outputs OVER the vendored set, which is exactly how the 6.0.0
        // regression shipped: the vendored file was right and the csproj pin clobbered it.
        var outputRoot = Path.Combine(RepoRoot, @"Dependencies\bin");
        if (!Directory.Exists(outputRoot))
            Assert.Inconclusive("Dependencies has no build output on this machine; nothing to check.");

        foreach (var dll in Directory.GetFiles(
            outputRoot, "System.Runtime.CompilerServices.Unsafe.dll", SearchOption.AllDirectories))
        {
            var version = AssemblyName.GetAssemblyName(dll).Version;
            Assert.AreEqual(RequiredUnsafeVersion, version,
                $"Build output at {dll} would overwrite the module's Unsafe with {version} on the "
                + "next deploy; the TAOM.Dependencies.csproj PackageReference must stay at the "
                + "package whose net461 assembly is 4.0.4.1 (System.Runtime.CompilerServices.Unsafe 4.5.3)");
        }
    }
}
