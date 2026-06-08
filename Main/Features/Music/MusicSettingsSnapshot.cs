namespace TAOM.Features.Music;

public sealed class MusicSettingsSnapshot
{
    public MusicSettingsSnapshot(
        bool musicEnabled,
        bool campaignContextEnabled,
        bool missionContextEnabled,
        MusicRouteSettings routeSettings,
        MusicRotationPolicy.RotationSnapshot rotation,
        bool useNoRepeatShuffle,
        int noRepeatHistorySize,
        float masterVolume,
        float worldVolume,
        float townVolume,
        float tavernVolume,
        float battleVolume,
        float siegeVolume)
    {
        MusicEnabled = musicEnabled;
        CampaignContextEnabled = campaignContextEnabled;
        MissionContextEnabled = missionContextEnabled;
        RouteSettings = routeSettings ?? MusicRouteSettings.AllEnabled;
        Rotation = rotation;
        UseNoRepeatShuffle = useNoRepeatShuffle;
        NoRepeatHistorySize = ClampInt(noRepeatHistorySize, 0, 64);
        MasterVolume = Clamp01(masterVolume);
        WorldVolume = Clamp01(worldVolume);
        TownVolume = Clamp01(townVolume);
        TavernVolume = Clamp01(tavernVolume);
        BattleVolume = Clamp01(battleVolume);
        SiegeVolume = Clamp01(siegeVolume);
    }

    public static MusicSettingsSnapshot Default { get; } =
        new MusicSettingsSnapshot(
            musicEnabled: true,
            campaignContextEnabled: true,
            missionContextEnabled: true,
            routeSettings: MusicRouteSettings.AllEnabled,
            rotation: new MusicRotationPolicy.RotationSnapshot(
                musicEnabled: true,
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

    public bool MusicEnabled { get; }

    public bool CampaignContextEnabled { get; }

    public bool MissionContextEnabled { get; }

    public MusicRouteSettings RouteSettings { get; }

    public MusicRotationPolicy.RotationSnapshot Rotation { get; }

    public bool UseNoRepeatShuffle { get; }

    public int NoRepeatHistorySize { get; }

    public float MasterVolume { get; }

    public float WorldVolume { get; }

    public float TownVolume { get; }

    public float TavernVolume { get; }

    public float BattleVolume { get; }

    public float SiegeVolume { get; }

    public float GetBucketVolume(MusicBucket bucket)
    {
        switch (bucket)
        {
            case MusicBucket.World:
                return MasterVolume * WorldVolume;
            case MusicBucket.Town:
                return MasterVolume * TownVolume;
            case MusicBucket.Tavern:
                return MasterVolume * TavernVolume;
            case MusicBucket.Battle:
                return MasterVolume * BattleVolume;
            case MusicBucket.Siege:
                return MasterVolume * SiegeVolume;
            case MusicBucket.CharacterCreation:
                return MasterVolume * TownVolume;
            default:
                return MasterVolume;
        }
    }

    private static int ClampInt(int value, int min, int max)
    {
        if (value < min)
            return min;
        return value > max ? max : value;
    }

    private static float Clamp01(float value)
    {
        return ClampFloat(value, 0f, 1f);
    }

    private static float ClampFloat(float value, float min, float max)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
            return min;
        if (value < min)
            return min;
        return value > max ? max : value;
    }
}
