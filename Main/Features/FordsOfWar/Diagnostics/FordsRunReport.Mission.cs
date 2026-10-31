using System;
using System.Collections.Generic;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Features.FordsOfWar.Diagnostics;

internal sealed partial class FordsRunReport
{
    /// <summary>Hot, throw-free. Advances the run's mission clock; true when a 30 second window has just closed.</summary>
    internal bool MissionTick(float dt, double frameMs)
    {
        var run = _run;
        if (run == null)
            return false;

        run.Mt += dt;
        if (dt != 0f && frameMs >= 0.0)
            _frames.Record(frameMs);
        if (run.Live && !run.Closed && run.End.Winner == null)
            run.Perf.LiveSeconds += dt;

        _windowSeconds += dt;
        if (_windowSeconds < WindowSeconds)
            return false;

        _windowSeconds = 0.0;
        return true;
    }

    internal void Window(int agentsAll, int agentsActive) => Guard(nameof(Window), () =>
    {
        var run = _run;
        if (run == null)
            return;

        var frame = _frames.Emit(run.Mt);
        var window = new FordsPerfWindow
        {
            Mt = run.Mt, Frames = frame.Frames, AvgMs = frame.AverageMs, P95Ms = frame.P95Ms, MaxMs = frame.MaxMs,
            AgentsAll = agentsAll, AgentsActive = agentsActive, LiveWest = _liveWest, LiveEast = _liveEast,
        };
        var checks = Array.Empty<char>();
        long blows = 0;
        long applied = 0;
        int towersWest = 0;
        int towersEast = 0;
        lock (_sync)
        {
            if (run.Perf.Windows.Count < WindowCap)
                run.Perf.Windows.Add(window);
            if (agentsAll > run.Perf.PeakAgentsAll)
                run.Perf.PeakAgentsAll = agentsAll;

            var rows = FordsVerdicts.Judge(Session, run);
            checks = new char[rows.Count];
            for (var i = 0; i < rows.Count; i++)
                checks[i] = FordsResults.Letter(rows[i].Result);
            blows = FordsVerdicts.Blows(run, null, null);
            applied = FordsVerdicts.Blows(run, null, TowerDamageReason.Applied);
            foreach (var tower in run.Towers.Values)
            {
                if (tower.FellMt != null)
                {
                    if (tower.Owner == "West")
                        towersWest++;
                    else
                        towersEast++;
                }
            }
        }

        var phase = run.Closed || run.End.Winner != null ? "Ended" : run.Live ? "Live" : "Countdown";
        var credit = run.Kills.Credit;
        Info("Summary", "phase=" + phase + " liveS=" + FordsLogLine.F1(run.Perf.LiveSeconds) + " liveW=" + _liveWest + " liveE=" + _liveEast
            + " backlog=" + _backlog + " deathsW=" + run.Kills.DeathsWest + " deathsE=" + run.Kills.DeathsEast
            + " paidW=" + (credit.TryGetValue("West:Paid", out var paid) ? paid : 0) + " towersW=" + towersWest + " towersE=" + towersEast
            + " blows=" + blows + " applied=" + applied + " missiles=" + run.Volley.Launched + " volleyHits=" + run.Volley.HitsSeen
            + " agentsAll=" + agentsAll + " agentsActive=" + agentsActive + " frames=" + window.Frames
            + " avgMs=" + FordsLogLine.F3(window.AvgMs) + " p95Ms=" + FordsLogLine.F3(window.P95Ms) + " maxMs=" + FordsLogLine.F3(window.MaxMs)
            + " checks=" + new string(checks));
    });

    /// <summary>
    /// Binds the evidence the hot path needs: the tower table and the lane list. Called before the hit patch is installed, so
    /// <see cref="TowerBlow"/> never meets a missing tower. Towers counts every slot, keeps included.
    /// </summary>
    internal void MatchStarted(FordsMatchFacts facts, IReadOnlyList<TowerSlot> built, int towersExpected) => Guard(nameof(MatchStarted), () =>
    {
        var run = _run;
        if (run == null)
            return;

        var lanes = new List<FordsLaneEvidence>();
        for (var side = 0; side < LaneStreams.Sides; side++)
        {
            for (var lane = 0; lane < facts.Lanes; lane++)
                lanes.Add(new FordsLaneEvidence { Side = side == 0 ? "West" : "East", Lane = lane });
        }

        var keeps = 0;
        lock (_sync)
        {
            run.Facts = facts;
            run.Lanes = lanes;
            foreach (var slot in built)
            {
                if (slot.Kind == TowerKind.Keep)
                    keeps++;
                run.Towers[slot.Id] = new FordsTowerEvidence { Owner = slot.Owner.ToString(), Lane = slot.Lane, Kind = slot.Kind.ToString() };
            }

            run.Start.TowersExpected = towersExpected;
            run.Start.TowersBuilt = built.Count;
            run.Start.KeepsBuilt = keeps;
        }

        Info("Run", "match towers=" + built.Count + " expected=" + towersExpected + " keeps=" + keeps + " lanes=" + facts.Lanes);
    });

    internal void Preflight(int idsChecked, IReadOnlyList<string> missing) => Guard(nameof(Preflight), () =>
    {
        var run = _run;
        if (run == null)
            return;

        run.Start.IdsChecked = idsChecked;
        foreach (var id in missing)
        {
            if (run.Start.MissingIds.Count < MissingCap)
                run.Start.MissingIds.Add(id);
        }

        Info("Spawn", "preflight checked=" + idsChecked + " missing=" + missing.Count);
        foreach (var id in missing)
            Warn("Spawn", "missing id=" + FordsLogLine.Q(id));
    });

    internal void VolleyItem(FordsVolleyItemInfo? item, float fireRange) => Guard(nameof(VolleyItem), () =>
    {
        var run = _run;
        if (run == null)
            return;

        var v = run.Volley;
        v.ItemReported = true;
        v.Item = item;
        v.ReachShort = item != null && item.Reach < fireRange;
        if (item == null)
            Warn("Volley", "item none fireRange=" + FordsLogLine.F1(fireRange));
        else
            Info("Volley", "item id=" + FordsLogLine.Q(item.ItemId) + " thrust=" + item.Thrust + " speed=" + FordsLogLine.F1(item.Speed)
                + " reach=" + FordsLogLine.F1(item.Reach) + " fireRange=" + FordsLogLine.F1(fireRange) + " reachShort=" + v.ReachShort);
    });

    internal void Spawned(MatchSide side, int lane, string troopId) => Guard(nameof(Spawned), () =>
    {
        var run = _run;
        if (run == null)
            return;

        var s = run.Spawn;
        s.Spawned++;
        s.SpawnedByTroop[troopId] = Count(s.SpawnedByTroop, troopId) + 1;
        var stream = LaneOf(run, side, lane);
        if (stream != null)
            stream.FirstSpawnMt ??= run.Mt;
    });

    internal void SpawnFailed(string troopId, string reason) => Guard(nameof(SpawnFailed), () =>
    {
        var run = _run;
        if (run == null)
            return;

        run.Spawn.Failed++;
        run.Spawn.FailedByTroop[troopId] = Count(run.Spawn.FailedByTroop, troopId) + 1;
        if (reason == "no-lane") // the director already warns once for a troop the engine cannot build
            Warn("Spawn", "failed troop=" + FordsLogLine.Q(troopId) + " reason=" + reason);
    });

    internal void HeroMark(string kind, string heroId, MatchSide side, int requestLane = -1) => Guard(nameof(HeroMark), () =>
    {
        var run = _run;
        if (run == null)
            return;

        if (run.Heroes.Count < HeroCap)
            run.Heroes.Add(new FordsHeroMark
            {
                Kind = kind, HeroId = heroId, Side = side.ToString(), Mt = run.Mt, RequestLane = requestLane,
            });
        Info("Hero", "kind=" + kind + " hero=" + FordsLogLine.Q(heroId) + " side=" + side + " requestLane=" + requestLane);
    });

    /// <summary>Counts a death of <paramref name="victimSide"/>; the credit is keyed by the opposing side, the one that would be paid.</summary>
    internal void Death(MatchSide victimSide, KillCredit credit) => Guard(nameof(Death), () =>
    {
        var run = _run;
        if (run == null)
            return;

        if (victimSide == MatchSide.West)
            run.Kills.DeathsWest++;
        else
            run.Kills.DeathsEast++;
        var key = MatchSides.Opponent(victimSide) + ":" + credit;
        run.Kills.Credit[key] = Count(run.Kills.Credit, key) + 1;
    });

    internal void LaneCommand(MatchSide side, int lane, LaneCommandKind kind, bool given) => Guard(nameof(LaneCommand), () =>
    {
        var run = _run;
        if (run == null)
            return;

        var stream = LaneOf(run, side, lane);
        if (given && stream != null)
            stream.LastCommand = kind.ToString();

        var n = NextOrdinal(run, FordsLogCategory.LaneCommand);
        if (n <= LaneSampleCap)
            Info("Lane", "side=" + side + " lane=" + lane + " cmd=" + kind + " given=" + given);
        CappedLine(FordsLogCategory.LaneCommand, n);
    });

    /// <summary>Any thread. Counts the blow in the tower's integer-indexed array and logs the first few.</summary>
    internal void TowerBlow(string towerId, TowerBlowClass cls, TowerDamageReason reason, int raw, bool sideUnknown)
    {
        var run = _run;
        if (run == null)
            return;

        int n;
        lock (_sync)
        {
            if (run.Towers.TryGetValue(towerId, out var tower))
            {
                tower.Blows[(int)cls, (int)reason]++;
                if (sideUnknown && reason == TowerDamageReason.Applied)
                    tower.SideUnknownApplied++;
            }

            n = NextOrdinal(run, FordsLogCategory.TowerBlow);
        }

        if (n <= TowerSampleCap)
            Trace("Tower", "blow tower=" + towerId + " class=" + cls + " outcome=" + reason + " raw=" + raw);
        CappedLine(FordsLogCategory.TowerBlow, n);
    }

    internal void TowerHealth(string towerId, int rulesHp, float engineHp) => Guard(nameof(TowerHealth), () =>
    {
        var run = _run;
        if (run == null || !run.Towers.TryGetValue(towerId, out var tower))
            return;

        var drift = Math.Abs(rulesHp - engineHp);
        tower.DriftRun = drift > 1.0 ? tower.DriftRun + 1 : 0;
        tower.MaxDrift = Math.Max(tower.MaxDrift, (int)drift);
        if (tower.DriftRun >= 2 && !tower.DriftFlagged)
        {
            tower.DriftFlagged = true;
            Warn("Tower", "drift tower=" + towerId + " rulesHp=" + rulesHp + " engineHp=" + FordsLogLine.F1(engineHp));
        }
    });

    internal void TowerFell(string towerId, bool keep) => Guard(nameof(TowerFell), () =>
    {
        var run = _run;
        if (run == null)
            return;

        if (run.Towers.TryGetValue(towerId, out var tower))
            tower.FellMt = run.Mt;
        if (keep)
            run.End.KeepFellMt = run.Mt;
        Info("Tower", "fell tower=" + towerId + " keep=" + keep);
    });

    internal void Volley(bool targetFound, int missiles) => Guard(nameof(Volley), () =>
    {
        var run = _run;
        if (run == null)
            return;

        if (missiles > 0)
            run.Volley.Launched += missiles;
        else if (!targetFound)
            run.Volley.NoTarget++;
        else
            run.Volley.NoGunnerOrItem++;
    });

    /// <summary>Any thread. Keeps the first 200 hits, counts the rest and logs the first few.</summary>
    internal void VolleyHit(FordsVolleyHit hit)
    {
        var run = _run;
        if (run == null)
            return;

        int n;
        lock (_sync)
        {
            run.Volley.HitsSeen++;
            if (run.Volley.Hits.Count < HitCap)
                run.Volley.Hits.Add(hit);
            n = NextOrdinal(run, FordsLogCategory.VolleyHit);
        }

        if (n <= VolleySampleCap)
            Trace("Volley", "hit base=" + FordsLogLine.F3(hit.BaseMagnitude) + " inflicted=" + hit.Inflicted + " impact=" + FordsLogLine.F3(hit.ImpactSpeed)
                + " part=" + hit.BodyPart + " shielded=" + hit.Shielded + " mounted=" + hit.Mounted);
        CappedLine(FordsLogCategory.VolleyHit, n);
    }

    /// <summary>Any thread. Counts a missile hit the volley reader left out, by its reason.</summary>
    internal void VolleyHitFiltered(string reason)
    {
        var run = _run;
        if (run == null)
            return;

        lock (_sync)
            run.Volley.Filtered[reason] = Count(run.Volley.Filtered, reason) + 1;
    }

    internal void PointCaptured(string pointId) => Guard(nameof(PointCaptured), () =>
    {
        var run = _run;
        if (run == null)
            return;

        PointOf(run, pointId).Captures++;
    });

    private static FordsPointEvidence PointOf(FordsRunEvidence run, string pointId)
    {
        if (!run.Points.TryGetValue(pointId, out var point))
            run.Points[pointId] = point = new FordsPointEvidence();
        return point;
    }

    /// <summary>Merges one 1 Hz sample; <c>dt</c> is the mission seconds since the previous sample.</summary>
    internal void Field(FordsFieldSample sample) => Guard(nameof(Field), () =>
    {
        var run = _run;
        if (run == null)
            return;

        var dt = _lastFieldMt < 0.0 ? 0.0 : run.Mt - _lastFieldMt;
        _lastFieldMt = run.Mt;
        MergeLanes(run, sample, dt);
        foreach (var t in sample.Towers)
        {
            if (run.Towers.TryGetValue(t.TowerId, out var tower))
                tower.MeleeNearUnitSeconds += t.EnemyMeleeInRange * dt;
        }

        foreach (var p in sample.Points)
        {
            var point = PointOf(run, p.PointId);
            if (p.Contested)
                point.ContestedSeconds += dt;
            if (p.NearestApproach >= 0f && p.NearestApproach < point.NearestApproach)
                point.NearestApproach = p.NearestApproach;
        }

        _liveWest = sample.LiveWest;
        _liveEast = sample.LiveEast;
        _backlog = sample.Backlog;
        var perf = run.Perf;
        if (sample.LiveWest > perf.PeakLiveWest || sample.LiveEast > perf.PeakLiveEast)
            perf.PeakLiveMt = run.Mt;
        perf.PeakLiveWest = Math.Max(perf.PeakLiveWest, sample.LiveWest);
        perf.PeakLiveEast = Math.Max(perf.PeakLiveEast, sample.LiveEast);
        run.Spawn.PeakBacklog = Math.Max(run.Spawn.PeakBacklog, sample.Backlog);
    });

    /// <summary>The evidence of one stream: lanes are stored West first, <c>Facts.Lanes</c> to a side. Null outside the match's lanes.</summary>
    private static FordsLaneEvidence? LaneOf(FordsRunEvidence run, MatchSide side, int lane)
    {
        var lanes = run.Facts?.Lanes ?? 0;
        var index = LaneStreams.Index(side, lane, lanes);
        return lane >= 0 && lane < lanes && index < run.Lanes.Count ? run.Lanes[index] : null;
    }

    private static void MergeLanes(FordsRunEvidence run, FordsFieldSample sample, double dt)
    {
        foreach (var l in sample.Lanes)
        {
            var lane = LaneOf(run, l.Side, l.Lane);
            if (lane == null)
                continue;

            if (l.Live > 0)
            {
                lane.FirstFront ??= l.Front;
                lane.MaxGain = Math.Max(lane.MaxGain, l.Front - lane.FirstFront.Value);
                lane.LiveSeconds += dt;
            }

            if (!l.HoldSettled)
            {
                lane.InFireRun = 0;
                lane.RunTower = null;
                continue;
            }

            lane.HoldSamples++;
            if (l.HoldFront >= 0f)
            {
                lane.HoldFrontSum += l.HoldFront;
                lane.MinHoldFront = Math.Min(lane.MinHoldFront, l.HoldFront);
            }

            if (l.InFire > 0.25 * l.Live)
            {
                lane.InFireSamples++;
                lane.RunTower = l.FireTower ?? lane.RunTower;
                lane.InFireRun++;
                if (lane.InFireRun > lane.MaxInFireRun)
                {
                    lane.MaxInFireRun = lane.InFireRun;
                    lane.FireTower = lane.RunTower;
                }
            }
            else
            {
                lane.InFireRun = 0;
                lane.RunTower = null;
            }
        }
    }

    internal void Winner(MatchSide winner) => Guard(nameof(Winner), () =>
    {
        var run = _run;
        if (run == null || run.End.Winner != null)
            return;

        run.End.Winner = winner.ToString();
        run.End.WinnerMt = run.Mt;
        run.End.WinnerWall = Wall;
        Pass(run, false, "result");
    });

    internal void ResultReady(bool playerVictory) => Guard(nameof(ResultReady), () =>
    {
        var run = _run;
        if (run == null)
            return;

        run.End.ResultSuccess = playerVictory;
        run.End.ResultWall = Wall;
        Info("Stage", "name=result playerVictory=" + playerVictory);
    });

    internal void ExitDue(string reason) => Guard(nameof(ExitDue), () =>
    {
        var run = _run;
        if (run == null)
            return;

        run.End.ExitReason = reason;
        run.End.ExitDueMt = run.Mt;
        run.End.ExitDueWall = Wall;
        Info("Stage", "name=exitDue detail=" + reason);
    });

    internal void EndGameRequested(string via) => Guard(nameof(EndGameRequested), () =>
    {
        var run = _run;
        if (run == null)
            return;

        run.End.EndGameVia = via;
        run.End.EndGameWall = Wall;
        Info("Stage", "name=endGame detail=" + via);
    });

    internal void OptionsWired(bool component) => Guard(nameof(OptionsWired), () =>
    {
        var run = _run;
        if (run == null)
            return;

        run.Options.Component = component;
        Info("EscMenu", "optionsWired component=" + component);
    });

    internal void OptionsRequested() => Guard(nameof(OptionsRequested), () =>
    {
        var run = _run;
        if (run == null)
            return;

        run.Options.Requested++;
        Info("EscMenu", "optionsRequested");
    });

    internal void Warg(FordsWargEvidence counts)
    {
        var run = _run;
        if (run != null)
            run.Warg = counts;
    }

    internal void MissionClosed() => Guard(nameof(MissionClosed), () =>
    {
        var run = _run;
        if (run == null)
            return;

        run.Closed = true;
        run.End.ClosedWall = Wall;
        Info("Stage", "name=missionClosed");
        Pass(run, false, "mission-end");
    });
}
