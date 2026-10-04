using System.Collections.Concurrent;
using System.Collections.Generic;
using TAOM.Core.Collections;
using TAOM.Features.RaceAbilities.Domain;

namespace TAOM.Features.RaceAbilities;

public enum RaceAbilityPhase
{
    Ready,
    Active,
    Spent,
}

// One soldier's ability at one moment. Immutable: the store replaces the whole state on every change, so
// a reader on another thread sees the old state or the new one, never half of each.
public sealed class RaceAbilityState
{
    public RaceAbilityState(RaceAbilityProfile profile, RaceAbilityPhase phase, float activatedAt, float phaseEndsAt,
        RaceAbilityEffects activeEffects, RaceAbilityEffects spentEffects)
    {
        Profile = profile;
        Phase = phase;
        ActivatedAt = activatedAt;
        PhaseEndsAt = phaseEndsAt;
        ActiveEffects = activeEffects;
        SpentEffects = spentEffects;
    }

    public RaceAbilityProfile Profile { get; }

    public RaceAbilityPhase Phase { get; }

    // Mission time of the activation; the cooldown counts from here.
    public float ActivatedAt { get; }

    public float PhaseEndsAt { get; }

    // Already scaled for this soldier's tier.
    public RaceAbilityEffects ActiveEffects { get; }

    public RaceAbilityEffects SpentEffects { get; }

    public RaceAbilityEffects? CurrentEffects => Phase switch
    {
        RaceAbilityPhase.Active => ActiveEffects,
        RaceAbilityPhase.Spent => SpentEffects,
        _ => null,
    };

    internal RaceAbilityState With(RaceAbilityPhase phase, float phaseEndsAt) =>
        new RaceAbilityState(Profile, phase, ActivatedAt, phaseEndsAt, ActiveEffects, SpentEffects);
}

// One phase change made by Advance.
public readonly struct RaceAbilityTransition<TKey>
{
    public RaceAbilityTransition(TKey key, RaceAbilityState before, RaceAbilityState after)
    {
        Key = key;
        Before = before;
        After = after;
    }

    public TKey Key { get; }

    public RaceAbilityState Before { get; }

    public RaceAbilityState After { get; }
}

// Live ability state per soldier for one mission. Written only from the mission's main thread (the tree
// tick and the mission logic, with engine callbacks deferred there); read from any thread by the stat and
// damage models, which the engine calls off the main thread too (#595). Keys are compared by identity:
// a new agent in a recycled engine slot is a new key (#592), and the mission logic evicts each agent when
// the engine removes it.
public sealed class RaceAbilityStore<TKey> where TKey : class
{
    private readonly ConcurrentDictionary<TKey, RaceAbilityState> _states = new ConcurrentDictionary<TKey, RaceAbilityState>(ReferenceIdentity.Instance);
    private readonly ConcurrentDictionary<TKey, float> _lastKillAt = new ConcurrentDictionary<TKey, float>(ReferenceIdentity.Instance);

    public int Count => _states.Count;

    public RaceAbilityState? Get(TKey key) => _states.TryGetValue(key, out var state) ? state : null;

    public float? LastFiredAt(TKey key) => Get(key)?.ActivatedAt;

    public float? LastKillAt(TKey key) => _lastKillAt.TryGetValue(key, out var at) ? at : (float?)null;

    public IEnumerable<KeyValuePair<TKey, RaceAbilityState>> Entries => _states;

    public void Activate(TKey key, RaceAbilityProfile profile, float now, RaceAbilityEffects activeEffects, RaceAbilityEffects spentEffects) =>
        _states[key] = new RaceAbilityState(profile, RaceAbilityPhase.Active, now, now + profile.DurationSeconds, activeEffects, spentEffects);

    // Moves every soldier whose phase has ended to the next one and lists each change, so the caller can
    // refresh stats and settle the end of an active window. A stall past both ends lands in Ready in one step.
    public void Advance(float now, List<RaceAbilityTransition<TKey>> transitions)
    {
        foreach (var pair in _states)
        {
            var state = pair.Value;
            if (state.Phase == RaceAbilityPhase.Ready || !(now >= state.PhaseEndsAt))
                continue;
            RaceAbilityState next;
            if (state.Phase == RaceAbilityPhase.Active && state.Profile.SpentSeconds > 0f
                && now < state.PhaseEndsAt + state.Profile.SpentSeconds)
                next = state.With(RaceAbilityPhase.Spent, state.PhaseEndsAt + state.Profile.SpentSeconds);
            else
                next = state.With(RaceAbilityPhase.Ready, state.PhaseEndsAt);
            _states[pair.Key] = next;
            transitions.Add(new RaceAbilityTransition<TKey>(pair.Key, state, next));
        }
    }

    public void ExtendActive(TKey key, float phaseEndsAt)
    {
        var state = Get(key);
        if (state != null && state.Phase == RaceAbilityPhase.Active)
            _states[key] = state.With(RaceAbilityPhase.Active, phaseEndsAt);
    }

    // How many soldiers of each ability are live now, active and spent, in ability-id order (the console's
    // "live now" line). A soldier back to Ready is not live.
    public List<(string AbilityId, int Active, int Spent)> LiveCounts()
    {
        var counts = new SortedDictionary<string, (int Active, int Spent)>(System.StringComparer.Ordinal);
        foreach (var pair in _states)
        {
            var state = pair.Value;
            if (state.Phase == RaceAbilityPhase.Ready)
                continue;
            counts.TryGetValue(state.Profile.AbilityId, out var count);
            counts[state.Profile.AbilityId] = state.Phase == RaceAbilityPhase.Active
                ? (count.Active + 1, count.Spent)
                : (count.Active, count.Spent + 1);
        }
        var live = new List<(string AbilityId, int Active, int Spent)>(counts.Count);
        foreach (var pair in counts)
            live.Add((pair.Key, pair.Value.Active, pair.Value.Spent));
        return live;
    }

    public void RecordKill(TKey key, float now) => _lastKillAt[key] = now;

    public void Remove(TKey key)
    {
        _states.TryRemove(key, out _);
        _lastKillAt.TryRemove(key, out _);
    }

    public void Clear()
    {
        _states.Clear();
        _lastKillAt.Clear();
    }
}
