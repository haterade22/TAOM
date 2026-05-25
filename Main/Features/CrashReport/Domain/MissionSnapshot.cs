using System.Collections.Generic;

namespace TAOM.Features.CrashReport.Domain;

// Populated only when Mission.Current != null. Every field is best-effort —
// missions can be in half-initialised state at crash time.
public sealed record MissionSnapshot(
    string? SceneName,
    string? MissionMode,
    float? TimeOfDay,
    string? Weather,
    IReadOnlyList<TeamSnapshot> Teams,
    IReadOnlyList<FormationSnapshot> PlayerFormations,
    PlayerAgentSnapshot? PlayerAgent);

public sealed record TeamSnapshot(
    int Index,
    string Side,
    int AgentsAlive,
    int AgentsTotal,
    bool IsPlayerTeam);

public sealed record FormationSnapshot(
    string Class,
    int CountAlive,
    int CountTotal,
    string? MovementOrder,
    string? FacingOrder,
    string? ArrangementOrder);

public sealed record PlayerAgentSnapshot(
    float Health,
    float MaxHealth,
    float PositionX,
    float PositionY,
    float PositionZ,
    bool IsMounted,
    string? WieldedItemId);
