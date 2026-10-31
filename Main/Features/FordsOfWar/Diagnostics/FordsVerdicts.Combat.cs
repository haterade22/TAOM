using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Features.FordsOfWar.Diagnostics;

internal static partial class FordsVerdicts
{
    private static string BlowsByReason(FordsRunEvidence run, TowerBlowClass? cls) =>
        string.Join(" ", Enum.GetNames(typeof(TowerDamageReason)).Select((n, i) => n + "=" + Blows(run, cls, (TowerDamageReason)i)));

    private static FordsVerdictRow Check09(FordsRunEvidence run)
    {
        var locked = Blows(run, null, TowerDamageReason.Locked);
        var drift = AnyDrift(run);
        var lockResult = drift ? FordsResult.Fail : locked > 0 ? FordsResult.Pass : FordsResult.NotSeen;

        var meleeApplied = Blows(run, TowerBlowClass.Melee, TowerDamageReason.Applied);
        var nearSeconds = run.Towers.Values.Sum(t => t.MeleeNearUnitSeconds);
        var melee = meleeApplied > 0 ? FordsResult.Pass : nearSeconds >= 30.0 ? FordsResult.Fail : FordsResult.NotSeen;

        var drifted = string.Join(",", run.Towers.Where(p => p.Value.DriftFlagged).Select(p => p.Key));
        return Row(9, new[]
        {
            Part("lock", lockResult, "locked=" + locked + (drift ? " drift(" + drifted + ")" : "")),
            Part("melee", melee, "applied=" + meleeApplied + " nearUnitSeconds=" + N(nearSeconds)),
        }, "melee blows " + BlowsByReason(run, TowerBlowClass.Melee) + " | all blows " + BlowsByReason(run, null),
            "A blow on a locked tower took nothing, engine and rules hit points never differed by more than 1 on two samples in a row, and melee landed once enemies stood at a tower for 30 unit-seconds.",
            "The tower bar moves when it is struck.");
    }

    private static FordsVerdictRow Check10(FordsRunEvidence run)
    {
        var own = Blows(run, null, TowerDamageReason.OwnSide);
        var drift = AnyDrift(run);
        var result = drift ? FordsResult.Fail : own > 0 ? FordsResult.Pass : FordsResult.NotSeen;
        var sideUnknown = run.Towers.Values.Sum(t => t.SideUnknownApplied);

        return Row(10, new[]
        {
            Part("ownSide", result, "ownSide=" + own),
        }, "sideUnknownApplied=" + sideUnknown,
            "A blow from a tower's own side took nothing and no tower's hit points drifted; not seen is expected when nothing strikes its own tower.",
            "-");
    }

    /// <summary>The ratio of the engine's magnitude to the formula N x multiplier x (impact speed / S) squared, for a hit that qualifies.</summary>
    private static bool TryRatio(FordsVolleyHit hit, FordsVolleyItemInfo item, out double ratio)
    {
        ratio = 0.0;
        if (!hit.Human || hit.Mounted || hit.Shielded || hit.ImpactSpeed <= 0f || item.Speed <= 0f)
            return false;

        var scale = hit.ImpactSpeed / item.Speed;
        var expected = item.Thrust * hit.Multiplier * scale * scale;
        if (!(expected > 0.0))
            return false;

        ratio = hit.BaseMagnitude / expected;
        return true;
    }

    private static double Median(List<double> values)
    {
        values.Sort();
        var mid = values.Count / 2;
        return values.Count % 2 == 1 ? values[mid] : (values[mid - 1] + values[mid]) / 2.0;
    }

    private static FordsVerdictRow Check11(FordsRunEvidence run)
    {
        var v = run.Volley;
        var item = v.Item;
        var itemResult = v.ItemReported && item == null || v.ReachShort ? FordsResult.Fail
            : item != null ? FordsResult.Pass : FordsResult.NotSeen;

        var ratios = new List<double>();
        FordsVolleyHit? worst = null;
        var worstGap = 0.0;
        var mismatch = false;
        if (item != null)
        {
            foreach (var hit in v.Hits)
            {
                if (TryRatio(hit, item, out var r))
                    ratios.Add(r);
                var gap = Math.Max(Math.Abs(hit.MissileTotalDamage - item.Thrust), Math.Abs(hit.LaunchSpeed - item.Speed));
                if (gap > 0.5)
                {
                    mismatch = true;
                    if (gap > worstGap)
                    {
                        worstGap = gap;
                        worst = hit;
                    }
                }
            }
        }

        var median = ratios.Count >= 3 ? Median(new List<double>(ratios)) : 0.0;
        var hitsResult = FordsResult.NotSeen;
        var hitsNumbers = "qualifying=" + ratios.Count + " launched=" + v.Launched + " noGunner=" + v.NoGunnerOrItem + " noTarget=" + v.NoTarget + " seen=" + v.HitsSeen
            + (v.Filtered.Count == 0 ? "" : " filtered(" + Pairs(v.Filtered) + ")");
        if (mismatch)
        {
            hitsResult = FordsResult.Fail;
            hitsNumbers = "mismatch totalDamage=" + N3(worst!.MissileTotalDamage) + " launchSpeed=" + N3(worst.LaunchSpeed) + " expected N="
                + item!.Thrust + " S=" + N3(item.Speed) + "; a missile magnitude or damage model override also changes r";
        }
        else if (ratios.Count >= 3)
        {
            hitsResult = median >= 0.8 && median <= 1.2 ? FordsResult.Pass : FordsResult.Fail;
            hitsNumbers = "qualifying=" + ratios.Count + " medianR=" + N3(median)
                + (hitsResult == FordsResult.Fail ? "; a missile magnitude or damage model override also changes r" : "");
        }
        else if (v.Launched == 0 && v.NoGunnerOrItem + v.NoTarget >= 10)
        {
            hitsResult = FordsResult.Fail;
            hitsNumbers += "; the towers asked to fire and no missile was ever added";
        }
        else if (v.Launched >= 20 && v.HitsSeen == 0)
        {
            hitsResult = FordsResult.Fail;
            hitsNumbers += "; missiles flew and none struck anyone";
        }

        var byPart = string.Join(" ", v.Hits.Where(h => !h.Shielded).GroupBy(h => h.BodyPart).Select(g =>
            g.Key + ":inflicted=" + N(MedianOf(g.Select(h => (double)h.Inflicted))) + ",absorbed=" + N(MedianOf(g.Select(h => (double)h.AbsorbedByArmor)))));

        return Row(11, new[]
        {
            Part("item", itemResult, item == null ? "reported=" + v.ItemReported : "id=" + item.ItemId + " N=" + item.Thrust + " S=" + N3(item.Speed) + " reach=" + N(item.Reach) + " reachShort=" + v.ReachShort),
            Part("hits", hitsResult, hitsNumbers),
        }, "perBodyPart " + (byPart == "" ? "-" : byPart),
            "The item resolved with a reach past the fire range, and the median ratio of the engine's magnitude to N x multiplier x (impact speed / S) squared over unshielded, unmounted human hits is within 20 percent of 1; it fails when the towers asked to fire 10 times and no missile was added, or when 20 missiles flew and no hit was seen.",
            "The hit points lost look right on armoured and leg hits.");
    }

    private static double MedianOf(IEnumerable<double> values) => Median(values.ToList());

    private static FordsVerdictRow Check12(FordsRunEvidence run)
    {
        var w = run.Warg;
        var started = w.Started > 0 ? FordsResult.Pass : FordsResult.NotSeen;
        var restored = w.NullFormation > 0 || w.Stranded > 0 ? FordsResult.Fail : w.Restored > 0 ? FordsResult.Pass : FordsResult.NotSeen;
        var returned = w.NotReturned > 0 ? FordsResult.Fail : w.Returned > 0 ? FordsResult.Pass : FordsResult.NotSeen;

        return Row(12, new[]
        {
            Part("started", started, "started=" + w.Started + (w.Started == 0 ? " (field a warg rider)" : "")),
            Part("restored", restored, "restored=" + w.Restored + " nullFormation=" + w.NullFormation + " stranded=" + w.Stranded),
            Part("returned", returned, "returned=" + w.Returned + " notReturned=" + w.NotReturned),
        }, "riderInactive=" + w.RiderInactive + " superseded=" + w.Superseded + " open=" + w.Open + " maxReturnSeconds=" + N(w.MaxReturnSeconds),
            "A raging warg's rider kept a formation to go back to and was never left on foot without one (stranded), and every restored rider was back in it within 60 mission seconds (the distance is to the formation's front-middle: within max(15 m, half the width + 5 m) plus the formation's depth). A rider whose rage restarted before it returned is counted as superseded: it still counts toward the restored part (its reset restored it), and only its return goes unjudged.",
            "The warg did not wander off between returns.");
    }
}
