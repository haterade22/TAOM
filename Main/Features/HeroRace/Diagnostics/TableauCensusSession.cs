using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace TAOM.Features.HeroRace.Diagnostics;

/// <summary>
/// Session-scoped state for the #389 render census: per-tableau identity keys plus the shared
/// <see cref="TableauResidencyTracker"/>. The Harmony patches hold no state of their own, so they
/// stay thin entry points (ADR-002) and this logic stays unit-testable.
///
/// Keyed on <c>object</c> rather than <c>CharacterTableau</c> deliberately — nothing here needs the
/// engine type, which keeps the class free of a TaleWorlds dependency and lets the tests drive it
/// with plain objects.
///
/// THREE THINGS THIS EXISTS TO GET RIGHT, all found by deep review 2026-08-06:
///
/// 1. **No per-frame allocation.** The postfix runs once per rendered frame per live tableau. An
///    earlier version rebuilt the key string on every tick, which at 144 Hz across several tableaus
///    is a steady stream of garbage for a diagnostic that reports once per character. The key is now
///    built once per (tableau, character) pair and reused; the steady-state path is a
///    <see cref="ConditionalWeakTable{TKey,TValue}"/> lookup plus one ordinal string compare.
/// 2. **No leaked references, and no leaked entries.** A <see cref="ConditionalWeakTable{TKey,TValue}"/>
///    holds its keys weakly, so a finalised tableau's entry is collected with it — a plain
///    <c>Dictionary</c> keyed on the tableau would pin engine objects for the life of the process.
/// 3. **No identity collisions.** <c>RuntimeHelpers.GetHashCode</c> is reusable after GC, so a
///    recycled hash could match a stale "already reported" entry and silently suppress a genuinely
///    new tableau's census. Each tableau instead gets a monotonic serial that is never reissued.
/// </summary>
public static class TableauCensusSession
{
    private const string NoCharacterId = "<no-char-id>";

    private sealed class KeyState
    {
        public string Serial;
        public string CharacterId;
        public string Key;
    }

    // Non-capturing, so Roslyn caches it in a static field — no delegate allocation per lookup.
    private static readonly ConditionalWeakTable<object, KeyState>.CreateValueCallback NewState =
        _ => new KeyState { Serial = Interlocked.Increment(ref _nextSerial).ToString("X8") };

    private static readonly ConditionalWeakTable<object, KeyState> Keys = new();
    private static readonly TableauResidencyTracker Tracker = new();
    private static long _nextSerial;

    /// <summary>
    /// Stable key for one tableau showing one character, allocated at most once per
    /// (tableau, character) pair. The character id is part of the key because a single tableau shows
    /// many troops as the user walks a troop tree, and each deserves its own census.
    /// </summary>
    public static string KeyFor(object tableau, string characterId)
    {
        if (tableau == null) return null;

        string id = string.IsNullOrWhiteSpace(characterId) ? NoCharacterId : characterId;
        var state = Keys.GetValue(tableau, NewState);

        // Ordinal compare, no allocation. Only a genuine character change rebuilds the string.
        if (!string.Equals(state.CharacterId, id, StringComparison.Ordinal))
        {
            state.CharacterId = id;
            state.Key = state.Serial + "/" + id;
        }

        return state.Key;
    }

    /// <summary>Records one tick. See <see cref="TableauResidencyTracker.Observe"/>.</summary>
    public static TableauResidencyResult Observe(string key, int agentVisualLoadingCounter, int mountVisualLoadingCounter)
        => Tracker.Observe(key, agentVisualLoadingCounter, mountVisualLoadingCounter);

    /// <summary>
    /// Drops a tableau's census state so it can report again — on a race change (which re-arms the
    /// loading counters) and on teardown (so a closed screen does not consume a tracked slot for the
    /// rest of the session).
    /// </summary>
    public static void Forget(object tableau, string characterId)
    {
        string key = KeyFor(tableau, characterId);
        if (key != null) Tracker.Forget(key);
    }

    /// <summary>Test seam: how many characters the shared tracker currently holds.</summary>
    public static int TrackedCount => Tracker.TrackedCount;
}
