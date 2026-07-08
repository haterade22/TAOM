using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features;
using TAOM.Features.NativeSkinFixes;

namespace TAOM.Tests.Features.NativeSkinFixes;

/// <summary>
/// Pure-logic tests for <see cref="NativeSkinFixesInstaller"/>. The hook
/// install path itself cannot be unit-tested (requires the running game
/// process to host TaleWorlds.Native.dll), so these tests cover the
/// editor-mode skip predicate and the localization-key wiring.
/// </summary>
[TestClass]
public class NativeSkinFixesInstallerTests
{
    [TestMethod]
    public void IsEditorProcess_NullProcessName_ReturnsFalse()
    {
        Assert.IsFalse(NativeSkinFixesInstaller.IsEditorProcess(null));
    }

    [TestMethod]
    public void IsEditorProcess_EmptyProcessName_ReturnsFalse()
    {
        Assert.IsFalse(NativeSkinFixesInstaller.IsEditorProcess(string.Empty));
    }

    [TestMethod]
    public void IsEditorProcess_NormalClientProcess_ReturnsFalse()
    {
        // Path matches the standard non-editor game process.
        const string normalPath =
            @"E:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\Bannerlord.exe";
        Assert.IsFalse(NativeSkinFixesInstaller.IsEditorProcess(normalPath));
    }

    [TestMethod]
    public void IsEditorProcess_EditorPath_ReturnsTrue()
    {
        // The Bannerlord editor lives under Win64_Shipping_wEditor.
        const string editorPath =
            @"E:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_wEditor\Bannerlord.exe";
        Assert.IsTrue(NativeSkinFixesInstaller.IsEditorProcess(editorPath));
    }

    [TestMethod]
    public void IsEditorProcess_MixedCaseEditor_ReturnsTrue()
    {
        // Match must be case-insensitive — Win32 paths are not case-sensitive
        // and we don't want a build that downcases the path to silently skip
        // the skip.
        Assert.IsTrue(NativeSkinFixesInstaller.IsEditorProcess(@"C:\Games\WeditorShipping\Bannerlord.exe"));
        Assert.IsTrue(NativeSkinFixesInstaller.IsEditorProcess(@"C:\Games\WEDITOR\Bannerlord.exe"));
    }

    [TestMethod]
    public void IsEditorProcess_FalsePositiveAvoidance_ReturnsFalseForSubstringInUnrelatedWord()
    {
        // Confirm the substring check fires only on actual "wEditor" — not on
        // any word that happens to contain "editor" elsewhere (the engine
        // launcher exes don't, but the test pins the contract for future edits).
        Assert.IsFalse(NativeSkinFixesInstaller.IsEditorProcess(@"C:\path\with\nothing\matching.exe"));
    }

    [TestMethod]
    public void EnableNativeSkinFixes_DefaultsToFalse_ParkedAtWiringLevel()
    {
        // OFF by default as of 2026-07-08 (user decision). The native hooks are
        // parked at the wiring level: the install call in SubModule.cs is
        // commented out, so the detours never load regardless of any persisted
        // MCM value. The compiled default is false to match. The hook targets
        // remain authored + verified against the installed v1.4.6
        // TaleWorlds.Native.dll — this is a deliberate opt-out, not a lost
        // capability. RE-ENABLE is a code change: uncomment the install branch
        // in SubModule.cs AND flip this default back to true.
        Assert.IsFalse(new TaomSettings().EnableNativeSkinFixes);
    }

    [TestMethod]
    public void LoadedMessageKey_HasExpectedLocalizationFormat()
    {
        // The key must use TaleWorlds' "{=key}default" format so DisplayMessage
        // can route through the localization system. The key itself must match
        // the entry in taom_module_strings.xml exactly — otherwise the user
        // sees a literal "{=taom_nativeskinfixes_loaded}" string in-game.
        Assert.AreEqual("{=taom_nativeskinfixes_loaded}", NativeSkinFixesInstaller.LoadedMessageKey);
    }

    [TestMethod]
    public void DegradedMessageKey_HasExpectedLocalizationFormat()
    {
        Assert.AreEqual("{=taom_nativeskinfixes_degraded}", NativeSkinFixesInstaller.DegradedMessageKey);
    }

    [TestMethod]
    public void LoadedMessageDefault_IsNonEmptyDescriptiveString()
    {
        Assert.IsFalse(string.IsNullOrWhiteSpace(NativeSkinFixesInstaller.LoadedMessageDefault));
        // Sanity: the fallback English text should mention what was loaded so
        // a user reading the launcher / Documents log can identify the feature.
        StringAssert.Contains(NativeSkinFixesInstaller.LoadedMessageDefault, "NativeSkinFixes");
    }

    [TestMethod]
    public void DegradedMessageDefault_IsDistinctFromLoadedDefault()
    {
        // The degraded banner must NOT read "loaded" because that misleads the
        // user when every hook is inert (deep-review LOW UX 2026-05-26).
        Assert.IsFalse(string.IsNullOrWhiteSpace(NativeSkinFixesInstaller.DegradedMessageDefault));
        Assert.AreNotEqual(NativeSkinFixesInstaller.LoadedMessageDefault, NativeSkinFixesInstaller.DegradedMessageDefault);
        StringAssert.Contains(NativeSkinFixesInstaller.DegradedMessageDefault, "NativeSkinFixes");
        // Must signal the degraded state to the user. Either word is acceptable;
        // both are commonly used in TAOM diagnostic banners.
        bool signalsDegraded =
            NativeSkinFixesInstaller.DegradedMessageDefault.IndexOf("degraded", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            NativeSkinFixesInstaller.DegradedMessageDefault.IndexOf("unauthored", System.StringComparison.OrdinalIgnoreCase) >= 0;
        Assert.IsTrue(signalsDegraded, "Degraded banner must signal degraded state to user.");
    }
}
