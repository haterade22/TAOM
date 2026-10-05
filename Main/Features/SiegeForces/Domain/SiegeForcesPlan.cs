using System;
using System.Collections.Generic;

namespace TAOM.Features.SiegeForces.Domain;

/// <summary>
/// How many of each character every party in scope keeps for the wall battle: the picked roster mapped back to the
/// parties it came from. A party that is not in the plan is not the player's to choose for, so it keeps everyone.
/// </summary>
public sealed class SiegeForcesPlan
{
    private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> _keepByParty;

    /// <param name="keepByParty">Party id to (character id to the number of that character the party keeps).</param>
    public SiegeForcesPlan(IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> keepByParty)
    {
        _keepByParty = keepByParty ?? throw new ArgumentNullException(nameof(keepByParty));
    }

    /// <summary>The plan's keep counts for a party, or false when the party keeps everyone.</summary>
    public bool TryGetKeep(string partyId, out IReadOnlyDictionary<string, int> keep)
    {
        if (partyId != null && _keepByParty.TryGetValue(partyId, out var found))
        {
            keep = found;
            return true;
        }

        keep = null!;
        return false;
    }
}

/// <summary>The four numbers <c>DefaultBattleMissionAgentSpawnLogic.InitWithSinglePhase</c> is handed.</summary>
public readonly record struct SpawnTotals(int DefenderTotal, int AttackerTotal, int DefenderInitial, int AttackerInitial);
