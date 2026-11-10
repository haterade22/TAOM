using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Features.HeroDefence.Domain;

internal sealed partial class CreepWavePlan
{
    private static readonly CreepClass[] Classes = { CreepClass.Infantry, CreepClass.Ranged, CreepClass.Cavalry, CreepClass.HorseArcher };

    private List<CreepPack> BuildPacks(CultureDef culture, LevelBand band, int wave)
    {
        var rows = Classes.ToDictionary(cls => cls, cls => RowsFor(culture, cls, band));
        var counts = ClassCounts(culture, rows, TotalOf(culture, wave));
        var gap = InfantryGap(rows[CreepClass.Infantry], band);
        var packs = new List<CreepPack>();
        foreach (var cls in ArrivalOrder(culture, wave))
        {
            var creeps = CreepsOf(cls, rows[cls], counts[cls], wave);
            var levels = cls == CreepClass.Infantry ? gap : 0;
            for (var start = 0; start < creeps.Count; start += _c.PackSize)
            {
                packs.Add(new CreepPack(cls, creeps.Skip(start).Take(_c.PackSize).ToList())
                {
                    HealthBonus = levels * _c.GapHealthPerLevel,
                    DamageBonus = levels * _c.GapDamagePerLevel,
                });
            }
        }

        return packs;
    }

    /// <summary>
    /// Levels from the infantry rows in use up to the band's high level, when those rows are below the band (see <see cref="RowsFor"/>).
    /// 0 when infantry has a row in the band.
    /// </summary>
    private static int InfantryGap(List<RosterRow> infantry, LevelBand band) =>
        infantry.Count > 0 && infantry[0].Level < band.Low ? band.High - infantry[0].Level : 0;

    /// <summary>In double, so a large wave cannot overflow before the cap. At least one creep, so a small factor cannot empty a wave.</summary>
    private int TotalOf(CultureDef culture, int wave)
    {
        var raw = (_c.CountStart + (double)_c.CountPerWave * (wave - 1)) * culture.CountFactor;
        return (int)Math.Max(1d, Math.Min(_c.CountMax, Math.Round(raw, MidpointRounding.AwayFromZero)));
    }

    /// <summary>
    /// The band's rows. With none, the rows of the nearest lower level within the limit, all of one level. Infantry alone may fall
    /// further, to the top of its tree, because a wave needs foot soldiers. Nothing reaches upward.
    /// </summary>
    private List<RosterRow> RowsFor(CultureDef culture, CreepClass cls, LevelBand band)
    {
        var rows = culture.Roster.Where(row => row.Class == cls).ToList();
        var inBand = rows.Where(row => row.Level >= band.Low && row.Level <= band.High).ToList();
        if (inBand.Count > 0)
            return inBand;

        var lower = rows.Where(row => row.Level < band.Low).ToList();
        if (cls != CreepClass.Infantry)
            lower = lower.Where(row => row.Level >= band.Low - _c.NearestLowerLevels).ToList();

        if (lower.Count == 0)
            return lower;

        var top = lower.Max(row => row.Level);
        return lower.Where(row => row.Level == top).ToList();
    }

    /// <summary>Largest remainder over the classes that have rows. The weight of a class with no rows moves to infantry.</summary>
    private static Dictionary<CreepClass, int> ClassCounts(CultureDef culture, Dictionary<CreepClass, List<RosterRow>> rows, int total)
    {
        var weights = Classes.ToDictionary(cls => cls, cls => culture.Mix.TryGetValue(cls, out var weight) ? weight : 0);
        foreach (var cls in Classes.Where(cls => cls != CreepClass.Infantry && rows[cls].Count == 0))
        {
            weights[CreepClass.Infantry] += weights[cls];
            weights[cls] = 0;
        }

        var sum = weights.Values.Sum();
        if (sum <= 0)
            return Classes.ToDictionary(cls => cls, cls => 0);

        var counts = Classes.ToDictionary(cls => cls, cls => total * weights[cls] / sum);
        var leftover = total - counts.Values.Sum();
        foreach (var cls in Classes.OrderByDescending(cls => total * weights[cls] % sum).ThenBy(cls => (int)cls).Take(leftover))
            counts[cls]++;

        return counts;
    }

    private IReadOnlyList<CreepClass> ArrivalOrder(CultureDef culture, int wave)
    {
        var signature = _c.Signatures[culture.Signature];
        if (!signature.Shuffled)
            return signature.Order;

        var order = signature.Order.ToArray();
        Shuffle(order, RunRandom.For(_seed, OrderStream, wave, 0));
        return order;
    }

    private List<CreepSpec> CreepsOf(CreepClass cls, List<RosterRow> rows, int count, int wave)
    {
        var creeps = new List<CreepSpec>();
        if (count <= 0 || rows.Count == 0)
            return creeps;

        var levels = rows.Select(row => row.Level).Distinct().OrderByDescending(level => level).ToList();
        var topCount = levels.Count == 1 ? count : (int)Math.Round(count * _c.HighLevelShare, MidpointRounding.AwayFromZero);
        var lowerLeft = count - topCount;
        for (var i = 0; i < levels.Count; i++)
        {
            var levelsBelow = levels.Count - i;
            var share = i == 0 ? topCount : lowerLeft / levelsBelow;
            if (i > 0)
                lowerLeft -= share;

            AddLevel(creeps, cls, rows.Where(row => row.Level == levels[i]).ToList(), share, wave);
        }

        var mixed = creeps.ToArray();
        Shuffle(mixed, RunRandom.For(_seed, ShuffleStream, wave, (int)cls));
        return mixed.ToList();
    }

    private void AddLevel(List<CreepSpec> creeps, CreepClass cls, List<RosterRow> rows, int count, int wave)
    {
        var sorted = rows.OrderBy(row => row.TroopId, StringComparer.Ordinal).ToArray();
        Shuffle(sorted, RunRandom.For(_seed, TroopPickStream, wave, (int)cls * 1000 + rows[0].Level));
        var picked = sorted.Take(Math.Max(1, _c.TroopsPerLevel)).ToArray();
        var mounted = cls is CreepClass.Cavalry or CreepClass.HorseArcher;
        for (var i = 0; i < count; i++)
            creeps.Add(new CreepSpec(picked[i % picked.Length].TroopId, rows[0].Level, CreepRank.Soldier, mounted));
    }
}
