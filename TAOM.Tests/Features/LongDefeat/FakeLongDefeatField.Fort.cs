using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Adapters;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat;

/// <summary>
/// The fortification half of the field fake: a gate that keeps hit points, a maximum and a destroyed flag, and answers a level write and a repair as the real adapter does (D17, D18), through the same
/// <see cref="GateMath"/>. The real field implements both interfaces on one object, so the fake does too, and the director tests hand it over as both.
/// </summary>
internal sealed partial class FakeLongDefeatField
{
    /// <summary>The visible gates the field reads. One gate at Helm's Deep's vanilla 15000 by default; a test sets <c>Gates</c> empty for a fortress with none.</summary>
    public List<LdGateFacts> Gates { get; } = new() { new LdGateFacts(0, 15000f, 15000f, 15000f, false, false) };

    /// <summary>How many hidden gates the boot broke that still stand; a repair must leave it 0.</summary>
    public int HiddenStanding { get; set; }

    /// <summary>A level write lands this share of the maximum it was asked for; 1 is a faithful engine.</summary>
    public float LevelWritesLow { get; set; } = 1f;

    /// <summary>When false the vanilla reset is not found, as after an engine update that renames it (R10).</summary>
    public bool ResetFound { get; set; } = true;

    /// <summary>When false a repair writes the numbers but leaves the gate disabled, as a reset that failed would.</summary>
    public bool RepairWorks { get; set; } = true;

    /// <summary>Defenders within the near radius of a gate when it is repaired.</summary>
    public int NearGate { get; set; }

    /// <summary>What a sample of attackers behind the gates answers while a gate stands; -1 (nothing measured) once every gate is down, as the real field does.</summary>
    public int BehindGate { get; set; }

    /// <summary>The inside point each sample was given.</summary>
    public List<LdPoint?> SamplesAsked { get; } = new();

    /// <summary>Per point, what the fort points check asks; every point is usable by default.</summary>
    public Func<LdPlace, LdPointFacts> FortPointFacts { get; set; } = _ => new LdPointFacts(true, true);

    public List<LdPlace> FortPointsAsked { get; } = new();

    public IReadOnlyList<LdGateFacts> ReadGates() => Gates.ToList();

    private static LdGateFacts Restated(LdGateFacts g, float hp, float max, bool destroyed, bool open) =>
        g with { HitPoints = hp, MaxHitPoints = max, Destroyed = destroyed, Open = open, StateIndex = GateMath.StateIndex(hp, max, 4) };

    public LdGateWrite SetGateLevel(float factor)
    {
        Calls.Add("GateLevel:" + factor.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
        var changes = new List<LdGateChange>();
        for (var i = 0; i < Gates.Count; i++)
        {
            var before = Gates[i];
            var max = before.VanillaMax * factor * LevelWritesLow;
            Gates[i] = Restated(before, GateMath.KeepShare(before.HitPoints, before.MaxHitPoints, max), max, before.Destroyed, before.Open);
            changes.Add(new LdGateChange(before, Gates[i], false, true, 0, true));
        }

        return new LdGateWrite(changes, 1, HiddenStanding, ResetFound);
    }

    public LdGateWrite RepairGates(float factor)
    {
        Calls.Add("RepairGates:" + factor.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
        var changes = new List<LdGateChange>();
        for (var i = 0; i < Gates.Count; i++)
        {
            var before = Gates[i];
            var touched = before.MissingShare >= GateMath.MinMissingShare;
            if (touched && RepairWorks)
            {
                var max = before.VanillaMax * factor;
                Gates[i] = Restated(before, max, max, false, false);
            }

            changes.Add(new LdGateChange(before, Gates[i], touched && !RepairWorks, true, touched ? NearGate : 0, touched));
        }

        return new LdGateWrite(changes, 1, HiddenStanding, ResetFound);
    }

    public int AttackersBehindGates(LdPoint? inside)
    {
        SamplesAsked.Add(inside);
        Calls.Add("Behind");
        return Gates.Any(g => !g.Destroyed) ? BehindGate : -1;
    }

    public IReadOnlyList<LdPointFacts> PointsUsable(IReadOnlyList<LdPlace> points)
    {
        FortPointsAsked.AddRange(points);
        Calls.Add("FortPoints:" + points.Count);
        return points.Select(FortPointFacts).ToList();
    }

    public string DescribeArcherPositions() => "archer_positions=3 formations=0:30,1:10,2:0,3:0,4:0,5:0,6:0,7:0";

    /// <summary>A ram's blow on the gates: takes hit points off every standing gate, and brings one to 0 (broken) when they run out.</summary>
    public void HitGates(float damage)
    {
        for (var i = 0; i < Gates.Count; i++)
        {
            var g = Gates[i];
            var hp = Math.Max(0f, g.HitPoints - damage);
            Gates[i] = Restated(g, hp, g.MaxHitPoints, hp <= 0f, hp <= 0f);
        }
    }
}
