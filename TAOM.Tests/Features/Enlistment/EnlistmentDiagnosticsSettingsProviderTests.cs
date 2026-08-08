using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Enlistment;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// Fail-direction pin for the enlistment diagnostics toggle. The toggle ships ON while the enlisted
/// service loop is under active diagnosis, so a missing MCM instance must resolve to ON too —
/// otherwise a player whose MCM failed to load silently loses the trace we are asking them for, and
/// MCM-absent behaviour would differ from MCM-present-at-default behaviour.
///
/// <see cref="EnlistmentDiagnosticsSettingsProvider.ResolveEnabled"/> is a pure seam that never
/// touches the MCM static, so the fail direction stays pinned regardless of whether
/// <c>TaomSettings.Instance</c> is reachable from the MSTest host.
///
/// When the toggle is later flipped to default OFF, BOTH the compiled default and the <c>??</c>
/// fallback flip together — <see cref="CompiledDefault_AndProviderFallback_Agree"/> is what makes
/// changing only one of them fail.
/// </summary>
[TestClass]
public class EnlistmentDiagnosticsSettingsProviderTests
{
    [TestMethod]
    public void ResolveEnabled_NullSetting_ReturnsTrue()
    {
        // The whole guard. Reddened by changing the provider's `?? true` to `?? false`.
        Assert.IsTrue(EnlistmentDiagnosticsSettingsProvider.ResolveEnabled(null));
    }

    [TestMethod]
    public void ResolveEnabled_False_ReturnsFalse()
    {
        // Kills a vacuous hard-coded-true implementation: the seam must actually pass the player's
        // choice through, not just always answer "on". This is the test that proves the MCM
        // checkbox does something.
        Assert.IsFalse(EnlistmentDiagnosticsSettingsProvider.ResolveEnabled(false));
    }

    [TestMethod]
    public void ResolveEnabled_True_ReturnsTrue()
    {
        Assert.IsTrue(EnlistmentDiagnosticsSettingsProvider.ResolveEnabled(true));
    }

    [TestMethod]
    public void IsEnabled_NoMcmInstance_DefaultsTrue()
    {
        // TaomSettings.Instance is null in the test host (MCM v5 isn't loaded), so the provider
        // falls back to the compiled default. Same shape as NameplateFadeSettingsProviderTests.
        var sut = new EnlistmentDiagnosticsSettingsProvider();

        Assert.IsTrue(sut.IsEnabled);
    }

    [TestMethod]
    public void CompiledDefault_AndProviderFallback_Agree()
    {
        // The MCM default and the null-instance fallback are two independent literals that encode
        // the SAME decision. If they drift, a player with MCM loaded and a player without it get
        // different behaviour at the shipped default — the kind of split that is invisible in
        // testing because the test host never has MCM. Source-scanned rather than reflected because
        // instantiating TaomSettings would drag in the MCM assembly this host does not have.
        var settingsSource = File.ReadAllText(RepoPath("Main", "Features", "TaomSettings.cs"));
        var providerSource = File.ReadAllText(
            RepoPath("Main", "Features", "Enlistment", "EnlistmentDiagnosticsSettingsProvider.cs"));

        var defaultIsTrue = settingsSource.Contains("EnableEnlistmentDiagnostics { get; set; } = true;");
        var fallbackIsTrue = providerSource.Contains("ResolveEnabled(bool? raw) => raw ?? true;");

        Assert.IsTrue(
            settingsSource.Contains("EnableEnlistmentDiagnostics"),
            "Sanity floor: TaomSettings.cs must actually contain the property — a path or rename " +
            "slip must fail loudly here rather than let both Contains() checks pass vacuously.");
        Assert.AreEqual(
            defaultIsTrue, fallbackIsTrue,
            "TaomSettings.EnableEnlistmentDiagnostics' compiled default and " +
            "EnlistmentDiagnosticsSettingsProvider.ResolveEnabled's `??` fallback must encode the " +
            "same posture. Flip both or neither.");
    }

    // Locates a repo file from THIS source file's compile-time path, so the test does not depend on
    // the test assembly's output layout (bin/Debug/net472 depth) staying what it is today.
    private static string RepoPath(params string[] parts)
    {
        // THREE levels: <repo>/TAOM.Tests/Features/Enlistment -> Features -> TAOM.Tests -> <repo>.
        var repoRoot = Path.GetFullPath(Path.Combine(ThisFile(), "..", "..", ".."));
        return Path.Combine(new[] { repoRoot }.Concat(parts).ToArray());
    }

    private static string ThisFile([CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;
}
