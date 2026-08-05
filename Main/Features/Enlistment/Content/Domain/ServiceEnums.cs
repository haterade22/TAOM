namespace TAOM.Features.Enlistment.Content.Domain;

/// <summary>Service rank ladder. Ordinal order is the promotion order — never reorder (persisted as int).</summary>
public enum ServiceRank
{
    Recruit = 0,
    Soldier = 1,
    Veteran = 2,
    Sergeant = 3,
}

/// <summary>Player's chosen service role. Persisted as int — never reorder.</summary>
public enum ServiceAssignment
{
    Infantry = 0,
    Archer = 1,
    Cavalry = 2,
    Support = 3,
}

/// <summary>
/// The 5 field-duty mechanics. The donor's 13 duty types collapse onto these; per-duty
/// flavor (briefings, targets, rewards) stays in data rows.
/// </summary>
public enum DutyMechanic
{
    HuntSpawnedParty = 0,
    VisitSettlement = 1,
    DeliverFood = 2,
    CollectFood = 3,
    WaitHours = 4,
}

public enum DutyTargetKind
{
    None = 0,
    SpawnedLooterParty = 1,
    FriendlySettlement = 2,
    FriendlyVillage = 3,
    AllySettlement = 4,
}

/// <summary>Spawned-hunt AI: MountedPursuit keeps the donor's engage-the-player fiction; the rest patrol.</summary>
public enum DutyTargetAi
{
    PatrolAnchor = 0,
    EngagePlayer = 1,
}

/// <summary>Commander-reputation domains (donor's four, enum-typed instead of strings).</summary>
public enum ReputationDomain
{
    None = 0,
    Field = 1,
    Logistics = 2,
    Command = 3,
    Siege = 4,
}

/// <summary>Time-of-day phase for the army-rhythm scheduler.</summary>
public enum ServicePhase
{
    Night = 0,
    Dawn = 1,
    Midday = 2,
    Dusk = 3,
}
