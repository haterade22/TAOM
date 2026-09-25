namespace TAOM.Features.CompanionTactics.FormationPresets.Models;

/// <summary>Outcome of one Auto-Assign press, for the overlay's message.</summary>
public sealed record AutoAssignResult(AutoAssignStatus Status, int AssignedCount);
