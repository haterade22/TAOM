namespace TAOM.Features.CultureConversion.GarrisonSwap.Domain;

/// <summary>
/// TAOM's own battlefield-role enum, mapped at the adapter boundary from the engine's
/// <c>FormationClass</c> (itself derived from the troop XML's <c>default_group</c> attribute) so the
/// mapper stays free of TaleWorlds types (ADR-007).
///
/// Only the four roles TAOM troop data actually uses are modelled — measured 2026-09-20 across the
/// 16 <c>troops_*.xml</c> files: Infantry 491, Ranged 201, Cavalry 143, HorseArcher 23, and nothing
/// else. Anything the engine reports outside that set (Bodyguard, NumberOfDefaultFormations, an
/// unset formation) arrives as <see cref="Unknown"/> and is treated as Infantry for matching, which
/// is the safe default: infantry is the one role every culture in the game has at some tier.
/// </summary>
public enum TroopRole
{
    Infantry = 0,
    Ranged = 1,
    Cavalry = 2,
    HorseArcher = 3,
    Unknown = 4,
}
