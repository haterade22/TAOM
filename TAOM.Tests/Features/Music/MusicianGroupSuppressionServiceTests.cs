using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.Music;

namespace TAOM.Tests.Features.Music;

[TestClass]
public class MusicianGroupSuppressionServiceTests
{
    private IMusicSettingsProvider _settings;
    private IMusicPlaybackService _playback;
    private MusicianGroupSuppressionService _service;

    [TestInitialize]
    public void SetUp()
    {
        _settings = Substitute.For<IMusicSettingsProvider>();
        _playback = Substitute.For<IMusicPlaybackService>();
        _settings.GetSnapshot().Returns(Settings());

        _service = new MusicianGroupSuppressionService(_settings, _playback);
    }

    [TestMethod]
    public void ShouldSuppressVanillaTavernMusic_ReturnsTrueWhenTaomOwnsActiveTavernBucket()
    {
        _playback.IsActiveBucket(MusicBucket.Tavern).Returns(true);

        Assert.IsTrue(_service.ShouldSuppressVanillaTavernMusic());
    }

    [TestMethod]
    public void ShouldSuppressVanillaTavernMusic_ReturnsFalseWhenMusicDisabled()
    {
        _settings.GetSnapshot().Returns(Settings(musicEnabled: false));
        _playback.IsActiveBucket(MusicBucket.Tavern).Returns(true);

        Assert.IsFalse(_service.ShouldSuppressVanillaTavernMusic());
    }

    [TestMethod]
    public void ShouldSuppressVanillaTavernMusic_ReturnsFalseWhenTavernBucketDisabled()
    {
        _settings.GetSnapshot().Returns(Settings(tavernEnabled: false));
        _playback.IsActiveBucket(MusicBucket.Tavern).Returns(true);

        Assert.IsFalse(_service.ShouldSuppressVanillaTavernMusic());
    }

    [TestMethod]
    public void ShouldSuppressVanillaTavernMusic_ReturnsFalseWhenTaomDoesNotOwnTavernBucket()
    {
        _playback.IsActiveBucket(MusicBucket.Tavern).Returns(false);

        Assert.IsFalse(_service.ShouldSuppressVanillaTavernMusic());
    }

    private static MusicSettingsSnapshot Settings(bool musicEnabled = true, bool tavernEnabled = true)
    {
        return new MusicSettingsSnapshot(
            musicEnabled: musicEnabled,
            campaignContextEnabled: true,
            missionContextEnabled: true,
            routeSettings: new MusicRouteSettings(
                musicEnabled: musicEnabled,
                siegeEnabled: true,
                battleEnabled: true,
                tavernEnabled: tavernEnabled,
                townEnabled: true,
                worldEnabled: true),
            rotation: new MusicRotationPolicy.RotationSnapshot(
                musicEnabled: musicEnabled,
                enableWorldRotation: true,
                enableTownRotation: true,
                enableBattleRotation: true,
                worldRotateIntervalSeconds: 180f,
                townRotateIntervalSeconds: 180f,
                battleRotateIntervalSeconds: 180f,
                characterCreationRotateIntervalSeconds: 180f),
            useNoRepeatShuffle: true,
            noRepeatHistorySize: 8,
            masterVolume: 1f,
            worldVolume: 1f,
            townVolume: 1f,
            tavernVolume: 1f,
            battleVolume: 1f,
            siegeVolume: 1f);
    }
}
