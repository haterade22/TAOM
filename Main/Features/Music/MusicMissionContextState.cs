namespace TAOM.Features.Music;

public sealed class MusicMissionContextState
{
    public MusicMissionContextState(
        bool isActive,
        bool siege,
        bool battle,
        bool town,
        bool tavern,
        string cultureId,
        string sceneId,
        string reason = null)
    {
        IsActive = isActive;
        Siege = siege;
        Battle = battle;
        Town = town;
        Tavern = tavern;
        CultureId = Normalize(cultureId);
        SceneId = Normalize(sceneId);
        Reason = Normalize(reason);
    }

    public static MusicMissionContextState Inactive { get; } =
        new MusicMissionContextState(false, false, false, false, false, null, null, "inactive");

    public bool IsActive { get; }

    public bool Siege { get; }

    public bool Battle { get; }

    public bool Town { get; }

    public bool Tavern { get; }

    public string CultureId { get; }

    public string SceneId { get; }

    public string Reason { get; }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
