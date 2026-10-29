namespace TAOM.Features.StagedSiege.Domain;

/// <summary>Why the lord's hall point E did or did not arm; the first failed guard names the cause in the log.</summary>
public enum HallVerdict
{
    Armed,
    NotAHallMission,
    NoCampaign,
    Client,
    NotInTheLordsHall,
    Disabled,
    WallsSceneNotRecorded,
    NoMarkers,
}

/// <summary>The hall arming decision, with every engine reading already taken as a plain bool.</summary>
public static class StagedHallActivation
{
    /// <summary>Guards are checked in parameter order.</summary>
    public static HallVerdict Decide(bool isHallMission, bool hasCampaign, bool isClient, bool siegeInLordsHall, bool enabled,
        bool wallsSceneRecorded, bool hasMarkers)
    {
        if (!isHallMission) return HallVerdict.NotAHallMission;
        if (!hasCampaign) return HallVerdict.NoCampaign;
        if (isClient) return HallVerdict.Client;
        if (!siegeInLordsHall) return HallVerdict.NotInTheLordsHall;
        if (!enabled) return HallVerdict.Disabled;
        if (!wallsSceneRecorded) return HallVerdict.WallsSceneNotRecorded;
        if (!hasMarkers) return HallVerdict.NoMarkers;
        return HallVerdict.Armed;
    }
}
