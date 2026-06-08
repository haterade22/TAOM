using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Music;

namespace TAOM.Tests.Features.Music;

[TestClass]
public class MusicTransitionResolverTests
{
    [TestMethod]
    public void Resolve_UsesMissionSnapshotBeforeCampaignSnapshot()
    {
        var resolver = new MusicTransitionResolver();
        var index = Index();
        var mission = Snapshot("gondor", world: true, reason: "mission_world");
        var campaign = Snapshot("gondor", siege: true, reason: "campaign_siege");

        var decision = resolver.Resolve(index, MusicRouteSettings.AllEnabled, mission, campaign);

        Assert.IsTrue(decision.HasSelection);
        Assert.AreEqual(MusicRouteSource.Mission, decision.Source);
        Assert.AreEqual(MusicBucket.World, decision.Bucket);
        Assert.AreEqual("mission_world", decision.Reason);
    }

    [TestMethod]
    public void Resolve_FallsThroughMissionBucketOrderWhenFirstBucketHasNoCandidates()
    {
        var resolver = new MusicTransitionResolver();
        var index = Index();
        var mission = Snapshot("gondor", siege: true, battle: true, world: true);

        var decision = resolver.Resolve(index, MusicRouteSettings.AllEnabled, mission, MusicRouteSnapshot.Empty);

        Assert.IsTrue(decision.HasSelection);
        Assert.AreEqual(MusicBucket.Battle, decision.Bucket);
        Assert.AreEqual("gondor", decision.Pool.ResolvedCultureId);
    }

    [TestMethod]
    public void Resolve_UsesCharacterCreationBucketWhenSnapshotRequestsIt()
    {
        var resolver = new MusicTransitionResolver();
        var index = Index();
        var campaign = Snapshot("gondor", characterCreation: true, world: true, reason: "cc");

        var decision = resolver.Resolve(index, MusicRouteSettings.AllEnabled, MusicRouteSnapshot.Empty, campaign);

        Assert.IsTrue(decision.HasSelection);
        Assert.AreEqual(MusicRouteSource.Campaign, decision.Source);
        Assert.AreEqual(MusicBucket.CharacterCreation, decision.Bucket);
        Assert.AreEqual("gondor", decision.Pool.ResolvedCultureId);
        Assert.AreEqual("taom/character_creation/gondor/cc_a", decision.Pool.Tracks[0].EventName);
    }

    [TestMethod]
    public void Resolve_UsesNeutralFallbackForMissingCharacterCreationCulturePool()
    {
        var resolver = new MusicTransitionResolver();
        var index = Index();
        var campaign = Snapshot("shaghana", characterCreation: true);

        var decision = resolver.Resolve(index, MusicRouteSettings.AllEnabled, MusicRouteSnapshot.Empty, campaign);

        Assert.IsTrue(decision.HasSelection);
        Assert.AreEqual(MusicBucket.CharacterCreation, decision.Bucket);
        Assert.IsTrue(decision.Pool.UsedNeutralFallback);
        Assert.AreEqual(MusicTrackIndex.NeutralCulture, decision.Pool.ResolvedCultureId);
    }

    [TestMethod]
    public void Resolve_SkipsDisabledBuckets()
    {
        var resolver = new MusicTransitionResolver();
        var index = Index();
        var settings = new MusicRouteSettings(
            musicEnabled: true,
            siegeEnabled: true,
            battleEnabled: false,
            tavernEnabled: true,
            townEnabled: true,
            worldEnabled: true);
        var mission = Snapshot("gondor", battle: true, town: true);

        var decision = resolver.Resolve(index, settings, mission, MusicRouteSnapshot.Empty);

        Assert.IsTrue(decision.HasSelection);
        Assert.AreEqual(MusicBucket.Town, decision.Bucket);
    }

    [TestMethod]
    public void Resolve_UsesNeutralCultureFallbackForMissingCulturePool()
    {
        var resolver = new MusicTransitionResolver();
        var index = Index();
        var campaign = Snapshot("shaghana", town: true);

        var decision = resolver.Resolve(index, MusicRouteSettings.AllEnabled, MusicRouteSnapshot.Empty, campaign);

        Assert.IsTrue(decision.HasSelection);
        Assert.AreEqual(MusicRouteSource.Campaign, decision.Source);
        Assert.AreEqual(MusicBucket.Town, decision.Bucket);
        Assert.IsTrue(decision.Pool.UsedNeutralFallback);
        Assert.AreEqual(MusicTrackIndex.NeutralCulture, decision.Pool.ResolvedCultureId);
    }

    [TestMethod]
    public void Resolve_ReturnsNoSelectionWhenMusicDisabled()
    {
        var resolver = new MusicTransitionResolver();
        var settings = new MusicRouteSettings(false, true, true, true, true, true);

        var decision = resolver.Resolve(Index(), settings, Snapshot("gondor", battle: true), MusicRouteSnapshot.Empty);

        Assert.IsFalse(decision.HasSelection);
        Assert.AreEqual(MusicRouteSource.None, decision.Source);
        Assert.AreEqual("music_disabled", decision.Reason);
    }

    [TestMethod]
    public void Resolve_ReturnsNoSelectionWhenNoCandidatePoolExists()
    {
        var resolver = new MusicTransitionResolver();
        var index = Index();
        var mission = Snapshot("missing_culture", siege: true);
        var campaign = Snapshot("missing_culture", siege: true);

        var decision = resolver.Resolve(index, MusicRouteSettings.AllEnabled, mission, campaign);

        Assert.IsFalse(decision.HasSelection);
        Assert.AreEqual("no_candidate_pool", decision.Reason);
    }

    private static MusicRouteSnapshot Snapshot(
        string culture,
        bool siege = false,
        bool battle = false,
        bool tavern = false,
        bool town = false,
        bool world = false,
        bool characterCreation = false,
        string reason = null)
    {
        return new MusicRouteSnapshot(true, culture, siege, battle, tavern, town, world, characterCreation, reason);
    }

    private static MusicTrackIndex Index()
    {
        return MusicTrackIndex.LoadFromModuleSoundDocuments(
            "D:/TAOM/Main/_Module",
            new[] { XDocument.Parse(Xml()) });
    }

    private static string Xml()
    {
        return @"<?xml version=""1.0"" encoding=""utf-8""?>
<base type=""module_sound"">
  <module_sounds>
    <module_sound name=""taom/battle_music/gondor/battle_a"" is_2d=""true"" sound_category=""music"" path=""taom/battle_music/gondor/battle_a.ogg"" />
    <module_sound name=""taom/character_creation/gondor/cc_a"" is_2d=""true"" sound_category=""music"" path=""taom/character_creation/gondor/cc_a.ogg"" />
    <module_sound name=""taom/character_creation/neutral_culture/cc_neutral"" is_2d=""true"" sound_category=""music"" path=""taom/character_creation/neutral_culture/cc_neutral.ogg"" />
    <module_sound name=""taom/town_wander/gondor/town_a"" is_2d=""true"" sound_category=""music"" path=""taom/town_wander/gondor/town_a.ogg"" />
    <module_sound name=""taom/worldmap/gondor/world_a"" is_2d=""true"" sound_category=""music"" path=""taom/worldmap/gondor/world_a.ogg"" />
    <module_sound name=""taom/town_wander/neutral_culture/town_neutral"" is_2d=""true"" sound_category=""music"" path=""taom/town_wander/neutral_culture/town_neutral.ogg"" />
  </module_sounds>
</base>";
    }
}
