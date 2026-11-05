using System.Collections.Generic;
using System.Linq;
using TAOM.Features.LongDefeat.Diagnostics;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Features.LongDefeat;

/// <summary>
/// The troop and class boons at the engine (phase2-plan.md section 4, Slice 6): what a pick does to the soldiers standing, the lord's body whenever a new one becomes his, and the
/// read-backs that prove it. The table itself is published by <see cref="Apply"/> first, so a refresh reads the new numbers.
/// </summary>
internal sealed partial class LongDefeatDirector
{
    // Each pick that changed a group's stats or health, until the first pack of that group spawns after it and is read back.
    private readonly List<(int Pick, BoonTarget Target)> _spawnReads = new();

    private void ApplyBoonToStanding(LdBoonApply boon)
    {
        if (_effects == null)
            return;

        foreach (var target in boon.Refresh)
        {
            var touched = _effects.RefreshStats(target);
            _logger.LogInfo($"{Tag}[Boon] refresh {target} n={touched.Count} ms={F2((float)touched.Milliseconds)}");
        }

        foreach (var scale in boon.Health)
        {
            if (scale.Target == BoonTarget.Player)
            {
                // Absolute, like DressNewBody: a body adopted this tick (a wave-clear respawn and the bot's pick can share one) was dressed with the product that already holds this
                // pick, and a relative scale would apply the pick's factor to it a second time.
                _effects.SetPlayerHealth(_run.HealthFactor(BoonTarget.Player));
                _logger.LogInfo($"{Tag}[Boon] health {scale.Target} set x{F2(_run.HealthFactor(BoonTarget.Player))}");
                continue;
            }

            var touched = _effects.ScaleHealth(scale.Target, scale.Factor);
            _logger.LogInfo($"{Tag}[Boon] health {scale.Target} x{F2(scale.Factor)} n={touched.Count} ms={F2((float)touched.Milliseconds)}");
        }
    }

    /// <summary>
    /// A new body is the lord's (the start, a respawn, a soldier taken over after his death). The controller setter calls no stat update (<c>Agent.cs:1233-1290</c>), so the body is
    /// refreshed and given the player's stats and health: the player record replaces its group's, and the health limit is set outright, not scaled, so a taken-over soldier's group factor never stacks.
    /// </summary>
    private void DressNewBody()
    {
        if (_effects == null)
            return;

        _effects.RefreshStats(BoonTarget.Player);
        _effects.SetPlayerHealth(_run.HealthFactor(BoonTarget.Player));
    }

    /// <summary>The standing read of the pick just taken: a soldier of each target the boon changed, against the numbers every boon owned gives that target. Empty with no effects adapter or no such target.</summary>
    private List<CheckVerdict> ReadPickedTargets(BoonPickRecord pick)
    {
        var reads = new List<CheckVerdict>();
        var taken = _run.OwnedBoons.FirstOrDefault(b => b.Id == pick.Taken);
        if (_effects == null || taken == null)
            return reads;

        var owned = _run.OwnedBoons;
        foreach (var target in RunModifiers.StatTargets(taken).Concat(RunModifiers.HealthTargets(taken)).Distinct())
        {
            var verdict = ReadTarget(pick.Pick, "standing", target, owned, newest: false);
            reads.Add(verdict);
            if (target != BoonTarget.Player)
                _spawnReads.Add((pick.Pick, target));
        }

        return reads;
    }

    private CheckVerdict ReadTarget(int pick, string when, BoonTarget target, IReadOnlyCollection<BoonRow> owned, bool newest)
    {
        var stats = LongDefeatBoonChecks.TargetExpectations(owned, target).Select(e => e.Stat).Distinct().ToList();
        var verdict = LongDefeatBoonChecks.TargetRead(pick, when, target, owned, _effects!.ReadTarget(target, stats, newest));
        _logger.LogInfo($"{Tag}[Boon] {verdict.Result.ToString().ToLowerInvariant()} {verdict.Detail}");
        return verdict;
    }

    /// <summary>The first defender batch of a group after a pick that changed it: the newest soldier of that group is read back, once per pick and group, and written as its own boon check.</summary>
    private void ReadSpawnedAfterPick(SoldierBatch batch, LdSpawnReport report)
    {
        if (_spawnReads.Count == 0 || _effects == null || report.Spawned == 0)
            return;

        var group = _plan.GroupOf(batch.TroopId, batch.Slot);
        foreach (var due in _spawnReads.Where(r => r.Target == group).ToList())
        {
            _spawnReads.Remove(due);
            var verdict = ReadTarget(due.Pick, "spawned", group, _run.OwnedBoons, newest: true);
            LongDefeatChecks.Record(_outcomes, LongDefeatChecks.BoonId, $"p{due.Pick}-{group.ToString().ToLowerInvariant()}", verdict);
        }
    }
}
