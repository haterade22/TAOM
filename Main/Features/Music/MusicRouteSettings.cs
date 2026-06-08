namespace TAOM.Features.Music;

public sealed class MusicRouteSettings
{
    public MusicRouteSettings(
        bool musicEnabled,
        bool siegeEnabled,
        bool battleEnabled,
        bool tavernEnabled,
        bool townEnabled,
        bool worldEnabled)
    {
        MusicEnabled = musicEnabled;
        SiegeEnabled = siegeEnabled;
        BattleEnabled = battleEnabled;
        TavernEnabled = tavernEnabled;
        TownEnabled = townEnabled;
        WorldEnabled = worldEnabled;
    }

    public static MusicRouteSettings AllEnabled { get; } =
        new MusicRouteSettings(true, true, true, true, true, true);

    public bool MusicEnabled { get; }

    public bool SiegeEnabled { get; }

    public bool BattleEnabled { get; }

    public bool TavernEnabled { get; }

    public bool TownEnabled { get; }

    public bool WorldEnabled { get; }

    public bool IsBucketEnabled(MusicBucket bucket)
    {
        switch (bucket)
        {
            case MusicBucket.Siege:
                return SiegeEnabled;
            case MusicBucket.Battle:
                return BattleEnabled;
            case MusicBucket.Tavern:
                return TavernEnabled;
            case MusicBucket.Town:
                return TownEnabled;
            case MusicBucket.World:
                return WorldEnabled;
            case MusicBucket.CharacterCreation:
                return TownEnabled;
            default:
                return false;
        }
    }
}
