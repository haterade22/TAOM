namespace TAOM.Features.Music;

public sealed class MusicRouteSnapshot
{
    public MusicRouteSnapshot(
        bool isActive,
        string cultureId,
        bool siege,
        bool battle,
        bool tavern,
        bool town,
        bool world,
        string reason = null)
        : this(isActive, cultureId, siege, battle, tavern, town, world, false, reason)
    {
    }

    public MusicRouteSnapshot(
        bool isActive,
        string cultureId,
        bool siege,
        bool battle,
        bool tavern,
        bool town,
        bool world,
        bool characterCreation,
        string reason = null)
    {
        IsActive = isActive;
        CultureId = string.IsNullOrWhiteSpace(cultureId) ? MusicTrackIndex.NeutralCulture : cultureId.Trim();
        Siege = siege;
        Battle = battle;
        Tavern = tavern;
        Town = town;
        World = world;
        CharacterCreation = characterCreation;
        Reason = string.IsNullOrWhiteSpace(reason) ? string.Empty : reason.Trim();
    }

    public static MusicRouteSnapshot Empty { get; } =
        new MusicRouteSnapshot(false, MusicTrackIndex.NeutralCulture, false, false, false, false, false, false, "empty");

    public bool IsActive { get; }

    public string CultureId { get; }

    public bool Siege { get; }

    public bool Battle { get; }

    public bool Tavern { get; }

    public bool Town { get; }

    public bool World { get; }

    public bool CharacterCreation { get; }

    public string Reason { get; }
}
