using System;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.LocalizationOverride;
using TAOM.Features.LocalizationOverride.Hooks;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.LocalizationOverride;

[TestClass]
public class MBTextManager_GetLocalizedText_PatchTests
{
    private ITextLocalizerAdapter _localizer = null!;

    [TestInitialize]
    public void Setup()
    {
        MBTextManager_GetLocalizedText_Patch.ClearOverrides();
        _localizer = Substitute.For<ITextLocalizerAdapter>();
        _localizer.ActiveLanguage.Returns("English");
        MBTextManager_GetLocalizedText_Patch.UseLanguageGate(
            new OverrideLanguageGate(_localizer, () => Substitute.For<IModLogger>()));
    }

    [TestCleanup]
    public void Cleanup()
    {
        MBTextManager_GetLocalizedText_Patch.UseLanguageGate(null);
    }

    [TestMethod]
    public void Prefix_RegisteredId_SetsResultAndSkipsOriginal()
    {
        // Arrange
        MBTextManager_GetLocalizedText_Patch.RegisterOverride("0cdXddA9", "{KINGDOM1_LINK} has formed an alliance with {KINGDOM2_LINK}.");
        string result = null;

        // Act
        bool runOriginal = MBTextManager_GetLocalizedText_Patch.Prefix("{=0cdXddA9}The {KINGDOM1_LINK} have formed an alliance with the {KINGDOM2_LINK}.", ref result);

        // Assert
        Assert.IsFalse(runOriginal);
        Assert.AreEqual("{KINGDOM1_LINK} has formed an alliance with {KINGDOM2_LINK}.", result);
    }

    [TestMethod]
    public void Prefix_UnregisteredId_FallsThrough()
    {
        // Arrange
        string result = null;

        // Act
        bool runOriginal = MBTextManager_GetLocalizedText_Patch.Prefix("{=someUnknownId}Some text here.", ref result);

        // Assert
        Assert.IsTrue(runOriginal);
        Assert.IsNull(result);
    }

    [TestMethod]
    public void Prefix_NullText_FallsThrough()
    {
        // Arrange
        string result = null;

        // Act
        bool runOriginal = MBTextManager_GetLocalizedText_Patch.Prefix(null, ref result);

        // Assert
        Assert.IsTrue(runOriginal);
        Assert.IsNull(result);
    }

    [TestMethod]
    public void Prefix_EmptyText_FallsThrough()
    {
        // Arrange
        string result = null;

        // Act
        bool runOriginal = MBTextManager_GetLocalizedText_Patch.Prefix("", ref result);

        // Assert
        Assert.IsTrue(runOriginal);
        Assert.IsNull(result);
    }

    [TestMethod]
    public void Prefix_ShortText_FallsThrough()
    {
        // Arrange
        string result = null;

        // Act
        bool runOriginal = MBTextManager_GetLocalizedText_Patch.Prefix("{=", ref result);

        // Assert
        Assert.IsTrue(runOriginal);
        Assert.IsNull(result);
    }

    [TestMethod]
    public void Prefix_SpecialMarkerBang_FallsThrough()
    {
        // Arrange
        MBTextManager_GetLocalizedText_Patch.RegisterOverride("!", "should not match");
        string result = null;

        // Act
        bool runOriginal = MBTextManager_GetLocalizedText_Patch.Prefix("{=!}Some dynamic text.", ref result);

        // Assert
        Assert.IsTrue(runOriginal);
        Assert.IsNull(result);
    }

    [TestMethod]
    public void Prefix_SpecialMarkerStar_FallsThrough()
    {
        // Arrange
        MBTextManager_GetLocalizedText_Patch.RegisterOverride("*", "should not match");
        string result = null;

        // Act
        bool runOriginal = MBTextManager_GetLocalizedText_Patch.Prefix("{=*}Some dynamic text.", ref result);

        // Assert
        Assert.IsTrue(runOriginal);
        Assert.IsNull(result);
    }

    [TestMethod]
    public void Prefix_NoClosingBrace_FallsThrough()
    {
        // Arrange
        string result = null;

        // Act
        bool runOriginal = MBTextManager_GetLocalizedText_Patch.Prefix("{=0cdXddA9 missing brace", ref result);

        // Assert
        Assert.IsTrue(runOriginal);
        Assert.IsNull(result);
    }

    [TestMethod]
    public void Prefix_TextNotStartingWithLocalizationMarker_FallsThrough()
    {
        // Arrange
        string result = null;

        // Act
        bool runOriginal = MBTextManager_GetLocalizedText_Patch.Prefix("Just plain text", ref result);

        // Assert
        Assert.IsTrue(runOriginal);
        Assert.IsNull(result);
    }

    [TestMethod]
    public void RegisterOverride_OverwritesPreviousValue()
    {
        // Arrange
        MBTextManager_GetLocalizedText_Patch.RegisterOverride("testId", "first value");
        MBTextManager_GetLocalizedText_Patch.RegisterOverride("testId", "second value");
        string result = null;

        // Act
        MBTextManager_GetLocalizedText_Patch.Prefix("{=testId}original text", ref result);

        // Assert
        Assert.AreEqual("second value", result);
    }

    [TestMethod]
    public void ClearOverrides_RemovesAllRegisteredOverrides()
    {
        // Arrange
        MBTextManager_GetLocalizedText_Patch.RegisterOverride("0cdXddA9", "override text");
        MBTextManager_GetLocalizedText_Patch.ClearOverrides();
        string result = null;

        // Act
        bool runOriginal = MBTextManager_GetLocalizedText_Patch.Prefix("{=0cdXddA9}The {KINGDOM1_LINK} have formed.", ref result);

        // Assert
        Assert.IsTrue(runOriginal);
        Assert.IsNull(result);
    }

    // The prefix runs on every localized text resolve, so it reads the {=ID} in place instead of
    // cutting it out into a new string. The probe's key (IdSlice) compares and hashes on that path too.
    [TestMethod]
    public void Prefix_NeverCallsSubstring()
    {
        var prefix = typeof(MBTextManager_GetLocalizedText_Patch).GetMethod("Prefix");
        var slice = typeof(IdSlice);
        var scanned = new MethodBase[]
        {
            prefix,
            slice.GetMethod(nameof(IdSlice.Equals), new[] { slice }),
            slice.GetMethod(nameof(IdSlice.GetHashCode), Type.EmptyTypes),
        };

        foreach (var method in scanned)
        {
            Assert.IsNotNull(method);
            var called = IlCallScanner.ExtractCalledMethods(method, method.GetMethodBody().GetILAsByteArray()).ToList();

            Assert.IsTrue(called.Count > 0, "the scan read the calls of " + method.Name);
            Assert.IsFalse(called.Any(m => m.DeclaringType == typeof(string) && m.Name == "Substring"),
                method.DeclaringType.Name + "." + method.Name + " allocates an id substring per call");
        }
    }

    [TestMethod]
    public void Prefix_TextIdIsAPrefixOfARegisteredId_FallsThrough()
    {
        MBTextManager_GetLocalizedText_Patch.RegisterOverride("abcd", "override");
        string result = null;

        Assert.IsTrue(MBTextManager_GetLocalizedText_Patch.Prefix("{=abc}x", ref result));
        Assert.IsNull(result);
    }

    [TestMethod]
    public void Prefix_RegisteredIdIsAPrefixOfTheTextId_FallsThrough()
    {
        MBTextManager_GetLocalizedText_Patch.RegisterOverride("abc", "override");
        string result = null;

        Assert.IsTrue(MBTextManager_GetLocalizedText_Patch.Prefix("{=abcd}x", ref result));
        Assert.IsNull(result);
    }

    [TestMethod]
    public void Prefix_IdDiffersOnlyInCase_FallsThrough()
    {
        MBTextManager_GetLocalizedText_Patch.RegisterOverride("AbC", "override");
        string result = null;

        Assert.IsTrue(MBTextManager_GetLocalizedText_Patch.Prefix("{=abc}x", ref result));
        Assert.IsNull(result);
    }

    [TestMethod]
    public void Prefix_ManyRegisteredIds_EachTextGetsItsOwnOverride()
    {
        for (int i = 0; i < 500; i++)
            MBTextManager_GetLocalizedText_Patch.RegisterOverride("id" + i, "text" + i);

        for (int i = 0; i < 500; i++)
        {
            string result = null;
            Assert.IsFalse(MBTextManager_GetLocalizedText_Patch.Prefix("{=id" + i + "}tail", ref result), "id" + i);
            Assert.AreEqual("text" + i, result);
        }
    }

    [TestMethod]
    public void Prefix_EmptyIdRegistered_MatchesTheEmptyIdText()
    {
        MBTextManager_GetLocalizedText_Patch.RegisterOverride("", "E");
        string result = null;

        Assert.IsFalse(MBTextManager_GetLocalizedText_Patch.Prefix("{=}tail", ref result));
        Assert.AreEqual("E", result);
    }

    [TestMethod]
    public void RegisterOverride_NullId_ThrowsArgumentNullException()
    {
        Assert.ThrowsException<ArgumentNullException>(
            () => MBTextManager_GetLocalizedText_Patch.RegisterOverride(null, "text"));
    }

    // #706: vanilla skips its dictionary only for English, so the table is English-only. Any other
    // language falls through to vanilla, which reads that language's translated row.
    [TestMethod]
    public void Prefix_EnglishLanguage_RegisteredId_ReturnsTheOverride()
    {
        MBTextManager_GetLocalizedText_Patch.RegisterOverride("aom_ab_notable_0", "Far Harad dynasty broker");
        string result = null!;

        bool runOriginal = MBTextManager_GetLocalizedText_Patch.Prefix("{=aom_ab_notable_0}Far Harad dynasty broker", ref result);

        Assert.IsFalse(runOriginal);
        Assert.AreEqual("Far Harad dynasty broker", result);
    }

    [TestMethod]
    public void Prefix_NonEnglishLanguage_RegisteredId_FallsThroughToVanilla()
    {
        MBTextManager_GetLocalizedText_Patch.RegisterOverride("aom_ab_notable_0", "Far Harad dynasty broker");
        _localizer.ActiveLanguage.Returns("Deutsch");
        string result = null!;

        bool runOriginal = MBTextManager_GetLocalizedText_Patch.Prefix("{=aom_ab_notable_0}Far Harad dynasty broker", ref result);

        Assert.IsTrue(runOriginal);
        Assert.IsNull(result);
    }

    [TestMethod]
    public void Prefix_LanguageChangesWhileRunning_AppliesTheOverrideOnlyWhileEnglish()
    {
        MBTextManager_GetLocalizedText_Patch.RegisterOverride("aom_ab_notable_0", "Far Harad dynasty broker");
        const string text = "{=aom_ab_notable_0}Far Harad dynasty broker";
        string result = null!;

        Assert.IsFalse(MBTextManager_GetLocalizedText_Patch.Prefix(text, ref result));
        Assert.AreEqual("Far Harad dynasty broker", result);

        _localizer.ActiveLanguage.Returns("Deutsch");
        result = null!;
        Assert.IsTrue(MBTextManager_GetLocalizedText_Patch.Prefix(text, ref result));
        Assert.IsNull(result);

        _localizer.ActiveLanguage.Returns("English");
        Assert.IsFalse(MBTextManager_GetLocalizedText_Patch.Prefix(text, ref result));
        Assert.AreEqual("Far Harad dynasty broker", result);
    }

    // The gate the game runs is the real adapter over MBTextManager, which a process that never changed
    // language leaves on English (v1.5.3 MBTextManager.cs:36). Needs the engine, so not on hosted CI.
    [TestMethod]
    [TestCategory("RequiresGame")]
    public void Prefix_GameGate_EngineLanguageIsEnglish_ReturnsTheOverride()
    {
        MBTextManager_GetLocalizedText_Patch.UseLanguageGate(null);
        MBTextManager_GetLocalizedText_Patch.RegisterOverride("aom_ab_notable_0", "Far Harad dynasty broker");
        string result = null!;

        bool runOriginal = MBTextManager_GetLocalizedText_Patch.Prefix("{=aom_ab_notable_0}Far Harad dynasty broker", ref result);

        Assert.IsFalse(runOriginal);
        Assert.AreEqual("Far Harad dynasty broker", result);
    }
}
