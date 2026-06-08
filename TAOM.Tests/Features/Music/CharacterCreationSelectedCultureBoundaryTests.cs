using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Features.Music;

[TestClass]
public class CharacterCreationSelectedCultureBoundaryTests
{
    [TestMethod]
    public void SelectedCulturePatch_DelegatesStringIdToMusicContextService()
    {
        var source = ReadProjectSource(
            "Main",
            "Features",
            "CharacterCreation",
            "Hooks",
            "CharacterCreationContent_SetSelectedCulture_Patch.cs");

        StringAssert.Contains(source, "ICharacterCreationMusicContextService",
            "The selected-culture boundary must delegate to the music context service.");
        StringAssert.Contains(source, ".ConfirmCulture(culture.StringId)",
            "The boundary must pass only culture.StringId into the music service, not TaleWorlds CultureObject.");
        StringAssert.Contains(source, "CharacterCreationMusicSmokeTrace.CultureConfirmed(culture.StringId)",
            "The selected-culture boundary must emit the smoke marker that proves culture selection reached the CC music path.");
    }

    [TestMethod]
    public void ExecuteSelectCultureMusicPatch_DelegatesSameBucketCultureChangesToMusicContextService()
    {
        var source = ReadProjectSource(
            "Main",
            "Features",
            "Music",
            "Hooks",
            "CharacterCreationCultureVM_ExecuteSelectCulture_MusicPatch.cs");

        StringAssert.Contains(source, "ICharacterCreationMusicContextService",
            "Live character-creation culture selection must feed the music context before final persistence.");
        StringAssert.Contains(source, ".SelectCulture(cultureId)",
            "The live picker boundary must pass only culture.StringId into the music service.");
        StringAssert.Contains(source, "CharacterCreationMusicSmokeTrace.CultureSelected(cultureId",
            "The live picker boundary must emit the selected-culture smoke marker for same-bucket CC culture changes.");
    }

    [TestMethod]
    public void BodyPropertiesCultureStageSelectionPatch_DoesNotOwnMusicSignal()
    {
        var source = ReadProjectSource(
            "Main",
            "Features",
            "CharacterCreation",
            "Hooks",
            "CharacterCreationCultureStageVM_OnCultureSelection_Patch.cs");

        Assert.IsFalse(source.Contains("ICharacterCreationMusicContextService"),
            "Patch29_CCBodyProperties must not duplicate the Patch46_Music selected-culture signal.");
        Assert.IsFalse(source.Contains("CharacterCreationMusicSmokeTrace"),
            "Patch29_CCBodyProperties must not emit music smoke markers.");
    }

    [TestMethod]
    public void FactionMapRegionSelectionBoundary_DelegatesSelectedCultureToMusicContextService()
    {
        var source = ReadProjectSource(
            "Main",
            "Features",
            "FactionMap",
            "Hooks",
            "CultureStageViewCreatedHook.cs");

        StringAssert.Contains(source, "OnFactionMapCultureSelected",
            "The FactionMap culture-stage hook must wire selected region changes into the music boundary.");
        StringAssert.Contains(source, "ICharacterCreationMusicContextService",
            "The FactionMap boundary must resolve the already-tested character creation music context service.");
        StringAssert.Contains(source, ".SelectCulture(cultureId)",
            "Region selection must update the live selected-culture route without waiting for final confirmation.");
        StringAssert.Contains(source, "CharacterCreationMusicSmokeTrace.CultureSelected(cultureId, \"faction_map_region_selected\")",
            "The map-region path must emit a distinct selected-culture smoke marker for in-game verification.");
    }

    private static string ReadProjectSource(params string[] relativeParts)
    {
        var path = Path.Combine(MusicTestPaths.RepositoryRootPath, Path.Combine(relativeParts));
        Assert.IsTrue(File.Exists(path), $"Project source file not found: {path}");
        return File.ReadAllText(path);
    }
}
