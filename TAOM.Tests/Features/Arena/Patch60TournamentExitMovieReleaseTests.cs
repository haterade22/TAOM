using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Arena.Hooks;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.Arena;

/// <summary>
/// Drift-guard for the two private-field bindings <see cref="Patch60_TournamentExitMovieRelease"/>
/// resolves at type-load against the installed engine: <c>MissionGauntletTournamentView._gauntletLayer</c>
/// and <c>._gauntletMovie</c> (issue #331 — the engine's tournament view nulls both without
/// ReleaseMovie/RemoveLayer, deferring the Tournament-movie teardown into ScreenBase.HandleFinalize
/// where an in-flight prize tableau render hangs the exit ~108s).
/// <para>
/// <c>HarmonyPatchBindingTests</c> already covers the patch's <c>[HarmonyPatch]</c> target
/// (<c>OnMissionScreenFinalize</c>). The two <c>AccessTools.FieldRefAccess</c> lookups live inside the
/// patch and are NOT covered there — a 1.4.x field rename would silently disable the release fix
/// (fail-safe back to today's leak + hang) with no other red test. These are those red tests.
/// </para>
/// </summary>
[TestClass]
public class Patch60TournamentExitMovieReleaseTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void GauntletLayerField_BindingResolves_AgainstInstalledEngine()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        Assert.IsNotNull(
            Patch60_TournamentExitMovieRelease.LayerField,
            "MissionGauntletTournamentView._gauntletLayer did not resolve against the installed engine — " +
            "engine drift would silently disable the tournament-exit movie release (fail-safe to the vanilla leak + hang).");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void GauntletMovieField_BindingResolves_AgainstInstalledEngine()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        Assert.IsNotNull(
            Patch60_TournamentExitMovieRelease.MovieField,
            "MissionGauntletTournamentView._gauntletMovie did not resolve against the installed engine — " +
            "engine drift would silently disable the tournament-exit movie release (fail-safe to the vanilla leak + hang).");
    }
}
