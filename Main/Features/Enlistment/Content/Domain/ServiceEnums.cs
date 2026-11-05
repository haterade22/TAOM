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
