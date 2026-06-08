using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Features.Music;

[TestClass]
public class MusicTavernContextFeedWiringTests
{
    [TestMethod]
    public void CampaignContextSource_UsesExplicitTavernContextFeedInsteadOfHardCodedFalse()
    {
        var source = ReadProjectSource("Main", "Adapters", "TaleWorldsMusicCampaignContextSource.cs");

        StringAssert.Contains(source, "IMusicTavernContextSource",
            "Campaign context must consume an explicit tavern context feed before it can route Tavern instead of Town.");
        Assert.IsFalse(source.Contains("settlement != null,\r\n                false,"),
            "Campaign context currently hard-codes inTavern=false, so tavern visits route as Town.");
    }

    [TestMethod]
    public void MissionContextSource_UsesExplicitTavernContextFeedInsteadOfHardCodedFalse()
    {
        var source = ReadProjectSource("Main", "Adapters", "TaleWorldsMusicMissionContextSource.cs");

        StringAssert.Contains(source, "IMusicTavernContextSource",
            "Mission context must consume an explicit tavern context feed before MusicianGroup suppression can see Tavern.");
        Assert.IsFalse(source.Contains("false,\r\n                false,\r\n                fallbackCultureId,"),
            "Mission context currently hard-codes town=false and tavern=false, so tavern missions fall back to campaign Town.");
    }

    [TestMethod]
    public void MusicIoC_RegistersExplicitTavernContextFeed()
    {
        var source = ReadProjectSource("Main", "Features", "Music", "MusicIoC.cs");

        StringAssert.Contains(source, "IMusicTavernContextSource",
            "The explicit tavern context feed must be registered with the music IoC boundary.");
    }

    [TestMethod]
    public void SetPlayListPatch_CallsMarkTavernContextInactiveWhenPlaylistIsNullOrEmpty()
    {
        // The SetPlayList patch must call MarkTavernContextInactive when vanilla clears the musician
        // group (null/empty list = tavern exit). Verify the guard exists at the source level.
        var source = ReadProjectSource("Main", "Features", "Music", "Hooks", "MusicianGroupSuppressionPatches.cs");

        StringAssert.Contains(source, "MarkTavernContextInactive",
            "SetPlayList Prefix must call MarkTavernContextInactive when playList is null/empty so the " +
            "tavern context flag doesn't stay sticky after the player leaves the tavern.");
        StringAssert.Contains(source, "playList == null || playList.Count == 0",
            "The empty/null guard must be present before the suppression check.");
    }

    private static string ReadProjectSource(params string[] relativeParts)
    {
        var path = Path.Combine(MusicTestPaths.RepositoryRootPath, Path.Combine(relativeParts));
        Assert.IsTrue(File.Exists(path), $"Project source file not found: {path}");
        return File.ReadAllText(path);
    }
}
