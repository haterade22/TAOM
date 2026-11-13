using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Features.FordsOfWar.Autorun;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Features.FordsOfWar.Diagnostics;

internal static partial class FordsVerdicts
{
    private static string BlowsByReason(FordsRunEvidence run, TowerBlowClass? cls) =>
        string.Join(" ", Enum.GetNames(typeof(TowerDamageReason)).Select((n, i) => n + "=" + Blows(run, cls, (TowerDamageReason)i)));

    /// <summary>The ladder per stream that was ordered at a tower: orders given, unit-seconds under the order, flagged, in contact with that tower, then its blows and the ones that took hit points.</summary>
    private static string Ladder(FordsRunEvidence run)
    {
        var rows = run.Lanes.Where(l => AttackOrders(l) > 0).Select(l => "ladder " + StreamName(l) + ">" + (l.AttackTarget ?? "-") + " orders=" + AttackOrders(l)
            + " underS=" + N(l.AttackUnitSeconds) + " flaggedS=" + N(l.AttackFlaggedUnitSeconds) + " contactS=" + N(l.AttackContactUnitSeconds)
            + " towerBlows=" + l.TowerBlows + " melee=" + l.MeleeTowerBlows + " gunnerBlows=" + l.GunnerBlows + " applied=" + l.AppliedBlows).ToList();
        return rows.Count == 0 ? "ladder -" : string.Join(" ; ", rows);
    }

    /// <summary>
    /// For each tower that was struck: the lowest hit points the rules held, what regeneration gave back, the damage applied by class,
    /// the melee raw damage, the damage per contact second (applied damage over the melee unit-seconds in contact, when any), the blows the thrust damage floor turned into damage with their total (floored=N/D) and the top probe keys.
    /// </summary>
    private static string TowerHealth(FordsRunEvidence run)
    {
        var rows = new List<string>();
        foreach (var pair in run.Towers)
        {
            var tower = pair.Value;
            long struck = 0;
            foreach (var n in tower.Blows)
                struck += n;
            if (struck == 0)
                continue;

            var applied = string.Join(",", Enum.GetNames(typeof(TowerBlowClass)).Select((name, i) => new { name, damage = tower.AppliedDamage[i] })
                .Where(x => x.damage > 0).Select(x => x.name + ":" + x.damage));
            var raw = tower.MeleeRawSummary();
            var probe = string.Join(",", tower.MeleeProbe.OrderByDescending(x => x.Value).ThenBy(x => x.Key, StringComparer.Ordinal).Take(3)
                .Select(x => x.Key + ":" + x.Value));
            var perContact = tower.ContactUnitSeconds > 0.0 ? " dmgPerContactS=" + N(tower.AppliedDamage.Sum() / tower.ContactUnitSeconds) : "";
            rows.Add(pair.Key + ":minHp=" + (tower.MinRulesHp?.ToString() ?? "-") + " healed=" + tower.HealedTotal + " applied=" + (applied == "" ? "-" : applied)
                + " meleeRaw=n" + raw.Count + "/min" + raw.Min + "/med" + N(raw.Median) + "/max" + raw.Max + " floored=" + tower.FlooredBlows + "/" + tower.FlooredDamage + perContact + (probe == "" ? "" : " probe=" + probe));
        }

        return rows.Count == 0 ? "towerHealth -" : "towerHealth " + string.Join(" ", rows);
    }

    /// <summary>
    /// Fails when a stream marched with more than half its units past the next standing enemy tower on two looks in a row. The numbers
    /// are the looks judged and the failing ones, and for a failure the worst stream (most failing looks, then the largest share).
    /// </summary>
    private static (FordsResult, string) Bypass(FordsRunEvidence run)
    {
        var judged = run.Lanes.Sum(l => l.MarchSamples);
        var failing = run.Lanes.Sum(l => l.PastSamples);
        var numbers = "samplesJudged=" + judged + " failingSamples=" + failing;
        var failed = run.Lanes.Where(l => l.MaxPastRun >= FordsMarchWatch.ConsecutiveLooks)
            .OrderByDescending(l => l.PastSamples).ThenByDescending(l => l.MaxPastShare).ToList();
        if (failed.Count > 0)
        {
            var worst = failed[0];
            numbers += " worst=" + StreamName(worst) + " past " + worst.PastTower + ": " + N(worst.MaxPastShare * 100.0) + "% of its units, "
                + worst.MaxPastRun + " looks in a row, from mt=" + (worst.FirstPastMt == null ? "-" : N(worst.FirstPastMt.Value));
            if (failed.Count > 1)
                numbers += " also=" + string.Join(",", failed.Skip(1).Select(StreamName));
            return (FordsResult.Fail, numbers);
        }

        return (judged > 0 ? FordsResult.Pass : FordsResult.NotSeen, numbers);
    }

    /// <summary>How long a tower strike must have stood ordered before a short run expects a melee blow from it.</summary>
    private const double StrikeAgeSeconds = 30.0;

    /// <summary>Mission seconds from the first AttackTower order of any stream to the end of the run; negative when none was given.</summary>
    private static double OldestStrikeAge(FordsRunEvidence run)
    {
        var key = nameof(LaneCommandKind.AttackTower);
        var first = run.Lanes.Where(l => l.FirstGivenMt.ContainsKey(key)).Select(l => l.FirstGivenMt[key]).DefaultIfEmpty(double.NaN).Min();
        return double.IsNaN(first) ? -1.0 : run.Mt - first;
    }

    private static FordsVerdictRow Check09(FordsRunEvidence run)
    {
        var locked = Blows(run, null, TowerDamageReason.Locked);
        var drift = AnyDrift(run);
        var lockResult = drift ? FordsResult.Fail : locked > 0 ? FordsResult.Pass : FordsResult.NotSeen;

        var meleeApplied = Blows(run, TowerBlowClass.Melee, TowerDamageReason.Applied);
        var nearSeconds = run.Towers.Values.Sum(t => t.MeleeNearUnitSeconds);
        // #1137: early in a match the waves meet near the outer towers before any strike is ordered, and the first order can come
        // too late for a blow. A short run fails only once a strike has been ordered for StrikeAgeSeconds; a full-length run as before.
        var strikeOwed = run.Perf.LiveSeconds >= FordsAutorunPlan.DefaultLiveSeconds || OldestStrikeAge(run) >= StrikeAgeSeconds;
        var melee = meleeApplied > 0 ? FordsResult.Pass : nearSeconds >= 30.0 && strikeOwed ? FordsResult.Fail : FordsResult.NotSeen;
        var (bypass, bypassNumbers) = Bypass(run);

        var drifted = string.Join(",", run.Towers.Where(p => p.Value.DriftFlagged).Select(p => p.Key));
        return Row(9, new[]
        {
            Part("lock", lockResult, "locked=" + locked + (drift ? " drift(" + drifted + ")" : "")),
            Part("melee", melee, "applied=" + meleeApplied + " nearUnitSeconds=" + N(nearSeconds)),
            Part("bypass", bypass, bypassNumbers),
        }, "attackTowerGiven=" + AttackTowerGiven(run) + " nativeTowerBlows=" + Blows(run, null, null)
            + " gunnerBlows=" + run.Towers.Values.Sum(t => t.GunnerBlowsMelee) + " melee/" + run.Towers.Values.Sum(t => t.GunnerBlowsRanged) + " ranged"
            + " | " + Ladder(run) + " | " + TowerHealth(run)
            + " | melee blows " + BlowsByReason(run, TowerBlowClass.Melee) + " | all blows " + BlowsByReason(run, null),
            "A blow on a locked tower took nothing, engine and rules hit points never differed by more than 1 on two samples in a row, melee landed once enemies stood at a tower for 30 unit-seconds (in a run shorter than the default 600 live seconds, only once a tower strike had been ordered 30 mission seconds before the run ended), and no marching stream had more than half its units more than 10 m past the next standing enemy tower on two looks in a row (the looks are the 1 Hz field probe, measured from the units, not from the rules' lead). The ladder names, per stream ordered at a tower, the broken link: units under the order, flagged AttackEntity, within 4 m of that tower, blows on the tower against blows on its gunners, and the blows that took hit points.",
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
            "Passes when at least one blow from a tower's own side was counted as taking nothing (the count includes own-side blows whose raw damage the engine had already zeroed, and own-side blows on locked or fallen towers) and no tower's hit points drifted; not seen is expected when nothing strikes its own tower.",
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

        var arrowId = v.Arrow?.ItemId;
        var javelins = v.Hits.Where(h => h.ItemId != arrowId).ToList();
        var (qualifying, median, worst) = JudgeHits(item, javelins);
        var hitsResult = FordsResult.NotSeen;
        var hitsNumbers = "qualifying=" + qualifying + " launched=" + v.Launched + " noGunner=" + v.NoGunnerOrItem + " noTarget=" + v.NoTarget + " seen=" + v.HitsSeen
            + (v.Filtered.Count == 0 ? "" : " filtered(" + Pairs(v.Filtered) + ")");
        if (worst != null)
        {
            hitsResult = FordsResult.Fail;
            hitsNumbers = "mismatch totalDamage=" + N3(worst!.MissileTotalDamage) + " launchSpeed=" + N3(worst.LaunchSpeed) + " expected N="
                + item!.Thrust + " S=" + N3(item.Speed) + "; a missile magnitude or damage model override also changes r";
        }
        else if (qualifying >= 3)
        {
            hitsResult = median >= 0.8 && median <= 1.2 ? FordsResult.Pass : FordsResult.Fail;
            hitsNumbers = "qualifying=" + qualifying + " medianR=" + N3(median)
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

        var parts = new List<FordsPart>
        {
            Part("item", itemResult, item == null ? "reported=" + v.ItemReported : "id=" + item.ItemId + " N=" + item.Thrust + " S=" + N3(item.Speed) + " reach=" + N(item.Reach) + " reachShort=" + v.ReachShort),
            Part("hits", hitsResult, hitsNumbers),
        };
        var arrows = v.Hits.Where(h => arrowId != null && h.ItemId == arrowId).ToList();
        if (v.ArrowReported)
            parts.Add(ArrowPart(v, arrows));

        return Row(11, parts.ToArray(), "perBodyPart " + ByPart(javelins) + (arrowId == null ? "" : " arrowPerBodyPart " + ByPart(arrows)),
            "The javelin and the arrow each resolved with a reach past the fire range, and for each the median ratio of the engine's magnitude to its own N x multiplier x (impact speed / S) squared over unshielded, unmounted human hits is within 20 percent of 1; it fails when the towers asked to fire 10 times and no missile was added, or when 20 missiles flew and no hit was seen.",
            "The hit points lost look right on armoured and leg hits.");
    }

    /// <summary>The hits of one item against that item's own numbers: how many qualify for the ratio, their median, and the hit furthest from the item, if any differs.</summary>
    private static (int Qualifying, double Median, FordsVolleyHit? Worst) JudgeHits(FordsVolleyItemInfo? item, IReadOnlyList<FordsVolleyHit> hits)
    {
        var ratios = new List<double>();
        FordsVolleyHit? worst = null;
        var worstGap = 0.5;
        if (item != null)
        {
            foreach (var hit in hits)
            {
                if (TryRatio(hit, item, out var r))
                    ratios.Add(r);
                var gap = Math.Max(Math.Abs(hit.MissileTotalDamage - item.Thrust), Math.Abs(hit.LaunchSpeed - item.Speed));
                if (gap > worstGap)
                {
                    worstGap = gap;
                    worst = hit;
                }
            }
        }

        return (ratios.Count, ratios.Count >= 3 ? Median(ratios) : 0.0, worst);
    }

    private static FordsPart ArrowPart(FordsVolleyEvidence v, IReadOnlyList<FordsVolleyHit> arrows)
    {
        if (v.Arrow is not { } arrow)
            return Part("arrow", FordsResult.Fail, "the tower arrow item does not exist; towers fire javelins only");

        var (qualifying, median, worst) = JudgeHits(arrow, arrows);
        var numbers = "id=" + arrow.ItemId + " N=" + arrow.Thrust + " S=" + N3(arrow.Speed) + " reach=" + N(arrow.Reach) + " reachShort=" + v.ArrowReachShort
            + " seen=" + arrows.Count + " qualifying=" + qualifying;
        if (v.ArrowReachShort)
            return Part("arrow", FordsResult.Fail, numbers);
        if (worst != null)
            return Part("arrow", FordsResult.Fail, numbers + " mismatch totalDamage=" + N3(worst.MissileTotalDamage) + " launchSpeed=" + N3(worst.LaunchSpeed));
        if (qualifying < 3)
            return Part("arrow", FordsResult.NotSeen, numbers);

        return Part("arrow", median >= 0.8 && median <= 1.2 ? FordsResult.Pass : FordsResult.Fail, numbers + " medianR=" + N3(median));
    }

    private static string ByPart(IEnumerable<FordsVolleyHit> hits)
    {
        var text = string.Join(" ", hits.Where(h => !h.Shielded).GroupBy(h => h.BodyPart).Select(g =>
            g.Key + ":inflicted=" + N(MedianOf(g.Select(h => (double)h.Inflicted))) + ",absorbed=" + N(MedianOf(g.Select(h => (double)h.AbsorbedByArmor)))));
        return text == "" ? "-" : text;
    }

    private static double MedianOf(IEnumerable<double> values) => Median(values.ToList());

    private static FordsVerdictRow Check12(FordsRunEvidence run)
    {
        var w = run.Warg;
        // No warg rages in a Fords match (Mike, 2026-11-07; WargTryToGoRage), so a start means that gate broke.
        var started = w.Started > 0 ? FordsResult.Fail : FordsResult.NotSeen;
        var restored = w.NullFormation > 0 || w.Stranded > 0 ? FordsResult.Fail : w.Restored > 0 ? FordsResult.Pass : FordsResult.NotSeen;
        var returned = w.NotReturned > 0 ? FordsResult.Fail : w.Returned > 0 ? FordsResult.Pass : FordsResult.NotSeen;

        return Row(12, new[]
        {
            Part("started", started, "started=" + w.Started),
            Part("restored", restored, "restored=" + w.Restored + " nullFormation=" + w.NullFormation + " stranded=" + w.Stranded),
            Part("returned", returned, "returned=" + w.Returned + " notReturned=" + w.NotReturned),
        }, "riderInactive=" + w.RiderInactive + " superseded=" + w.Superseded + " open=" + w.Open + " maxReturnSeconds=" + N(w.MaxReturnSeconds),
            "No warg started a rage, since rage is off in a Fords match. If one does, the start FAILs, and the rest still judges the episode: a raging warg's rider kept a formation to go back to and was never left on foot without one (stranded), and every restored rider was back in it within 60 mission seconds (within max(15 m, half the width + 5 m) plus the formation's depth of the formation's front-middle, or within 15 m of a formation mate that is not itself a raging rider, since a lane formation strings out along its lane). A rider whose rage restarted before it returned is counted as superseded: it still counts toward the restored part (its reset restored it), and only its return goes unjudged.",
            "The warg did not wander off between returns.");
    }
}
