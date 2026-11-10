using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Features.HeroDefence.Domain;

/// <summary>
/// Builds every wave from the seed and the wave number alone, so the preview and the wave cannot differ and a run can replay.
/// The rotation depends on the waves before it, so the plan works it out in wave order and keeps it.
/// </summary>
internal sealed partial class CreepWavePlan
{
    private const int CultureStream = 101;
    private const int OrderStream = 102;
    private const int TroopPickStream = 103;
    private const int ShuffleStream = 104;
    private const int PowerStream = 105;

    private readonly WaveCatalog _c;
    private readonly ulong _seed;
    private readonly List<CultureDef> _rotation = new();
    private readonly HashSet<string> _drawn = new();

    public CreepWavePlan(WaveCatalog catalog, ulong seed)
    {
        _c = catalog;
        _seed = seed;
    }

    public WaveBrief Brief(int wave)
    {
        var culture = CultureOf(wave);
        var band = _c.BandOf(wave);
        var packs = BuildPacks(culture, band, wave);
        var leaderFactor = AddLeader(packs, culture, band, wave);
        var extra = wave - 1;

        return new WaveBrief(wave, packs)
        {
            CultureId = culture.Id,
            LevelLow = band.Low,
            LevelHigh = band.High,
            HealthFactor = _c.Scale.HealthFactor(extra),
            DamageFactor = _c.Scale.DamageFactor(extra),
            ArmourPercent = Math.Min(_c.ArmourPercentMax, _c.ArmourPercentPerWave * extra),
            SwingPercent = Math.Min(_c.SwingPercentMax, _c.SwingPercentPerWave * extra),
            PowerIds = Powers(culture, wave),
            LeaderHealthFactor = leaderFactor,
        };
    }

    private CultureDef CultureOf(int wave)
    {
        while (_rotation.Count < wave)
            _rotation.Add(Draw(_rotation.Count + 1));

        return _rotation[wave - 1];
    }

    /// <summary>A pinned culture that has not entered yet would field no troops, so the bag draws instead.</summary>
    private CultureDef Draw(int wave)
    {
        var pinned = wave <= _c.Opening.Count ? _c.Cultures.FirstOrDefault(c => c.Id == _c.Opening[wave - 1]) : null;
        var drawn = pinned != null && _c.HasEntered(pinned, wave) ? pinned : Pick(wave);
        _drawn.Add(drawn.Id);
        return drawn;
    }

    /// <summary>
    /// A bag without replacement. An empty bag refills, and a catalog with one culture must repeat it. The bag also refills
    /// early when it holds only the last wave's signature while another signature has entered, so a signature never repeats
    /// when the catalog can avoid it.
    /// </summary>
    private CultureDef Pick(int wave)
    {
        var last = _rotation.LastOrDefault();
        var candidates = Candidates(wave, last, fromBag: true);
        if (!candidates.Any(c => Varies(c, last)))
        {
            var refill = Candidates(wave, last, fromBag: false);
            if (candidates.Count == 0 || refill.Any(c => Varies(c, last)))
            {
                _drawn.Clear();
                candidates = refill;
            }
        }

        if (candidates.Count == 0)
            return last ?? _c.Cultures[0];

        var varied = candidates.Where(c => Varies(c, last)).ToList();
        var pool = varied.Count > 0 ? varied : candidates;
        return pool[RunRandom.For(_seed, CultureStream, wave, 0).Below(pool.Count)];
    }

    private List<CultureDef> Candidates(int wave, CultureDef? last, bool fromBag) =>
        _c.Cultures.Where(c => (!fromBag || !_drawn.Contains(c.Id)) && c.Id != last?.Id && _c.HasEntered(c, wave)).ToList();

    private static bool Varies(CultureDef culture, CultureDef? last) => culture.Signature != last?.Signature;

    private IReadOnlyList<string> Powers(CultureDef culture, int wave)
    {
        var step = _c.PowerSchedule.LastOrDefault(s => s.FromWave <= wave);
        if (step == null)
            return Array.Empty<string>();

        var rest = _c.PowerPool.Where(power => power != culture.FirstPower).OrderBy(power => power, StringComparer.Ordinal).ToArray();
        Shuffle(rest, RunRandom.For(_seed, PowerStream, wave, 0));
        return new[] { culture.FirstPower }.Concat(rest).Take(step.Count).ToList();
    }

    /// <summary>
    /// The leader replaces one soldier, so the wave count does not change. In a wave with no foot pack the leader takes one creep
    /// from the largest pack and walks in a pack of one.
    /// </summary>
    private float AddLeader(List<CreepPack> packs, CultureDef culture, LevelBand band, int wave)
    {
        if (_c.LeaderEveryWaves <= 0 || wave % _c.LeaderEveryWaves != 0)
            return 1f;

        var rank = Math.Min(wave / _c.LeaderEveryWaves, _c.LeaderHealth.Count) - 1;
        if (rank >= culture.Leaders.Count)
            return 1f;

        var leader = new CreepSpec(culture.Leaders[rank], band.High, (CreepRank)(rank + 1), false);
        var foot = packs.Select((pack, index) => (pack, index)).Where(x => x.pack.Class == CreepClass.Infantry).ToList();
        if (foot.Count == 0)
        {
            RemoveOneCreep(packs);
            packs.Insert(packs.Count / 2, new CreepPack(CreepClass.Infantry, new[] { leader }));
            return _c.LeaderHealth[rank];
        }

        var (holder, position) = foot[foot.Count / 2];
        var creeps = holder.Creeps.ToList();
        creeps[creeps.Count / 2] = leader;
        packs[position] = holder with { Creeps = creeps };
        return _c.LeaderHealth[rank];
    }

    /// <summary>Removes the last creep of the first largest pack, and the pack when it is then empty.</summary>
    private static void RemoveOneCreep(List<CreepPack> packs)
    {
        if (packs.Count == 0)
            return;

        var largest = packs.Max(pack => pack.Creeps.Count);
        var index = packs.FindIndex(pack => pack.Creeps.Count == largest);
        var rest = packs[index].Creeps.Take(largest - 1).ToList();
        if (rest.Count == 0)
            packs.RemoveAt(index);
        else
            packs[index] = packs[index] with { Creeps = rest };
    }

    private static void Shuffle<T>(IList<T> items, RunRandom random)
    {
        for (var i = items.Count - 1; i > 0; i--)
        {
            var j = random.Below(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }
}
