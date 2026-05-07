namespace TAOM.Features.CompanionTactics.Roles.Models;

/// <summary>
/// Combat role classifications for companions/heroes, derived purely from equipped weapons +
/// mount status. Ported verbatim from the original developer's drop (no Healer/Support — the
/// source has no skill-based detection). Order is deliberate; underlying ints are stable for
/// future SaveableField use, though currently they are recomputed at runtime.
/// </summary>
public enum CombatRole
{
    Unknown = 0,
    Archer = 1,
    Crossbow = 2,
    ShieldInfantry = 3,
    TwoHanded = 4,
    Polearm = 5,
    Cavalry = 6,
    HorseArcher = 7,
    Skirmisher = 8,
    OneHanded = 9,
    Slinger = 10,
}
