using System.Collections.Generic;

namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>
/// One commander: a human or an AI on a side, with its own gold. Lanes are the lane indices the slot orders troops in
/// and spends on; free-wave units of those lanes belong to the slot. AiProfileId is read only when IsHuman is false.
/// </summary>
internal sealed record CommanderSlot(int Id, MatchSide Side, bool IsHuman, string? AiProfileId, IReadOnlyList<int> Lanes);
