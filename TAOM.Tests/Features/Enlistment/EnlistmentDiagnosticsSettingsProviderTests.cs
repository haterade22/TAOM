using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Enlistment;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// Fail-direction pin for the enlistment diagnostics toggle. The toggle is ON again from 2026-08-28
/// while the #520 standing and rank loop is verified in play, so a missing MCM instance must resolve
/// to ON too — otherwise a player whose MCM failed to load silently loses the trace we are asking
/// them for, and MCM-absent behaviour would differ from MCM-present-at-default behaviour.
///
/// <see cref="EnlistmentDiagnosticsSettingsProvider.ResolveEnabled"/> is a pure seam that never
/// touches the MCM static, so the fail direction stays pinned regardless of whether
/// <c>TaomSettings.Instance</c> is reachable from the MSTest host.
///
/// This ON posture is TEMPORARY and reverts when #520's smoke has run. Whichever way it goes, BOTH
/// the compiled default and the <c>??</c> fallback flip together — <see
/// cref="CompiledDefault_AndProviderFallback_Agree"/> is what makes changing only one of them fail,
/// and it is symmetric, so it cannot tell you which posture is current. These two tests can.
/// </summary>
[TestClass]
public class EnlistmentDiagnosticsSettingsProviderTests
{
    [TestMethod]
    public void ResolveEnabled_NullSetting_ReturnsTrue()
    {
        // The whole guard. Reddened by changing the provider's `?? true` back to `?? false`.
        //
        // Flipped OFF 2026-08-09, and back ON 2026-08-28 with the compiled default, for the #520
        // smoke. The cost of the loud posture for an MCM-less player is a bigger log file; the cost
        // of the quiet one is that the session we are asking them to reproduce tells us nothing.
        // While a defect is under investigation the second is the worse trade. Flip both back after.
        Assert.IsTrue(EnlistmentDiagnosticsSettingsProvider.ResolveEnabled(null));
    }

    [TestMethod]
    public void ResolveEnabled_False_ReturnsFalse()
    {
        // Kills a vacuous hard-coded implementation: the seam must pass the player's choice
        // through, not just always answer with the default. With the default back ON this is again
        // the test carrying that weight, because it is the one that disagrees with the default;
        // ResolveEnabled_True_ReturnsTrue now agrees with it and cannot tell a constant from a
        // passthrough. Exactly one of this pair is load-bearing at any time, and which one swaps
        // every time the posture does.
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

        var defaultIsOn = settingsSource.Contains("EnableEnlistmentDiagnostics { get; set; } = true;");
        var fallbackIsOn = providerSource.Contains("ResolveEnabled(bool? raw) => raw ?? true;");

        Assert.IsTrue(
            settingsSource.Contains("EnableEnlistmentDiagnostics"),
            "Sanity floor: TaomSettings.cs must actually contain the property — a path or rename " +
            "slip must fail loudly here rather than let both Contains() checks pass vacuously.");
        Assert.AreEqual(
            defaultIsOn, fallbackIsOn,
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
