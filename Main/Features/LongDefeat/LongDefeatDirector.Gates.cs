using System.Collections.Generic;
using System.Linq;
using TAOM.Features.LongDefeat.Diagnostics;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Features.LongDefeat;

// The gates and the fort points at the director (phase3-plan.md Slice 5, D17 to D19). A gate level and a repair are bought between waves and written on the tick after the buy, the level first and the
// repair second, both after the Hammer (which writes rams and towers only). Each write is read back at once by its check, and a repair is watched until the next clear or the run's end: the gate must take a
// blow or fall, and no attacker may be sampled behind it while it stands.
internal sealed partial class LongDefeatDirector
{
    private GateWatch? _gateWatch;
    private bool _archersLoggedInWave;

    private static float GateHitPoints(IEnumerable<LdGateFacts> gates) => gates.Sum(g => g.Destroyed ? 0f : System.Math.Max(0f, g.HitPoints));

    /// <summary>
    /// A gate level bought since the last tick (D18): every visible gate's maximum becomes its vanilla maximum times <paramref name="factor"/>, keeping a standing gate's share of hit points. A watch already open
    /// is moved to the new hit points, because keeping the share less one point is not a blow.
    /// </summary>
    private void ApplyGateLevel(float factor)
    {
        if (_fort == null)
            return;

        var write = _fort.SetGateLevel(factor);
        var level = _run.GateLevel;
        LongDefeatChecks.Record(_outcomes, LongDefeatChecks.GateLevelId, "l" + level, LongDefeatGateChecks.GateLevel(level, factor, write));
        _gateWatch?.Rebase(GateHitPoints(write.Gates.Select(g => g.After)));
        _logger.LogInfo($"{Tag}[Gates] level {level}: factor {F2(factor)}, " + string.Join("; ", write.Gates.Select(g => $"hit points {F1(g.Before.HitPoints)} of {F1(g.Before.MaxHitPoints)} to {F1(g.After.HitPoints)} of {F1(g.After.MaxHitPoints)}")));
    }

    /// <summary>
    /// A repair bought since the last tick (D17): every visible gate missing a share of its maximum is made whole at its vanilla maximum times <paramref name="factor"/>, by the one path. The wave it precedes
    /// is the purchase's; the check reads the gate back, and the repaired gates are watched from here to the next clear.
    /// </summary>
    private void ApplyGateRepair(float factor)
    {
        if (_fort == null)
            return;

        JudgeGateAttacked(false);
        var wave = _run.FortSummary().Purchases.LastOrDefault(p => p.Kind == PurchaseKind.GateRepair)?.BeforeWave ?? _run.WavesCleared + 1;
        var write = _fort.RepairGates(factor);
        LongDefeatChecks.Record(_outcomes, LongDefeatChecks.GateRepairId, "w" + wave, LongDefeatGateChecks.GateRepair(wave, factor, write));
        _logger.LogInfo($"{Tag}[Gates] repair before wave {wave}: path {write.Path}, " + string.Join("; ", write.Touched.Select(g => $"hit points {F1(g.Before.HitPoints)} to {F1(g.After.HitPoints)} of {F1(g.After.MaxHitPoints)}, defenders within {GateRules.NearRadius:F0} m {g.Near}")));
        if (write.Touched.Any())
            _gateWatch = new GateWatch(wave, GateHitPoints(write.Gates.Select(g => g.After)), write.Path);
    }

    /// <summary>The 5-second state line's sample of the watch: attackers behind a standing repaired gate's plane. The inside is the keep.</summary>
    private void SampleGateWatch()
    {
        if (_gateWatch is { } watch && _fort != null)
            watch.Sample(_fort.AttackersBehindGates(_keepAt));
    }

    /// <summary>Writes the open watch's <c>gate-attacked</c> line, once: at the next clear, or at the run's end (<paramref name="endedRun"/>). The gates are read now, from the gate objects.</summary>
    private void JudgeGateAttacked(bool endedRun)
    {
        if (_gateWatch is not { } watch || _fort == null)
            return;

        _gateWatch = null;
        var gates = _fort.ReadGates();
        LongDefeatChecks.Record(_outcomes, LongDefeatChecks.GateAttackedId, "w" + watch.Wave,
            LongDefeatGateChecks.GateAttacked(watch, GateHitPoints(gates), gates.Any(g => g.Destroyed), endedRun));
    }

    /// <summary>
    /// At the boot: every recorded tower post, tower volley spot and volley origin has a navmesh and lies inside the mission boundary (the sally points' test). Nothing recorded writes a skip. A point that fails
    /// is named here and by the check; it does not refuse the boot, because the slice that reads the point decides what to do without it.
    /// </summary>
    private void CheckFortPoints()
    {
        if (_fort == null)
            return;

        var names = new List<string>();
        var places = new List<LdPlace>();
        foreach (var tower in _plan.Towers)
        {
            for (var i = 0; i < tower.Posts.Count; i++)
            {
                names.Add($"{tower.Id}.post[{i}]");
                places.Add(LdPlace.From(tower.Posts[i]));
            }

            for (var i = 0; i < tower.VolleyOrigins.Count; i++)
            {
                names.Add($"{tower.Id}.volley[{i}]");
                places.Add(LdPlace.At(tower.VolleyOrigins[i]));
            }
        }

        var origins = _plan.Volley?.Origins ?? new List<LdPoint>();
        for (var i = 0; i < origins.Count; i++)
        {
            names.Add($"volley[{i}]");
            places.Add(LdPlace.At(origins[i]));
        }

        var facts = places.Count == 0 ? new List<LdPointFacts>() : _fort.PointsUsable(places);
        var points = names.Select((name, i) => (name, i < facts.Count ? facts[i] : default)).ToList();
        var verdict = LongDefeatGateChecks.FortPoints(points);
        LongDefeatChecks.Record(_outcomes, LongDefeatChecks.FortPointsId, _mission, verdict);
        foreach (var (name, fact) in points.Where(p => !p.Item2.Usable))
            _logger.LogWarning($"{Tag}[Boot] fort point {name} is unusable: navmesh {(fact.Navmesh ? "yes" : "no")}, boundary {(fact.Boundary ? "yes" : "no")}");
        _logger.LogInfo($"{Tag}[Boot] fort points: {verdict.Detail}");
    }

    /// <summary>The A23 boot line: the engine's count of archer positions and the units in each defender formation, which tells the posts slice which formation index is free.</summary>
    private void LogArcherPositions()
    {
        if (_fort != null)
            _logger.LogInfo($"{Tag}[Boot] {_fort.DescribeArcherPositions()}");
    }

    /// <summary>
    /// The A23 line again, once, on the first wave's state line. The boot line is written in the call that spawns the opening batch, before the siege tactic's first occasional tick has split any formation
    /// (<c>ManageFormationCounts</c> runs from <c>TacticComponent.AreFormationsCreated</c> once a formation has units), so an index that reads empty there can be claimed seconds later; this one shows the split.
    /// </summary>
    private void LogArcherPositionsInWave()
    {
        if (_fort == null || _archersLoggedInWave)
            return;

        _archersLoggedInWave = true;
        _logger.LogInfo($"{Tag}[Wave] {_fort.DescribeArcherPositions()}");
    }
}
