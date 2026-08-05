namespace TAOM.Features.Enlistment.Equipment;

/// <summary>
/// Enlistment service ranks, ordered: numeric comparison IS the progression order
/// (the ledger's once-per-rank check and the resolver's descending fallback both
/// rely on it). Tokens (lowercase names) appear in roster ids —
/// <c>enlist_{cultureId}_{rank}</c> in taom_enlistment_equipment.xml.
/// </summary>
public enum EnlistmentRank
{
    Recruit = 0,
    Soldier = 1,
    Veteran = 2,
    Sergeant = 3,
}
