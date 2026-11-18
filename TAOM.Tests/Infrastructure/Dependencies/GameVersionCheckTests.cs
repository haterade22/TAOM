using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Dependencies.Foundation;

namespace TAOM.Tests.Infrastructure.Dependencies;

/// <summary>
/// TAOM.Dependencies warns, before the engine's "Error while loading TAOM" box, when the game is not the Bannerlord
/// version TAOM's SubModule.xml pins for Native. These pin the compare, the pin reader, the text and where it runs.
/// </summary>
[TestClass]
public class GameVersionCheckTests
{
    private const string Pin = "v1.5.7.*";

    [TestMethod]
    public void Evaluate_SameVersion_Matches()
    {
        Assert.AreEqual(GameVersionMatch.Match, GameVersionCheck.Evaluate(Pin, 1, 5, 7));
    }

    [TestMethod]
    public void Evaluate_OtherRevision_IsMismatch()
    {
        Assert.AreEqual(GameVersionMatch.Mismatch, GameVersionCheck.Evaluate(Pin, 1, 5, 6));
        Assert.AreEqual(GameVersionMatch.Mismatch, GameVersionCheck.Evaluate(Pin, 1, 5, 8));
    }

    [TestMethod]
    public void Evaluate_OtherMinor_IsMismatch()
    {
        Assert.AreEqual(GameVersionMatch.Mismatch, GameVersionCheck.Evaluate(Pin, 1, 4, 7));
        Assert.AreEqual(GameVersionMatch.Mismatch, GameVersionCheck.Evaluate(Pin, 1, 6, 7));
    }

    [TestMethod]
    public void Evaluate_OtherMajor_IsMismatch()
    {
        Assert.AreEqual(GameVersionMatch.Mismatch, GameVersionCheck.Evaluate(Pin, 2, 5, 7));
    }

    [TestMethod]
    public void Evaluate_WildcardRevision_MatchesEveryRevision()
    {
        Assert.AreEqual(GameVersionMatch.Match, GameVersionCheck.Evaluate("v1.5.*", 1, 5, 0));
        Assert.AreEqual(GameVersionMatch.Match, GameVersionCheck.Evaluate("v1.5.*", 1, 5, 9));
        Assert.AreEqual(GameVersionMatch.Mismatch, GameVersionCheck.Evaluate("v1.5.*", 1, 4, 9));
    }

    [TestMethod]
    public void Evaluate_PinWithoutWildcardOrLetter_ComparesItsNumbers()
    {
        Assert.AreEqual(GameVersionMatch.Match, GameVersionCheck.Evaluate("v1.5.7", 1, 5, 7));
        Assert.AreEqual(GameVersionMatch.Match, GameVersionCheck.Evaluate("1.5.7.*", 1, 5, 7));
        Assert.AreEqual(GameVersionMatch.Mismatch, GameVersionCheck.Evaluate("1.5.7", 1, 5, 6));
    }

    [TestMethod]
    public void Evaluate_GameVersionNotDetected_IsUnknown_NoWarning()
    {
        // VersionProbe leaves Major at 0 when the engine read failed.
        Assert.AreEqual(GameVersionMatch.Unknown, GameVersionCheck.Evaluate(Pin, 0, 0, 0));
    }

    [TestMethod]
    public void Evaluate_PinMissingOrUnreadable_IsUnknown_NoWarning()
    {
        foreach (var bad in new[] { null, "", "  ", "v1", "1", "vX.5.7", "v1.*.7", "v1.5.7.1.2", "v1.-5.7", "*" })
            Assert.AreEqual(GameVersionMatch.Unknown, GameVersionCheck.Evaluate(bad, 1, 5, 6), bad ?? "null");
    }

    [TestMethod]
    public void ReadSubModule_ShippedTaomFile_PinsTheGameVersionTheRepoTargets()
    {
        var path = RepoPaths.RepoPath("Main", "_Module", "SubModule.xml");
        var (pin, version) = GameVersionCheck.ReadSubModule(File.ReadAllText(path));

        var pinned = File.ReadAllText(RepoPaths.RepoPath(".claude", "pinned-game-version.txt")).Trim();
        Assert.AreEqual(pinned + ".*", pin);
        Assert.AreEqual((string)XDocument.Load(path).Root!.Element("Version")!.Attribute("value")!, version);
    }

    [TestMethod]
    public void ReadSubModule_FindsNativeAmongOtherMetadata_CaseInsensitiveId()
    {
        const string xml = "<Module><Version value='v2.0.40'/><DependedModuleMetadatas>"
            + "<DependedModuleMetadata id='TAOM.Dependencies' version='v2.0.40'/>"
            + "<DependedModuleMetadata id='native' version='v1.4.8.*'/>"
            + "</DependedModuleMetadatas></Module>";
        Assert.AreEqual(("v1.4.8.*", "v2.0.40"), GameVersionCheck.ReadSubModule(xml));
    }

    [TestMethod]
    public void ReadSubModule_NoNativePin_GivesNullPin()
    {
        Assert.AreEqual((null, "v2.0.40"), GameVersionCheck.ReadSubModule(
            "<Module><Version value='v2.0.40'/><DependedModuleMetadatas>"
            + "<DependedModuleMetadata id='Native' order='LoadBeforeThis'/></DependedModuleMetadatas></Module>"));
    }

    [TestMethod]
    public void ReadSubModule_EmptyOrBrokenFile_GivesNothing_AndDoesNotThrow()
    {
        Assert.AreEqual(((string?)null, (string?)null), GameVersionCheck.ReadSubModule(null));
        Assert.AreEqual(((string?)null, (string?)null), GameVersionCheck.ReadSubModule(""));
        Assert.AreEqual(((string?)null, (string?)null), GameVersionCheck.ReadSubModule("<Module><Version"));
    }

    [TestMethod]
    public void DisplayPin_DropsWildcards_AndAddsTheLetter()
    {
        Assert.AreEqual("v1.5.7", GameVersionCheck.DisplayPin("v1.5.7.*"));
        Assert.AreEqual("v1.5", GameVersionCheck.DisplayPin("v1.5.*"));
        Assert.AreEqual("v1.5.7", GameVersionCheck.DisplayPin("1.5.7"));
    }

    [TestMethod]
    public void Text_BoxBeforeTheGamesError_NamesBothVersions_TheGamesBox_AndBothFixes()
    {
        var text = GameVersionCheck.Text(Pin, "v2.0.40", 1, 5, 6, taomSkipped: false);

        StringAssert.Contains(text, "This TAOM (v2.0.40) is made for Bannerlord v1.5.7, but your game is v1.5.6.");
        StringAssert.Contains(text, "missing or conflicting dependency");
        Assert.IsFalse(text.Contains("turned off"));
        AssertFixesAndStyle(text);
    }

    [TestMethod]
    public void Text_MenuPopupAfterTaomWasTurnedOff_SaysCloseTheGame_NotTheGamesBox()
    {
        var text = GameVersionCheck.Text(Pin, "v2.0.40", 1, 5, 6, taomSkipped: true);

        StringAssert.Contains(text, "This TAOM (v2.0.40) is made for Bannerlord v1.5.7, but your game is v1.5.6.");
        StringAssert.Contains(text, "TAOM is turned off for this session");
        StringAssert.Contains(text, "Press Exit Game to close the game.");
        Assert.IsFalse(text.Contains("conflicting dependency"), "no engine box follows in this case");
        AssertFixesAndStyle(text);
    }

    [TestMethod]
    public void Text_WithoutTaomVersion_StillReads()
    {
        StringAssert.StartsWith(GameVersionCheck.Text(Pin, null, 1, 4, 8, taomSkipped: true), "This TAOM is made for Bannerlord v1.5.7, but your game is v1.4.8.");
    }

    [TestMethod]
    public void LoadErrorHint_WrongVersion_NamesTheCause_BothVersions_AndTheSteamStep()
    {
        var hint = GameVersionCheck.LoadErrorHint(Pin, "v2.0.40", 1, 5, 6);

        StringAssert.StartsWith(hint, "This error is usually caused by the wrong Bannerlord version.");
        StringAssert.Contains(hint, "This TAOM (v2.0.40) is made for Bannerlord v1.5.7, but your game is v1.5.6.");
        StringAssert.Contains(hint, "Properties, then Betas, and pick v1.5.7");
        AssertHintStyle(hint);
    }

    [TestMethod]
    public void LoadErrorHint_RightVersion_PointsAtAMismatchedTaomInstall_NotTheGame()
    {
        // The engine box can still show on the right game version: a stale TAOM.Dependencies (#371) or a mixed install.
        var hint = GameVersionCheck.LoadErrorHint(Pin, "v2.0.40", 1, 5, 7);

        StringAssert.StartsWith(hint, "This error is usually caused by the wrong Bannerlord version.");
        StringAssert.Contains(hint, "Your game is v1.5.7, the version this TAOM (v2.0.40) is made for");
        StringAssert.Contains(hint, "TAOM launcher");
        Assert.IsFalse(hint.Contains("Betas"), "the game version is right: do not send the player to change it");
        AssertHintStyle(hint);
    }

    [TestMethod]
    public void LoadErrorHint_PinOrGameVersionUnknown_StillNamesTheCause_WithoutInventedVersions()
    {
        foreach (var hint in new[]
                 {
                     GameVersionCheck.LoadErrorHint(null, null, 1, 5, 6),
                     GameVersionCheck.LoadErrorHint(Pin, null, 0, 0, 0),
                 })
        {
            StringAssert.StartsWith(hint, "This error is usually caused by the wrong Bannerlord version.");
            Assert.IsFalse(hint.Contains("your game is v"), hint);
            Assert.IsFalse(hint.Contains("v0."), hint);
            AssertHintStyle(hint);
        }
        StringAssert.Contains(GameVersionCheck.LoadErrorHint(Pin, null, 0, 0, 0), "This TAOM is made for Bannerlord v1.5.7.");
    }

    private static void AssertHintStyle(string hint)
    {
        Assert.IsFalse(hint.Contains("{="), "no language key: the engine box shows before TAOM's strings matter");
        Assert.IsFalse(hint.Contains("—") || hint.Contains("–"), "no em or en dash");
        Assert.IsFalse(hint.Contains("\n"), "one paragraph: the postfix adds the line breaks");
    }

    [TestMethod]
    public void Notice_InstallsTheLoadErrorHint_AfterTheSkips_BeforeTheMismatchReturn()
    {
        var run = Body(RepoPaths.ReadSource("Dependencies/GameVersionNotice.cs", stripComments: true), "private static void Run()");
        int kit = run.IndexOf("IncompatibleModDetector.IsEditorBin(", StringComparison.Ordinal);
        int hint = run.IndexOf("LoadErrorHint.TryInstall(", StringComparison.Ordinal);
        int mismatch = run.IndexOf("if (match != GameVersionMatch.Mismatch) return;", StringComparison.Ordinal);
        Assert.IsTrue(kit >= 0 && hint > kit && mismatch > hint,
            "the hint is for every client start (Match and Unknown too), never the server or the Kit");
    }

    [TestMethod]
    public void LoadErrorHint_Postfix_AppendsOnlyForTaomAndItsDependents_AndNeverThrows()
    {
        var source = RepoPaths.ReadSource("Dependencies/LoadErrorHint.cs", stripComments: true);
        var install = Body(source, "internal static void TryInstall(");
        // The same owners as the guard's skip list, so a module the guard turns off never gets a box of its own.
        StringAssert.Contains(install, "GameVersionNotice.OwnerModules(modules)");
        StringAssert.Contains(install, "\"GetAssemblyLoadResultsMessage\"");
        StringAssert.Contains(install, "catch (Exception");

        var postfix = Body(source, "private static void AppendHint(");
        StringAssert.Contains(postfix, "!_owners.Contains(module.Id)) return;");
        // The engine joins one message per failed submodule into one box (Module.cs:1144); TAOM.Client fails with
        // TAOM.dll, so without this the paragraph shows twice.
        StringAssert.Contains(postfix, "_appended) return;");
        StringAssert.Contains(postfix, "_appended = true;");
        StringAssert.Contains(postfix, "catch (Exception");
    }

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void LoadErrorHint_Target_BindsOnTheReferencedEngine_WithThePostfixShape()
    {
        var target = typeof(TaleWorlds.MountAndBlade.Module).GetMethod("GetAssemblyLoadResultsMessage",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.IsNotNull(target, "Module.GetAssemblyLoadResultsMessage is gone: the engine box shows without the hint");
        Assert.AreEqual(typeof(string), target!.ReturnType);
        var p = target.GetParameters();
        Assert.AreEqual(typeof(TaleWorlds.ModuleManager.ModuleInfo), p[0].ParameterType);
        Assert.AreEqual("module", p[0].Name, "the postfix binds the argument by this name");
        // A method with a try region is never inlined into HandleSubmoduleLoadError, so the detour is reached.
        Assert.IsTrue(target.GetMethodBody()!.ExceptionHandlingClauses.Count > 0, "foreach over the result list keeps a try/finally");
    }

    private static void AssertFixesAndStyle(string text)
    {
        StringAssert.Contains(text, "Properties, then Betas, and pick v1.5.7");
        StringAssert.Contains(text, "TAOM launcher");
        // The launcher may not have a TAOM for the player's game version yet; the text must not promise one.
        StringAssert.Contains(text, "if a TAOM made for your game version is out");
        Assert.IsFalse(text.Contains("{="), "no language key: TAOM's strings are not read when this shows");
        Assert.IsFalse(text.Contains("\u2014") || text.Contains("\u2013"), "no em or en dash");
    }

    [TestMethod]
    public void SubModuleConstructor_RunsTheNotice()
    {
        // The engine constructs every submodule before any OnSubModuleLoad, and a TAOM that does not fit the
        // game aborts between the two, so the notice must run from the constructor.
        var body = Body(RepoPaths.ReadSource("Dependencies/SubModule.cs", stripComments: true), "public SubModule()");
        StringAssert.Contains(body, "GameVersionNotice.ShowOnceIfMismatched()");
    }

    [TestMethod]
    public void Notice_ChecksServer_First_AndCatchesEverything()
    {
        var source = RepoPaths.ReadSource("Dependencies/GameVersionNotice.cs", stripComments: true);
        var run = Body(source, "private static void Run()");
        // MBDebug.TestModeEnabled is not read: /runTest sets it only after the submodules load (Module.cs:315), so here it is always false.
        Assert.IsFalse(run.Contains("TestModeEnabled"), "TestModeEnabled is always false while submodules are constructed");
        int guard = run.IndexOf("if (GameNetwork.IsDedicatedServer)", StringComparison.Ordinal);
        int mismatch = run.IndexOf("if (match != GameVersionMatch.Mismatch) return;", StringComparison.Ordinal);
        int skip = run.IndexOf("TrySkipTaomCode(", StringComparison.Ordinal);
        int box = run.IndexOf("ShowMessageBox(", StringComparison.Ordinal);
        Assert.IsTrue(guard >= 0 && mismatch > guard && skip > mismatch && box > skip,
            "server/test guard, then only on a mismatch: turn TAOM's code off, else the box");
        int kit = run.IndexOf("IncompatibleModDetector.IsEditorBin(", StringComparison.Ordinal);
        Assert.IsTrue(kit > guard && kit < mismatch, "the Modding Kit is skipped before anything is read or turned off");
        StringAssert.Contains(run, "taomSkipped: true");
        StringAssert.Contains(run, "taomSkipped: false");

        var entry = Body(source, "internal static void ShowOnceIfMismatched()");
        StringAssert.Contains(entry, "try { Run(); }");
        StringAssert.Contains(entry, "catch (Exception");
        StringAssert.Contains(source, "[MethodImpl(MethodImplOptions.NoInlining)]\n    private static void Run()");
    }

    [TestMethod]
    public void Guard_PatchesTheEnginesPerSubmoduleCheck_AndSkipsOnlyTaomsSubmodules()
    {
        var source = RepoPaths.ReadSource("Dependencies/GameVersionNotice.cs", stripComments: true);
        var install = Body(source, "private static bool TrySkipTaomCode(");
        StringAssert.Contains(install, "OwnerModules(modules)");
        // OwnerModules is expression-bodied, so Body() (brace matching) can't cut it.
        StringAssert.Contains(source, "internal static List<ModuleInfo> OwnerModules(List<ModuleInfo> modules) =>\n        modules.Where(m => IsTaom(m.Id) || m.DependedModules.Any(d => IsTaom(d.ModuleId))).ToList();");
        StringAssert.Contains(install, "nameof(Module.CheckIfSubmoduleCanBeLoadable)");

        var prefix = Body(source, "private static bool SkipTaomSubModule(");
        StringAssert.Contains(prefix, "!_skipped.Contains(subModuleInfo)) return true;");
        StringAssert.Contains(prefix, "__result = false;");
    }

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void Guard_Target_BindsOnTheReferencedEngine_WithThePrefixShape()
    {
        var target = typeof(TaleWorlds.MountAndBlade.Module).GetMethod("CheckIfSubmoduleCanBeLoadable",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
        Assert.IsNotNull(target, "Module.CheckIfSubmoduleCanBeLoadable is gone: the version guard falls back to the box");
        Assert.AreEqual(typeof(bool), target!.ReturnType);
        var p = target.GetParameters();
        Assert.AreEqual(1, p.Length);
        Assert.AreEqual(typeof(TaleWorlds.ModuleManager.SubModuleInfo), p[0].ParameterType);
        Assert.AreEqual("subModuleInfo", p[0].Name, "the prefix binds the argument by this name");
        // A method with a try region is never inlined into LoadSubModules, so the detour is reached.
        Assert.IsTrue(target.GetMethodBody()!.ExceptionHandlingClauses.Count > 0, "foreach over the tag list keeps a try/finally");
    }

    [TestMethod]
    public void MenuNotice_ShowsTheVersionText_BeforeTheMissingTaomCheck()
    {
        // TAOM stays in the loaded list when its code is turned off, so MissingTaomCheck alone would say nothing.
        var tick = Body(RepoPaths.ReadSource("Dependencies/MissingTaomNotice.cs", stripComments: true), "internal static void Tick()");
        int version = tick.IndexOf("GameVersionNotice.MenuText", StringComparison.Ordinal);
        int missing = tick.IndexOf("MissingTaomCheck.Evaluate(", StringComparison.Ordinal);
        Assert.IsTrue(version >= 0 && missing > version);
    }

    [TestMethod]
    public void MenuNotice_OkClosesTheGame_OnlyForTheWrongVersionPopup()
    {
        var source = RepoPaths.ReadSource("Dependencies/MissingTaomNotice.cs", stripComments: true);

        // Tick: the version popup quits, the missing-TAOM popup does not.
        var tick = Body(source, "internal static void Tick()");
        StringAssert.Contains(tick, "Show(versionText, quitOnOk: true)");
        StringAssert.Contains(tick, "Show(Text(reason), quitOnOk: false)");

        // Show: the flag really wires the quit and the Exit Game label (a Show that ignored it would pass the above).
        // Show is expression-bodied, so Body() (brace matching) can't cut it: take the text up to the next method.
        var showStart = source.IndexOf("private static void Show(", StringComparison.Ordinal);
        var showEnd = source.IndexOf("private static void QuitGame()", StringComparison.Ordinal);
        Assert.IsTrue(showStart >= 0 && showEnd > showStart, "Show must sit before QuitGame in MissingTaomNotice");
        var show = source.Substring(showStart, showEnd - showStart);
        StringAssert.Contains(show, "quitOnOk ? QuitGame : (Action?)null");
        StringAssert.Contains(show, "quitOnOk ? new TextObject(\"{=YbpzLHzk}Exit Game\")");
    }

    [TestMethod]
    [TestCategory("LiveInstall")]
    public void InstalledGame_MatchesTheShippedPin()
    {
        string? env = Environment.GetEnvironmentVariable("BANNERLORD_GAME_DIR");
        var game = string.IsNullOrWhiteSpace(env) ? @"E:\Steam\steamapps\common\Mount & Blade II Bannerlord" : env!;
        var versionFile = Path.Combine(game, "bin", "Win64_Shipping_Client", "Version.xml");
        if (!Directory.Exists(game))
            Assert.Inconclusive("Bannerlord install not found.");
        Assert.IsTrue(File.Exists(versionFile), "the game install has no Version.xml: " + versionFile);

        // The game reads its version from text compiled into TaleWorlds.Library (VirtualFolders), not from this file.
        // bin\Win64_Shipping_Client\Version.xml carries the same version and is what this test can read:
        // the first child's Value, <Version><Singleplayer Value="v1.5.7"/>.
        var value = (string)XDocument.Load(versionFile).Root!.Elements().First().Attribute("Value")!;
        var parts = value.Substring(1).Split('.');
        var (pin, _) = GameVersionCheck.ReadSubModule(File.ReadAllText(RepoPaths.RepoPath("Main", "_Module", "SubModule.xml")));

        Assert.AreEqual(GameVersionMatch.Match,
            GameVersionCheck.Evaluate(pin, int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2])),
            $"installed game {value} vs TAOM pin {pin}");
    }

    private static string Body(string code, string signature)
    {
        var start = code.IndexOf(signature, StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, "source no longer declares " + signature);
        var open = code.IndexOf('{', start);
        var depth = 0;
        for (var i = open; i < code.Length; i++)
        {
            if (code[i] == '{') depth++;
            else if (code[i] == '}' && --depth == 0)
                return code.Substring(open, i - open + 1);
        }
        Assert.Fail("Unbalanced braces after " + signature);
        return string.Empty;
    }
}
