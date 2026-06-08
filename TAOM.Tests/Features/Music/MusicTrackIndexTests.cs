using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Music;

namespace TAOM.Tests.Features.Music;

[TestClass]
public class MusicTrackIndexTests
{
    [TestMethod]
    public void ResolvePool_ReturnsCultureSpecificTracksWhenAvailable()
    {
        var index = MusicTrackIndex.LoadFromModuleSoundDocuments(
            "D:/TAOM/Main/_Module",
            new[] { XDocument.Parse(XmlWithTracks()) });

        var pool = index.ResolvePool(MusicBucket.Battle, "gondor");

        Assert.IsTrue(pool.HasTracks);
        Assert.IsFalse(pool.UsedNeutralFallback);
        Assert.AreEqual("gondor", pool.ResolvedCultureId);
        Assert.AreEqual(2, pool.Tracks.Count);
        CollectionAssert.AreEqual(
            new[] { "taom/battle_music/gondor/battle_a", "taom/battle_music/gondor/battle_b" },
            pool.Tracks.Select(t => t.EventName).ToArray());
    }

    [TestMethod]
    public void ResolvePool_FallsBackToNeutralCultureWhenSpecificPoolIsMissing()
    {
        var index = MusicTrackIndex.LoadFromModuleSoundDocuments(
            "D:/TAOM/Main/_Module",
            new[] { XDocument.Parse(XmlWithTracks()) });

        var pool = index.ResolvePool(MusicBucket.Battle, "shaghana");

        Assert.IsTrue(pool.HasTracks);
        Assert.IsTrue(pool.UsedNeutralFallback);
        Assert.AreEqual("shaghana", pool.RequestedCultureId);
        Assert.AreEqual(MusicTrackIndex.NeutralCulture, pool.ResolvedCultureId);
        Assert.AreEqual("taom/battle_music/neutral_culture/battle_neutral", pool.Tracks[0].EventName);
    }

    [TestMethod]
    public void ResolvePool_ReturnsEmptyPoolWhenBucketHasNoNeutralFallback()
    {
        var index = MusicTrackIndex.LoadFromModuleSoundDocuments(
            "D:/TAOM/Main/_Module",
            new[] { XDocument.Parse(XmlWithTracks()) });

        var pool = index.ResolvePool(MusicBucket.Siege, "missing_culture");

        Assert.IsFalse(pool.HasTracks);
        Assert.IsFalse(pool.UsedNeutralFallback);
        Assert.AreEqual(string.Empty, pool.ResolvedCultureId);
    }

    [TestMethod]
    public void LoadFromDocuments_IgnoresNonTaomAndNonMusicEntries()
    {
        var index = MusicTrackIndex.LoadFromModuleSoundDocuments(
            "D:/TAOM/Main/_Module",
            new[] { XDocument.Parse(XmlWithTracks()) });

        Assert.AreEqual(4, index.Count);
    }

    [TestMethod]
    public void TrackDefinitions_KeepEventNameRelativePathAndAbsolutePath()
    {
        var index = MusicTrackIndex.LoadFromModuleSoundDocuments(
            "D:/TAOM/Main/_Module",
            new[] { XDocument.Parse(XmlWithTracks()) });

        var track = index.ResolvePool(MusicBucket.Town, "gondor").Tracks[0];

        Assert.AreEqual("taom/town_wander/gondor/town_a", track.EventName);
        Assert.AreEqual("taom/town_wander/gondor/town_a.ogg", track.RelativePath);
        StringAssert.Contains(track.AbsolutePath.Replace('\\', '/'), "ModuleSounds/taom/town_wander/gondor/town_a.ogg");
    }

    private static string XmlWithTracks()
    {
        return @"<?xml version=""1.0"" encoding=""utf-8""?>
<base type=""module_sound"">
  <module_sounds>
    <module_sound name=""taom/battle_music/gondor/battle_b"" is_2d=""true"" sound_category=""music"" path=""taom/battle_music/gondor/battle_b.ogg"" />
    <module_sound name=""taom/battle_music/gondor/battle_a"" is_2d=""true"" sound_category=""music"" path=""taom/battle_music/gondor/battle_a.ogg"" />
    <module_sound name=""taom/battle_music/neutral_culture/battle_neutral"" is_2d=""true"" sound_category=""music"" path=""taom/battle_music/neutral_culture/battle_neutral.ogg"" />
    <module_sound name=""taom/town_wander/gondor/town_a"" is_2d=""true"" sound_category=""music"" path=""taom/town_wander/gondor/town_a.ogg"" />
    <module_sound name=""taom/battle_music/gondor/not_music"" is_2d=""true"" sound_category=""mission_voice"" path=""taom/battle_music/gondor/not_music.ogg"" />
    <module_sound name=""taom/battle_music/gondor/not_2d"" is_2d=""false"" sound_category=""music"" path=""taom/battle_music/gondor/not_2d.ogg"" />
    <module_sound name=""LOTR/OST/legacy"" is_2d=""true"" sound_category=""music"" path=""LOTR/OST/legacy.ogg"" />
  </module_sounds>
</base>";
    }
}
