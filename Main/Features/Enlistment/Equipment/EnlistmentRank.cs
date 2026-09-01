namespace TAOM.Features.Enlistment.Equipment;

/// <summary>
/// Enlistment service ranks, ordered: numeric comparison IS the progression order
/// (the ledger's once-per-rank check and the resolver's descending fallback both
/// rely on it). Tokens (lowercase names) appear in roster ids —
/// <c>enlist_{cultureId}_{assignment}_{rank}</c> in taom_enlistment_equipment.xml
/// (the assignment token joined the id in #525, when the kit stopped being armour-only).
/// </summary>
public enum EnlistmentRank
{
    Recruit = 0,
    Soldier = 1,
    Veteran = 2,
    Sergeant = 3,
}
