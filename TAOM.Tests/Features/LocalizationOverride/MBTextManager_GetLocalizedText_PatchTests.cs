using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LocalizationOverride.Hooks;

namespace TAOM.Tests.Features.LocalizationOverride;

[TestClass]
public class MBTextManager_GetLocalizedText_PatchTests
{
    [TestInitialize]
    public void Setup()
    {
        MBTextManager_GetLocalizedText_Patch.ClearOverrides();
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
}
