using System;

namespace TAOM.Features.FordsOfWar.Diagnostics;

/// <summary>
/// The melee probe: why an enemy's blow on a tower body lands for the damage it does. The hook sees the engine's collision data and
/// hands this class plain values: a key naming the strike, its flags and the base magnitude bucket (<see cref="MeleeProbeKey"/>),
/// counted per tower, and a trace line for the first few blows of each kind.
/// </summary>
internal sealed partial class FordsRunReport
{
    private volatile Func<object?, string?>? _towerOf;

    /// <summary>Set by the director at match start (and cleared at its shutdown): maps an engine destructible component to the tower it is.</summary>
    internal void BindTowerLookup(Func<object?, string?>? lookup) => _towerOf = lookup;

    /// <summary>The id of the tower <paramref name="component"/> is, or null when no lookup is bound or it is not a tower.</summary>
    internal string? ProbeTowerId(object? component) => component == null ? null : _towerOf?.Invoke(component);

    /// <summary>
    /// "{S|T}/{Cut|Pierce|Blunt|Invalid}/tip{0|1}/pas{0|1}/alt{0|1}/arm{0|1}/mnt{0|1}/{bucket}" for a swing or thrust (<paramref name="strike"/> 1 is
    /// a thrust), its engine damage type (0 cut, 1 pierce, 2 blunt), whether a thrust hit with the tip, whether the attacker was braced,
    /// whether it was an alternative attack, whether the bone that touched was not the weapon's own and whether the attacker was mounted. The bucket is <c>hit</c> when
    /// the blow did damage (<paramref name="raw"/> above 0), else <c>b&gt;=1</c>, <c>b&lt;1</c> or <c>b0</c> by the base magnitude, or <c>bNaN</c>
    /// when it is not a number or below zero.
    /// </summary>
    internal static string MeleeProbeKey(int strike, int damageType, bool tip, bool passive, bool alternative, bool armBone, bool mounted,
        float baseMagnitude, int raw)
    {
        var type = damageType switch { 0 => "Cut", 1 => "Pierce", 2 => "Blunt", _ => "Invalid" };
        var bucket = raw > 0 ? "hit" : baseMagnitude >= 1f ? "b>=1" : baseMagnitude > 0f ? "b<1" : baseMagnitude == 0f ? "b0" : "bNaN";
        return (strike == 1 ? "T" : "S") + "/" + type + "/tip" + Bit(tip) + "/pas" + Bit(passive) + "/alt" + Bit(alternative)
            + "/arm" + Bit(armBone) + "/mnt" + Bit(mounted) + "/" + bucket;
    }

    private static string Bit(bool on) => on ? "1" : "0";

    /// <summary>
    /// Any thread. Counts <paramref name="key"/> on the tower and returns whether the blow's trace line is still within the tower's cap
    /// (the caller then builds the line and passes it to <see cref="TraceTowerMelee"/>): a blow that did damage has one cap per tower and the zero
    /// ones another. An attacker the run knows to be on the tower owner's side is left out, as in <see cref="GunnerHit"/>; one whose side is
    /// unknown is counted. A tower that has fallen is left out too: its rubble can still be struck, and those blows are not the tower's.
    /// </summary>
    internal bool TowerMeleeProbe(string towerId, object? attacker, string key)
    {
        var run = _run;
        if (run == null)
            return false;

        var who = _attackerOf?.Invoke(attacker);
        var damaging = key.EndsWith("/hit", StringComparison.Ordinal);
        var cap = damaging ? ProbeHitCap : ProbeZeroCap;
        int n;
        lock (_sync)
        {
            if (!run.Towers.TryGetValue(towerId, out var tower) || tower.FellMt != null || (who is { } enemy && tower.Owner == enemy.Side.ToString()))
                return false;

            tower.MeleeProbe.TryGetValue(key, out var seen);
            tower.MeleeProbe[key] = seen + 1;
            n = damaging ? ++tower.ProbeHitSampled : ++tower.ProbeZeroSampled;
        }

        if (n == cap + 1)
            Info("Log", "category=" + (damaging ? "TowerProbeHit" : "TowerProbeZero") + " tower=" + towerId + " capped after " + cap + "; counting only");
        return n <= cap;
    }

    /// <summary>Writes the trace line of a blow <see cref="TowerMeleeProbe"/> said to trace.</summary>
    internal void TraceTowerMelee(string line) => Trace("TowerMelee", line);
}
