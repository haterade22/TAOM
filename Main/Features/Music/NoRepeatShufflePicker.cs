using System;
using System.Collections.Generic;

namespace TAOM.Features.Music;

public sealed class NoRepeatShufflePicker
{
    private sealed class ShuffleBagState
    {
        public int Signature { get; set; }

        public List<MusicTrackDefinition> Bag { get; } = new List<MusicTrackDefinition>();

        public Queue<string> History { get; } = new Queue<string>();

        public string LastPick { get; set; }

        public int NextIndex { get; set; }
    }

    private readonly Dictionary<string, ShuffleBagState> _states =
        new Dictionary<string, ShuffleBagState>(StringComparer.OrdinalIgnoreCase);

    private readonly Random _random;

    public NoRepeatShufflePicker()
        : this(new Random())
    {
    }

    internal NoRepeatShufflePicker(Random random)
    {
        _random = random ?? new Random();
    }

    public MusicTrackDefinition Pick(
        string key,
        IReadOnlyList<MusicTrackDefinition> tracks,
        bool useNoRepeatShuffle,
        int historySize)
    {
        var sanitized = Sanitize(tracks);
        if (sanitized.Count == 0)
            return null;

        if (sanitized.Count == 1)
            return sanitized[0];

        if (string.IsNullOrWhiteSpace(key))
            key = "__default__";

        historySize = Clamp(historySize, 0, 64);

        if (!_states.TryGetValue(key, out var state))
        {
            state = new ShuffleBagState();
            _states[key] = state;
        }

        var signature = ComputeSignatureOrderIndependent(sanitized);
        if (state.Signature != signature)
        {
            state.Signature = signature;
            state.Bag.Clear();
            state.History.Clear();
            state.LastPick = null;
            state.NextIndex = 0;
        }

        return useNoRepeatShuffle
            ? PickShuffleNoRepeat(state, sanitized, historySize)
            : PickDeterministicRoundRobin(state, sanitized);
    }

    public void Reset(string key = null)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            _states.Clear();
            return;
        }

        _states.Remove(key);
    }

    private MusicTrackDefinition PickDeterministicRoundRobin(ShuffleBagState state, IReadOnlyList<MusicTrackDefinition> tracks)
    {
        var ordered = new List<MusicTrackDefinition>(tracks);
        ordered.Sort((a, b) => string.Compare(a.EventName, b.EventName, StringComparison.OrdinalIgnoreCase));

        var index = state.NextIndex;
        if (index < 0)
            index = 0;

        index %= ordered.Count;
        var pick = ordered[index];
        state.NextIndex = (index + 1) % ordered.Count;
        return pick;
    }

    private MusicTrackDefinition PickShuffleNoRepeat(
        ShuffleBagState state,
        IReadOnlyList<MusicTrackDefinition> tracks,
        int historySize)
    {
        if (state.Bag.Count == 0)
        {
            state.Bag.AddRange(tracks);
            Shuffle(state.Bag);

            if (historySize > 0 && state.History.Count > 0)
                SwapFrontOutOfHistoryIfPossible(state);

            if (historySize == 0 && state.Bag.Count > 1 && IsSameTrack(state.Bag[0], state.LastPick))
                Swap(state.Bag, 0, 1);
        }

        var pick = state.Bag[0];
        if (historySize > 0 && IsInHistory(state, pick))
        {
            for (var i = 1; i < state.Bag.Count; i++)
            {
                if (!IsInHistory(state, state.Bag[i]))
                {
                    Swap(state.Bag, 0, i);
                    pick = state.Bag[0];
                    break;
                }
            }
        }

        state.Bag.RemoveAt(0);
        state.LastPick = pick.EventName;

        if (historySize > 0)
        {
            state.History.Enqueue(pick.EventName);
            while (state.History.Count > historySize)
                state.History.Dequeue();
        }
        else if (state.History.Count > 0)
        {
            state.History.Clear();
        }

        return pick;
    }

    private void Shuffle(List<MusicTrackDefinition> tracks)
    {
        for (var i = tracks.Count - 1; i > 0; i--)
        {
            var j = _random.Next(i + 1);
            Swap(tracks, i, j);
        }
    }

    private static void SwapFrontOutOfHistoryIfPossible(ShuffleBagState state)
    {
        if (state.Bag.Count <= 1 || !IsInHistory(state, state.Bag[0]))
            return;

        for (var i = 1; i < state.Bag.Count; i++)
        {
            if (!IsInHistory(state, state.Bag[i]))
            {
                Swap(state.Bag, 0, i);
                return;
            }
        }
    }

    private static bool IsInHistory(ShuffleBagState state, MusicTrackDefinition track)
    {
        if (state.History.Count == 0 || track == null)
            return false;

        foreach (var item in state.History)
        {
            if (string.Equals(item, track.EventName, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool IsSameTrack(MusicTrackDefinition track, string eventName)
    {
        return track != null
            && !string.IsNullOrEmpty(eventName)
            && string.Equals(track.EventName, eventName, StringComparison.OrdinalIgnoreCase);
    }

    private static List<MusicTrackDefinition> Sanitize(IReadOnlyList<MusicTrackDefinition> tracks)
    {
        var sanitized = new List<MusicTrackDefinition>();
        if (tracks == null)
            return sanitized;

        for (var i = 0; i < tracks.Count; i++)
        {
            var track = tracks[i];
            if (track != null && !string.IsNullOrWhiteSpace(track.EventName))
                sanitized.Add(track);
        }

        return sanitized;
    }

    private static int ComputeSignatureOrderIndependent(IReadOnlyList<MusicTrackDefinition> tracks)
    {
        var names = new List<string>(tracks.Count);
        for (var i = 0; i < tracks.Count; i++)
            names.Add(tracks[i].EventName.Trim());

        names.Sort(StringComparer.OrdinalIgnoreCase);

        unchecked
        {
            var hash = 17;
            for (var i = 0; i < names.Count; i++)
                hash = (hash * 31) ^ StringComparer.OrdinalIgnoreCase.GetHashCode(names[i]);

            return (hash * 31) ^ names.Count;
        }
    }

    private static void Swap(List<MusicTrackDefinition> tracks, int left, int right)
    {
        var value = tracks[left];
        tracks[left] = tracks[right];
        tracks[right] = value;
    }

    private static int Clamp(int value, int min, int max)
    {
        if (value < min)
            return min;
        return value > max ? max : value;
    }
}
