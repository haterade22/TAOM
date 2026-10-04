using System.Collections.Generic;

namespace TAOM.Features.RaceAbilities;

public readonly struct RaceAbilityWave
{
    public RaceAbilityWave(string abilityId, bool playerSide, int soldiers)
    {
        AbilityId = abilityId;
        PlayerSide = playerSide;
        Soldiers = soldiers;
    }

    public string AbilityId { get; }

    public bool PlayerSide { get; }

    public int Soldiers { get; }
}

// Groups activations into two-second windows per side and ability, so the message log gets one line for
// a wave of five or more and nothing for a straggler. Main thread only.
public sealed class RaceAbilityWaveCounter
{
    public const int Threshold = 5;
    public const float WindowSeconds = 2f;

    private readonly Dictionary<(string ability, bool playerSide), (float start, int soldiers)> _open =
        new Dictionary<(string ability, bool playerSide), (float start, int soldiers)>();
    private readonly List<(string ability, bool playerSide)> _closed = new List<(string ability, bool playerSide)>();

    public void Record(string abilityId, bool playerSide, float now, int soldiers)
    {
        var key = (abilityId, playerSide);
        _open[key] = _open.TryGetValue(key, out var window) ? (window.start, window.soldiers + soldiers) : (now, soldiers);
    }

    // Closes every window older than WindowSeconds and lists the ones big enough to announce.
    public void Flush(float now, List<RaceAbilityWave> due)
    {
        _closed.Clear();
        foreach (var pair in _open)
        {
            if (!(now >= pair.Value.start + WindowSeconds))
                continue;
            _closed.Add(pair.Key);
            if (pair.Value.soldiers >= Threshold)
                due.Add(new RaceAbilityWave(pair.Key.ability, pair.Key.playerSide, pair.Value.soldiers));
        }
        foreach (var key in _closed)
            _open.Remove(key);
    }

    public void Clear() => _open.Clear();
}
