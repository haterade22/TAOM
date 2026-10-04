using System.Collections.Generic;
using TAOM.Core.Collections;

namespace TAOM.Features.RaceAbilities;

// One fear-aura pulse: when several auras reach the same enemy, only the strongest drains him, so a rallied
// pack cannot stack its auras into an instant rout. Remembers whose aura it was, for the log. Main thread
// only; cleared after every pulse. Keys are compared by identity (#592).
public sealed class RaceAbilityAuraLedger<TKey> where TKey : class
{
    private readonly Dictionary<TKey, (float drain, string abilityId)> _strongest =
        new Dictionary<TKey, (float drain, string abilityId)>(ReferenceIdentity.Instance);

    public int Count => _strongest.Count;

    public IEnumerable<KeyValuePair<TKey, (float drain, string abilityId)>> Entries => _strongest;

    public void Offer(TKey victim, float drain, string abilityId)
    {
        if (!(drain > 0f))
            return;
        if (!_strongest.TryGetValue(victim, out var current) || drain > current.drain)
            _strongest[victim] = (drain, abilityId);
    }

    public void Clear() => _strongest.Clear();
}
