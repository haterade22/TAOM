namespace TAOM.Features.CheatMenu;

/// <summary>The settlement lists of the teleport page.</summary>
public enum CheatSettlementKind
{
    Town,
    Castle,
    Village,
}

/// <summary>Why the main party cannot be teleported now; None means it can (docs/features/cheat-menu.md, "Teleport").</summary>
public enum TeleportRefusal
{
    None = 0,
    NoCampaign,
    InMission,
    InSettlement,
    InBattleOrEncounter,
    InSiege,
    InArmyNotLeader,
    Prisoner,
    PartyParked,
    AtSea,
    InFieldCamp,
}

/// <summary>The main party's state a teleport is judged on, read by the world adapter in one go.</summary>
public sealed record TeleportState(
    bool CampaignRunning,
    bool InMission,
    bool InSettlement,
    bool InMapEventOrEncounter,
    bool InSiege,
    bool InArmyNotLeader,
    bool IsPrisoner,
    bool PartyActive,
    bool AtSea,
    bool InFieldCamp);

/// <summary>
/// The pure rules of the cheat menu. A teleport only moves a party that stands free on the land map: case 12 (14 Nov 2026)
/// crashed when a third-party panel moved the party out of a village whose menu and encounter stayed open, so the party must
/// already be outside every settlement, encounter, battle, siege, army, captivity, enlistment (a parked, inactive party), ship
/// and field camp. Checked in this order, so the message names the first thing the player has to end.
/// </summary>
public static class CheatMenuPolicy
{
    /// <summary>The menu and its button exist only with the MCM switch on and in single player.</summary>
    public static bool IsMenuAvailable(bool enabled, bool singlePlayerAuthority) => enabled && singlePlayerAuthority;

    public static TeleportRefusal CheckTeleport(TeleportState s)
    {
        if (!s.CampaignRunning) return TeleportRefusal.NoCampaign;
        if (s.InMission) return TeleportRefusal.InMission;
        if (s.IsPrisoner) return TeleportRefusal.Prisoner;
        if (s.InMapEventOrEncounter) return TeleportRefusal.InBattleOrEncounter;
        if (s.InSiege) return TeleportRefusal.InSiege;
        if (s.InSettlement) return TeleportRefusal.InSettlement;
        if (s.InArmyNotLeader) return TeleportRefusal.InArmyNotLeader;
        if (!s.PartyActive) return TeleportRefusal.PartyParked;
        if (s.AtSea) return TeleportRefusal.AtSea;
        if (s.InFieldCamp) return TeleportRefusal.InFieldCamp;
        return TeleportRefusal.None;
    }
}
