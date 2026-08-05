using System.Collections.Generic;

namespace TAOM.Features.CareerSystem.Abilities;

public static class CareerAbilityBuffTracker
{
    private static readonly Dictionary<string, ActiveBuffs> _buffs = new Dictionary<string, ActiveBuffs>();
    private static readonly Dictionary<int, ActiveBuffs> _allyBuffs = new Dictionary<int, ActiveBuffs>();

    public static void SetBuff(string heroId, ActiveBuffs buffs) => _buffs[heroId] = buffs;
    public static ActiveBuffs GetBuff(string heroId) => _buffs.TryGetValue(heroId, out var b) ? b : null;
    public static void ClearBuff(string heroId) => _buffs.Remove(heroId);

    public static void SetAllyBuff(int agentIndex, ActiveBuffs buffs) => _allyBuffs[agentIndex] = buffs;
    public static ActiveBuffs GetAllyBuff(int agentIndex) => _allyBuffs.TryGetValue(agentIndex, out var b) ? b : null;
    public static void ClearAllyBuff(int agentIndex) => _allyBuffs.Remove(agentIndex);
    public static void ClearAllAllyBuffs() => _allyBuffs.Clear();

    // Issue #377 — contribution-counted lifecycle. Accumulate-on-apply / subtract-on-expire
    // (ActiveBuffsAlgebra) is field-exact, but a subtracted-to-zero entry used to stay in the
    // dictionary forever, so GetBuff was non-null from first activation to mission end.
    // Overlapping activations sum float fields, and (b1+b2)-b1-b2 is not reliably 0f, so
    // "all fields zero" cannot detect the last expiry — an integer contribution count can.
    // The entry retires exactly when its last contribution's restore fires.
    public static void AddContribution(string heroId, ActiveBuffs deltas)
    {
        if (!_buffs.TryGetValue(heroId, out var buff))
            _buffs[heroId] = buff = new ActiveBuffs();
        ActiveBuffsAlgebra.Accumulate(buff, deltas);
        buff.ActiveContributions++;
    }

    public static void RemoveContribution(string heroId, ActiveBuffs deltas)
    {
        var buff = GetBuff(heroId);
        if (buff == null) return; // entry already cleared (main-agent death) — late restore no-ops

        ActiveBuffsAlgebra.Subtract(buff, deltas);
        buff.ActiveContributions--;
        if (buff.ActiveContributions <= 0)
            _buffs.Remove(heroId);
    }

    public static void AddAllyContribution(int agentIndex, ActiveBuffs deltas)
    {
        if (!_allyBuffs.TryGetValue(agentIndex, out var buff))
            _allyBuffs[agentIndex] = buff = new ActiveBuffs();
        ActiveBuffsAlgebra.Accumulate(buff, deltas);
        buff.ActiveContributions++;
    }

    public static void RemoveAllyContribution(int agentIndex, ActiveBuffs deltas)
    {
        var buff = GetAllyBuff(agentIndex);
        if (buff == null) return; // agent deleted mid-window — late restore no-ops

        ActiveBuffsAlgebra.Subtract(buff, deltas);
        buff.ActiveContributions--;
        if (buff.ActiveContributions <= 0)
            _allyBuffs.Remove(agentIndex);
    }

    // Deep-review 2026-08-05 — hero-death cleanup must refresh the agents whose cached
    // AgentDrivenProperties still carry the buff (UpdateAgentProperties is event-triggered,
    // not per-tick). Snapshot (List copy) so the caller can clear first, refresh after;
    // also cheap double-lookup avoidance in the Add paths (perf review LOW).
    public static IReadOnlyList<int> GetBuffedAllyIndices() => new List<int>(_allyBuffs.Keys);

    public static void ClearAll()
    {
        _buffs.Clear();
        _allyBuffs.Clear();
    }
}

public class ActiveBuffs
{
    public float SpeedMultiplier { get; set; } = 0f;
    public float CombatSpeedMultiplier { get; set; } = 0f;
    public float DamageBonus { get; set; } = 0f;
    public float ArmorReduction { get; set; } = 0f;
    public float DrawSpeedBonus { get; set; } = 0f;
    public float MountSpeedBonus { get; set; } = 0f;
    public float ChargeDamageBonus { get; set; } = 0f;
    public float DamageReductionBonus { get; set; } = 0f;

    // Number of live activations composing this entry — owned by the tracker's
    // Add/RemoveContribution pair; the entry is removed when this reaches zero.
    public int ActiveContributions { get; set; }
}
