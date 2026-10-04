using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.MapPerf;

/// <summary>
/// Source pins for the Patch101 map profiler's two SubModule.cs lines. The installer must run on EVERY game
/// init, before the once-per-process guard, so a later game init can report (restart needed, or the install
/// failed); it applies its categories at most once itself. The session must close in OnGameEnd so a quit to
/// the main menu writes its [MapProfileSummary]. Neither can be exercised without the game.
/// </summary>
[TestClass]
public class MapFrameProfilerWiringTests
{
    private const string InstallCall = "Features.MapPerf.Hooks.MapFrameProfilerInstaller.OnGameInitialized(";
    private const string EndCall = "Features.MapPerf.Hooks.MapSessionHooks.EndSession(\"gameEnd\")";

    private static string Source() => RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);

    private static int Count(string text, string needle) => Regex.Matches(text, Regex.Escape(needle)).Count;

    [TestMethod]
    public void SubModule_CallsTheInstaller_OnEveryGameInit_BeforeTheOncePerProcessGuard()
    {
        var src = Source();
        Assert.AreEqual(1, Count(src, InstallCall), "The installer is called exactly once.");
        var install = src.IndexOf(InstallCall);
        var initFinished = src.IndexOf("public override void OnGameInitializationFinished");
        var gating = src.IndexOf("ApplyGating(", initFinished);
        var guard = src.IndexOf("if (_gameInitPatchesApplied) return;");

        Assert.IsTrue(initFinished >= 0 && gating >= 0 && guard >= 0, "An anchor moved.");
        Assert.IsTrue(install > initFinished, "The installer runs in OnGameInitializationFinished.");
        Assert.IsTrue(install > gating, "The installer follows the armour gating.");
        Assert.IsTrue(install < guard, "The installer runs before the once-per-process guard, on every game init.");
    }

    [TestMethod]
    public void SubModule_EndsTheMapSession_InOnGameEnd()
    {
        var src = Source();
        Assert.AreEqual(1, Count(src, EndCall), "The session end is called exactly once.");
        var end = src.IndexOf(EndCall);
        var onGameEnd = src.IndexOf("public override void OnGameEnd(Game game)");
        var onGameStart = src.IndexOf("protected override void OnGameStart(");

        Assert.IsTrue(onGameEnd >= 0 && onGameStart >= 0, "An anchor moved.");
        Assert.IsTrue(end > onGameEnd && end < onGameStart, "The session end belongs in OnGameEnd.");
    }
}
