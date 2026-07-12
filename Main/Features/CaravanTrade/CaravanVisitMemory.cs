using System.Collections.Generic;
using TAOM.Core.Validation;

namespace TAOM.Features.CaravanTrade;

/// <summary>
/// Pure implementation of <see cref="ICaravanVisitMemory"/>. Per caravan, a bounded ring of the last
/// <see cref="MemoryDepth"/> distinct-in-a-row entered towns (newest last). The penalty for a
/// candidate town scales with how recently it was visited (rank 0 = most recent), using the
/// configured <see cref="ICaravanTradeSettingsProvider.AntiShuttlePenalty"/> as the max strength, and
/// is floored at <see cref="MinRecencyFactor"/> so it never turns a positive score into a rejection.
///
/// Depth 4 kills 2-, 3-, and 4-town orbits (the previous town lands at rank 1, penalized strongly)
/// while leaving the rest of the map unpenalized. It is a <c>const</c>, not config — a tiny tuning
/// knob is not worth a new validated surface (simplicity-criterion / YAGNI); the penalty magnitude is
/// tunable via the existing <c>antiShuttlePenalty</c>.
/// </summary>
public class CaravanVisitMemory : ICaravanVisitMemory
{
    private const int MemoryDepth = 4;

    /// <summary>Strictly-positive floor so a penalized lone candidate still outscores a non-candidate (no stranding).</summary>
    private const float MinRecencyFactor = 0.05f;

    private readonly ICaravanTradeSettingsProvider _settings;
    private readonly Dictionary<string, List<string>> _visits = new();

    public CaravanVisitMemory(ICaravanTradeSettingsProvider settings)
    {
        _settings = settings;
    }

    public void RecordVisit(string caravanId, string townId)
    {
        if (string.IsNullOrEmpty(caravanId) || string.IsNullOrEmpty(townId))
            return;

        if (!_visits.TryGetValue(caravanId, out var ring))
        {
            ring = new List<string>(MemoryDepth);
            _visits[caravanId] = ring;
        }

        // Collapse consecutive re-entries of the same town (re-entering the current town, or a
        // possible double-fire) so they don't consume the whole ring with one id.
        if (ring.Count > 0 && ring[ring.Count - 1] == townId)
            return;

        ring.Add(townId);
        while (ring.Count > MemoryDepth)
            ring.RemoveAt(0);
    }

    public float GetRecencyPenaltyFactor(string caravanId, string townId)
    {
        if (string.IsNullOrEmpty(caravanId) || string.IsNullOrEmpty(townId))
            return 1f;

        if (!_visits.TryGetValue(caravanId, out var ring))
            return 1f;

        // Rank 0 = newest (last element). Use the MOST RECENT occurrence of a revisited town.
        int rank = -1;
        for (int i = ring.Count - 1; i >= 0; i--)
        {
            if (ring[i] == townId)
            {
                rank = ring.Count - 1 - i;
                break;
            }
        }

        if (rank < 0)
            return 1f; // not recently visited

        float strength = _settings.AntiShuttlePenalty;
        if (!FiniteFloatValidator.IsFiniteInRange(strength, 0f, 1f))
            return 1f; // bad strength -> no penalty (never emit a corrupted factor)

        // Linear decay by rank: rank 0 -> weight 1, older -> smaller weight.
        float weight = (float)(MemoryDepth - rank) / MemoryDepth;
        float factor = 1f - strength * weight;

        return factor < MinRecencyFactor ? MinRecencyFactor : factor;
    }

    public void Clear(string caravanId)
    {
        if (!string.IsNullOrEmpty(caravanId))
            _visits.Remove(caravanId);
    }

    public void ClearAll() => _visits.Clear();
}
