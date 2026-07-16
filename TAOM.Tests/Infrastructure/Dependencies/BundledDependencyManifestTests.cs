using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace TAOM.Tests.Infrastructure.Dependencies;

/// <summary>
/// Pins the invariants of TAOM's bundled BUTR dependency stack.
///
/// Why this class exists: the BUTR versions silently drifted across BOTH the v1.4.6 and v1.4.7
/// engine bumps, and the whole 4200-test suite stayed green throughout — because nothing asserted
/// dependency versions, stub versions, the Native engine constraint, or the vendored DLL set.
/// The drift was found by a manual audit (docs/migration/dependency-audit-2026-07-15.md), not by
/// a test. These tests close that gap: they fail the build when the coupled declarations disagree.
///
/// They deliberately assert RELATIONSHIPS between declarations, never hardcoded version literals —
/// a test that restates a version is just one more drift site (the exact defect that rotted
/// dr3-maintenance.md twice). Bumping a dependency correctly keeps these green with no test edit.
/// </summary>
[TestClass]
public class BundledDependencyManifestTests
{
    private static string RepoRoot => Path.GetFullPath(
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\.."));

    private static string MainCsproj => Path.Combine(RepoRoot, @"Main\TAOM.csproj");
    private static string DepsCsproj => Path.Combine(RepoRoot, @"Dependencies\TAOM.Dependencies.csproj");
    private static string MainSubModule => Path.Combine(RepoRoot, @"Main\_Module\SubModule.xml");
    private static string VendoredBin => Path.Combine(RepoRoot, @"Dependencies\_Module\bin\Win64_Shipping_Client");
    private static string LicensesFile => Path.Combine(RepoRoot, @"Dependencies\_Module\THIRD-PARTY-LICENSES.txt");
    private static string PinnedGameVersionFile => Path.Combine(RepoRoot, @".claude\pinned-game-version.txt");

    private static string StubSubModule(string moduleId) =>
        Path.Combine(RepoRoot, "Stubs", moduleId, "_Module", "SubModule.xml");

    /// <summary>The three BUTR packages pinned via NuGet, and the alias-stub module that fronts each.</summary>
    private static readonly (string Package, string StubModuleId)[] NuGetPinnedDeps =
    {
        ("Lib.Harmony", "Bannerlord.Harmony"),
        ("Bannerlord.UIExtenderEx", "Bannerlord.UIExtenderEx"),
        ("Bannerlord.MCM", "Bannerlord.MBOptionScreen"),
    };

    private static string GetPackageVersion(string csprojPath, string packageId)
    {
        var doc = XDocument.Load(csprojPath);
        var refs = doc.Descendants("PackageReference")
            .Where(e => (string?)e.Attribute("Include") == packageId)
            .ToList();

        Assert.AreEqual(1, refs.Count,
            $"Expected exactly one <PackageReference Include=\"{packageId}\"> in {csprojPath}, found {refs.Count}.");

        var version = (string?)refs[0].Attribute("Version");
        Assert.IsFalse(string.IsNullOrWhiteSpace(version),
            $"<PackageReference Include=\"{packageId}\"> in {csprojPath} has no Version attribute.");
        return version!;
    }

    private static string GetStubVersion(string stubModuleId)
    {
        var path = StubSubModule(stubModuleId);
        Assert.IsTrue(File.Exists(path), $"Stub SubModule.xml not found: {path}");

        var value = (string?)XDocument.Load(path).Root?.Element("Version")?.Attribute("value");
        Assert.IsFalse(string.IsNullOrWhiteSpace(value), $"Stub {stubModuleId} has no <Version value=\"...\"/>.");
        return value!;
    }

    private static string FileVersionOf(string dllPath) =>
        FileVersionInfo.GetVersionInfo(dllPath).FileVersion;

    /// <summary>"2.11.0.0" -> "2.11" (the minor the v99 stub scheme keys on).</summary>
    private static string MajorMinor(string version)
    {
        var parts = version.Split('.');
        Assert.IsTrue(parts.Length >= 2, $"Version '{version}' has no major.minor.");
        return $"{parts[0]}.{parts[1]}";
    }

    /// <summary>"2.11.0.0" -> "2.11.0" (the 3-part form used in prose/licences).</summary>
    private static string MajorMinorPatch(string version)
    {
        var parts = version.Split('.');
        Assert.IsTrue(parts.Length >= 3, $"Version '{version}' has no major.minor.patch.");
        return $"{parts[0]}.{parts[1]}.{parts[2]}";
    }

    private static string[] VendoredDlls(string searchPattern)
    {
        Assert.IsTrue(Directory.Exists(VendoredBin), $"Vendored bin folder not found: {VendoredBin}");
        var files = Directory.GetFiles(VendoredBin, searchPattern);
        Assert.AreNotEqual(0, files.Length, $"No vendored DLLs match '{searchPattern}' in {VendoredBin}.");
        return files;
    }

    // ── Compile-pin parity ────────────────────────────────────────────────────────────────────

    [TestMethod]
    public void CompilePins_MainAndDependenciesCsproj_MatchExactly()
    {
        // Arrange / Act / Assert — the rule is stated in TAOM.Dependencies.csproj's own comment:
        // drift between compile-time (Main, IncludeAssets="compile") and runtime (Dependencies)
        // risks MissingMethodException on APIs only present in the newer version.
        foreach (var (package, _) in NuGetPinnedDeps)
        {
            var mainVersion = GetPackageVersion(MainCsproj, package);
            var depsVersion = GetPackageVersion(DepsCsproj, package);

            Assert.AreEqual(depsVersion, mainVersion,
                $"'{package}' pin drift: Main/TAOM.csproj={mainVersion} but " +
                $"Dependencies/TAOM.Dependencies.csproj={depsVersion}. These MUST match.");
        }
    }

    // ── The v99 stub rule (docs/migration/dr3-maintenance.md "Stub modules") ──────────────────

    [TestMethod]
    public void StubVersions_NuGetPinnedDeps_TrackPackageMinorAs99()
    {
        // The stub advertises vX.Y.99.0 so third-party mods' minimum-version checks pass under BLSE.
        // X.Y must track the package's CURRENT minor; a stale stub silently fails those checks.
        foreach (var (package, stubModuleId) in NuGetPinnedDeps)
        {
            var packageVersion = GetPackageVersion(DepsCsproj, package);
            var expected = $"v{MajorMinor(packageVersion)}.99.0";
            var actual = GetStubVersion(stubModuleId);

            Assert.AreEqual(expected, actual,
                $"Stub '{stubModuleId}' declares {actual} but package '{package}' is pinned at " +
                $"{packageVersion}, so the stub must be {expected}. See dr3-maintenance.md \"Stub modules\".");
        }
    }

    [TestMethod]
    public void StubVersion_ButterLib_TracksVendoredDllMinorAs99()
    {
        // ButterLib has no NuGet pin — it is vendored-only, so the shipped DLL IS the version source.
        var dllVersion = FileVersionOf(Path.Combine(VendoredBin, "Bannerlord.ButterLib.dll"));
        var expected = $"v{MajorMinor(dllVersion)}.99.0";
        var actual = GetStubVersion("Bannerlord.ButterLib");

        Assert.AreEqual(expected, actual,
            $"Stub 'Bannerlord.ButterLib' declares {actual} but the vendored " +
            $"Bannerlord.ButterLib.dll is {dllVersion}, so the stub must be {expected}.");
    }

    // ── Vendored DLL set homogeneity (split-brain guard) ──────────────────────────────────────

    [TestMethod]
    public void VendoredButterLibDlls_AllShareOneVersion()
    {
        // A half-finished refresh (core updated, some Implementation.1.4.x left behind) would load a
        // mixed release set at runtime. Every ButterLib DLL ships from one upstream release.
        var byVersion = VendoredDlls("Bannerlord.ButterLib*.dll")
            .GroupBy(FileVersionOf)
            .ToList();

        Assert.AreEqual(1, byVersion.Count,
            "Vendored ButterLib DLLs are not all the same version (split-brain refresh): " +
            string.Join(" | ", byVersion.Select(g =>
                $"{g.Key} => {string.Join(", ", g.Select(Path.GetFileName))}")));
    }

    [TestMethod]
    public void VendoredMbOptionScreenDlls_AllShareOneVersion()
    {
        // MCM.UI.Adapter ships from the same MBOptionScreen release as the .v1.4.x impls.
        // (Bannerlord.ModuleLoader.*.dll is versioned independently and is excluded by the prefix.)
        var files = VendoredDlls("Bannerlord.MBOptionScreen*.dll")
            .Concat(VendoredDlls("MCM.UI.Adapter.MCMv5.dll"));

        var byVersion = files.GroupBy(FileVersionOf).ToList();

        Assert.AreEqual(1, byVersion.Count,
            "Vendored MBOptionScreen/MCM.UI.Adapter DLLs are not all the same version: " +
            string.Join(" | ", byVersion.Select(g =>
                $"{g.Key} => {string.Join(", ", g.Select(Path.GetFileName))}")));
    }

    [TestMethod]
    public void VendoredMcmUiAdapter_MatchesMbOptionScreenImplVersion()
    {
        // The MCM API (MCMv5.dll, NuGet) and the MCM UI (MBOptionScreen, vendored) are released in
        // lockstep; the adapter bridges them. A mismatch here means the API and UI disagree.
        var adapter = FileVersionOf(Path.Combine(VendoredBin, "MCM.UI.Adapter.MCMv5.dll"));
        var mcmPin = GetPackageVersion(DepsCsproj, "Bannerlord.MCM");

        Assert.AreEqual(MajorMinorPatch(adapter), MajorMinorPatch($"{mcmPin}.0"),
            $"Vendored MCM.UI.Adapter.MCMv5.dll is {adapter} but the Bannerlord.MCM NuGet pin is " +
            $"{mcmPin}. The vendored MCM UI and the MCM API package must ship the same release.");
    }

    // ── Engine target coupling (the drift that survived two engine bumps) ─────────────────────

    [TestMethod]
    public void NativeConstraint_MatchesPinnedGameVersion()
    {
        // Main/_Module/SubModule.xml sat at v1.4.5.* through the 1.4.6 AND 1.4.7 bumps because
        // nothing coupled it to the declared engine target. This is that coupling.
        if (!File.Exists(PinnedGameVersionFile))
        {
            Assert.Inconclusive($"Engine pin file not present in this checkout: {PinnedGameVersionFile}");
            return;
        }

        var pinned = File.ReadAllText(PinnedGameVersionFile).Trim();
        var nativeVersion = (string?)XDocument.Load(MainSubModule)
            .Root?.Element("DependedModuleMetadatas")
            ?.Elements("DependedModuleMetadata")
            .SingleOrDefault(e => (string?)e.Attribute("id") == "Native")
            ?.Attribute("version");

        Assert.IsFalse(string.IsNullOrWhiteSpace(nativeVersion),
            "Main/_Module/SubModule.xml has no <DependedModuleMetadata id=\"Native\" version=\"...\"/>.");

        Assert.AreEqual($"{pinned}.*", nativeVersion,
            $"Native engine constraint is {nativeVersion} but the pinned game version is {pinned}. " +
            "Bump the constraint per docs/migration/dr3-maintenance.md Scenario A, or run /engine-bump.");
    }

    // ── Redistribution attribution ────────────────────────────────────────────────────────────

    [TestMethod]
    public void ThirdPartyLicenses_NameTheActuallyShippedVersions()
    {
        // THIRD-PARTY-LICENSES.txt makes a redistribution claim about binaries that get physically
        // swapped on every refresh. Naming a version we no longer ship is a false attribution.
        Assert.IsTrue(File.Exists(LicensesFile), $"Licenses file not found: {LicensesFile}");
        var text = File.ReadAllText(LicensesFile);

        var butterLib = MajorMinorPatch(FileVersionOf(Path.Combine(VendoredBin, "Bannerlord.ButterLib.dll")));
        var mbOptionScreen = MajorMinorPatch(FileVersionOf(Path.Combine(VendoredBin, "MCM.UI.Adapter.MCMv5.dll")));
        var uiExtenderEx = GetPackageVersion(DepsCsproj, "Bannerlord.UIExtenderEx");
        var harmony = GetPackageVersion(DepsCsproj, "Lib.Harmony");
        var mcm = GetPackageVersion(DepsCsproj, "Bannerlord.MCM");

        foreach (var (label, expected) in new[]
                 {
                     ("ButterLib", butterLib),
                     ("Bannerlord.MBOptionScreen", mbOptionScreen),
                     ("Bannerlord.UIExtenderEx", uiExtenderEx),
                     ("Lib.Harmony", harmony),
                     ("Bannerlord.MCM", mcm),
                 })
        {
            StringAssert.Contains(text, $"{label} {expected}",
                $"THIRD-PARTY-LICENSES.txt does not attribute '{label} {expected}' — the version we " +
                "actually ship. Update the notice when refreshing the vendored/pinned dependency.");
        }
    }
}
