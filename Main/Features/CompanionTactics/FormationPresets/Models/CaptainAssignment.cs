namespace TAOM.Features.CompanionTactics.FormationPresets.Models;

/// <summary>
/// One planned Auto-Assign captaincy: the hero at <see cref="HeroIndex"/> in the candidate
/// list leads the open formation at <see cref="SlotIndex"/> in the slot list.
/// </summary>
public sealed record CaptainAssignment(int HeroIndex, int SlotIndex);
