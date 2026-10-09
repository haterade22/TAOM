namespace TAOM.Features.CompanionTactics.FormationPresets.Models;

/// <summary>
/// Outcome of one preset Load, for the overlay's message. The three counts are the parts that match the preset after
/// the load (already in place or placed now); <c>Skipped</c> counts the saved parts that could not be applied.
/// All zero when nothing was applied.
/// </summary>
public sealed record PresetApplyResult(int ClassesSet, int CaptainsPlaced, int TroopsPlaced, int Skipped);
