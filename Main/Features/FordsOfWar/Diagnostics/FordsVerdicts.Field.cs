using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.FordsOfWar.Diagnostics;

internal static partial class FordsVerdicts
{
    private const double MatchEndAllowance = 5.0;

    private static FordsVerdictRow Check04(FordsRunEvidence run)
    {
        var started = Stage(run, "matchStarted");
        var live = Stage(run, "live");
        var c = run.Client;

        var countdown = live != null ? FordsResult.Pass : FordsResult.NotSeen;
        var countdownNumbers = started != null && live != null ? "mt=" + N(live.Mt - started.Mt) + " wall=" + N(live.Wall - started.Wall) : "";

        var s = run.Start;
        var towers = started == null ? FordsResult.NotSeen
            : s.TowersBuilt == s.TowersExpected && s.KeepsBuilt == 2 ? FordsResult.Pass : FordsResult.Fail;

        var (waves, wavesNumbers) = Waves(run, live);
        var (march, marchNumbers) = March(run);

        var fight = run.Kills.DeathsWest > 0 && run.Kills.DeathsEast > 0 ? FordsResult.Pass : FordsResult.NotSeen;
        var westPaid = Count(run.Kills.Credit, "West:Paid");
        var killGold = westPaid > 0 ? FordsResult.Pass : run.Kills.DeathsEast >= 20 ? FordsResult.Fail : FordsResult.NotSeen;
        var killNumbers = "deathsEast=" + run.Kills.DeathsEast + " westPaid=" + westPaid
            + (killGold == FordsResult.Fail ? " credit(" + Pairs(run.Kills.Credit) + ")" : "");

        var inside = c.MaxOutsideRun >= 2 ? FordsResult.Fail : c.InsideSamples >= 10 ? FordsResult.Pass : FordsResult.NotSeen;
        var ownership = c.CameraFailed ? FordsResult.Fail : c.OwnedFrames > 0 ? FordsResult.Pass : FordsResult.NotSeen;

        var info = "firstSpawnMt=" + string.Join(",", run.Lanes.Select(l => l.FirstSpawnMt == null ? "-" : N(l.FirstSpawnMt.Value)))
            + " liveSeconds=" + N(run.Perf.LiveSeconds) + " deaths=" + run.Kills.DeathsWest + "/" + run.Kills.DeathsEast
            + " owned=" + c.OwnedFrames + " insideSamples=" + c.InsideSamples + " outsideSamples=" + c.OutsideSamples;

        return Row(4, new[]
        {
            Part("countdown", countdown, countdownNumbers),
            Part("towers", towers, "built=" + s.TowersBuilt + " expected=" + s.TowersExpected + " keeps=" + s.KeepsBuilt),
            Part("waves", waves, wavesNumbers),
            Part("march", march, marchNumbers),
            Part("fight", fight, "deathsWest=" + run.Kills.DeathsWest + " deathsEast=" + run.Kills.DeathsEast),
            Part("killGold", killGold, killNumbers),
            Part("pan", Input(c.PanInputFrames, c.PanMovedFrames), "input=" + c.PanInputFrames + " moved=" + c.PanMovedFrames),
            Part("rotate", Input(c.RotateInputFrames, c.TurnedFrames), "input=" + c.RotateInputFrames + " turned=" + c.TurnedFrames),
            Part("zoom", Input(c.ZoomInputFrames, c.ZoomedFrames), "input=" + c.ZoomInputFrames + " zoomed=" + c.ZoomedFrames),
            Part("inside", inside, "inside=" + c.InsideSamples + " maxOutsideRun=" + c.MaxOutsideRun),
            Part("ownership", ownership, "owned=" + c.OwnedFrames + " failed=" + c.CameraFailed),
        }, info,
            "Each part needs positive evidence; pan, rotate and zoom fail only when input over the whole run never changed the camera, and the boundary is the engine's own.",
            "The towers and keeps are drawn, the camera feels right, and the edge was pushed.");
    }

    /// <summary>Input frames that changed the camera pass; 30 or more that never did fail; pushing into a limit alone cannot fail.</summary>
    private static FordsResult Input(long inputFrames, long changedFrames)
    {
        if (inputFrames > 0 && changedFrames > 0)
            return FordsResult.Pass;
        return inputFrames >= 30 && changedFrames == 0 ? FordsResult.Fail : FordsResult.NotSeen;
    }

    private static (FordsResult, string) Waves(FordsRunEvidence run, FordsStageMark? live)
    {
        if (live == null || run.Lanes.Count == 0)
            return (FordsResult.NotSeen, "");

        var missing = 0;
        var late = 0;
        foreach (var lane in run.Lanes)
        {
            if (lane.FirstSpawnMt == null)
                missing++;
            else if (lane.FirstSpawnMt.Value > live.Mt + 60.0)
                late++;
        }

        var numbers = "streams=" + run.Lanes.Count + " noSpawn=" + missing + " late=" + late;
        if (missing == 0 && late == 0)
            return (FordsResult.Pass, numbers);
        return (run.Perf.LiveSeconds >= 60.0 ? FordsResult.Fail : FordsResult.NotSeen, numbers);
    }

    private static (FordsResult, string) March(FordsRunEvidence run)
    {
        var aged = run.Lanes.Where(l => l.LiveSeconds >= 60.0).ToList();
        if (aged.Count == 0)
            return (FordsResult.NotSeen, "no stream with 60 s of live units");

        var numbers = string.Join(" ", aged.Select(l => l.Side + l.Lane + ":gain=" + N(l.MaxGain) + (l.MaxGain < 10.0 ? ",cmd=" + l.LastCommand : "")));
        if (aged.Any(l => l.MaxGain < 10.0))
            return (FordsResult.Fail, numbers);
        return (aged.All(l => l.MaxGain >= 30.0) ? FordsResult.Pass : FordsResult.NotSeen, numbers);
    }

    private static FordsVerdictRow Check05(FordsRunEvidence run)
    {
        var c = run.Client;
        var photo = c.PhotoVetoes > 0 ? FordsResult.Pass : c.PauseEdges > 0 ? FordsResult.Fail : FordsResult.NotSeen;
        var o = run.Options;
        var options = o.Component == false ? FordsResult.Fail : o.Component == true && o.Requested > 0 ? FordsResult.Pass : FordsResult.NotSeen;

        return Row(5, new[]
        {
            Part("photo", photo, "pauseEdges=" + c.PauseEdges + " vetoes=" + c.PhotoVetoes
                + (photo == FordsResult.Fail ? " the menu never asked the views; check MissionScreen.CustomCamera" : "")),
            Part("options", options, "component=" + Tri(o.Component) + " requested=" + o.Requested),
        }, "pauseEdges=" + c.PauseEdges + " vetoes=" + c.PhotoVetoes + " optionsRequested=" + o.Requested,
            "The escape menu asked the views about Photo Mode and a click on Options reached the options component.",
            "The item reads that Photo mode is not allowed, and the options screen opened.");
    }

    private static FordsVerdictRow Check06(FordsSessionEvidence session, FordsRunEvidence run)
    {
        var gold = session.Console.Where(r => r.Command == "fords_gold").ToList();
        var index = gold.FindLastIndex(r => r.Ran && (r.Reply.StartsWith(GrantPrefix, System.StringComparison.Ordinal)
            || (r.MatchLive && r.Reply == FordsOfWarCommands.NoMatchReply)));
        var grant = index < 0 ? FordsResult.NotSeen
            : gold[index].Reply == FordsOfWarCommands.NoMatchReply ? FordsResult.Fail : FordsResult.Pass;

        var c = run.Client;
        var bought = Count(c.Commands, "BuyPack:Ok");
        var buy = bought > 0 ? FordsResult.Pass : FordsResult.NotSeen;
        var feed = c.PackBoughtLines > 0 ? FordsResult.Pass
            : bought > 0 && c.PanelAttached && run.Closed ? FordsResult.Fail : FordsResult.NotSeen;
        var refusals = string.Join(" ", c.Commands.Where(p => p.Key.StartsWith("BuyPack:") && p.Key != "BuyPack:Ok").Select(p => p.Key + "=" + p.Value));

        return Row(6, new[]
        {
            Part("grant", grant, "records=" + gold.Count),
            Part("buy", buy, "ok=" + bought + (refusals == "" ? "" : " refused " + refusals)),
            Part("feed", feed, "lines=" + c.PackBoughtLines + " panel=" + c.PanelAttached),
        }, "goldRecords=" + gold.Count + " packsBought=" + bought + " feedLines=" + c.PackBoughtLines,
            "The gold command granted gold during a live match, a pack was bought, and the buy reached the event feed (judged once the mission ended).",
            "The gold drops by the pack price, and the panel gold matches the console.");
    }

    private static FordsVerdictRow Check07(FordsRunEvidence run)
    {
        var west = run.Heroes.Where(h => h.Side == "West").ToList();
        var now = run.Mt;

        var fielded = west.Where(h => h.Kind == "fielded").ToList();
        var field = Link(fielded, run.Heroes, 5.0, now, run.End.WinnerMt);
        var died = west.Any(h => h.Kind == "died") ? FordsResult.Pass : FordsResult.NotSeen;
        var respawned = west.Where(h => h.Kind == "respawned").ToList();
        var respawn = Link(respawned, run.Heroes, 1.5, now, null);

        var c = run.Client;
        var buybackRefused = string.Join(" ", c.Commands.Where(p => p.Key.StartsWith("Buyback:") && p.Key != "Buyback:Ok").Select(p => p.Key + "=" + p.Value));
        var info = "buyback ok=" + Count(c.Commands, "Buyback:Ok") + (buybackRefused == "" ? "" : " refused " + buybackRefused)
            + " fielded=" + fielded.Count + " respawned=" + respawned.Count;

        return Row(7, new[]
        {
            Part("field", field, "fielded=" + fielded.Count),
            Part("died", died, "died=" + west.Count(h => h.Kind == "died")),
            Part("respawn", respawn, "respawned=" + respawned.Count),
        }, info,
            "Judges only the engine leg: a fielded or respawned hero of the same side must spawn within the allowed seconds (a purchase within 5 mission seconds of the match end is excused: it neither fails nor takes away the pass of the other heroes). Price and delay arithmetic is unit-tested.",
            "The respawn timer and the buyback button are drawn on the hero card.");
    }

    /// <summary>
    /// Each mark needs a later "spawned" mark of the same hero within <paramref name="window"/> seconds. A mark whose window has not
    /// elapsed yet at <paramref name="now"/> neither passes nor fails. The spawn must be of the mark's side. With a <paramref name="winnerMt"/>
    /// within 5 seconds after a mark, a missing spawn is excused (the clock ended and the spawn never ran), not a fail; excused marks
    /// do not count against the marks that did spawn, and a part with only excused marks is not seen.
    /// </summary>
    private static FordsResult Link(List<FordsHeroMark> marks, List<FordsHeroMark> all, double window, double now, double? winnerMt)
    {
        if (marks.Count == 0)
            return FordsResult.NotSeen;

        var pass = 0;
        var excused = 0;
        foreach (var mark in marks)
        {
            var spawn = all.FirstOrDefault(h => h.Kind == "spawned" && h.HeroId == mark.HeroId && h.Side == mark.Side && h.Mt >= mark.Mt - 0.001 && h.Mt <= mark.Mt + window);
            if (spawn == null)
            {
                if (winnerMt is { } won && won >= mark.Mt && won <= mark.Mt + MatchEndAllowance)
                {
                    excused++;
                    continue;
                }

                if (now > mark.Mt + window)
                    return FordsResult.Fail;
                continue;
            }

            pass++;
        }

        return pass > 0 && pass + excused == marks.Count ? FordsResult.Pass : FordsResult.NotSeen;
    }

    private static FordsVerdictRow Check08(FordsRunEvidence run)
    {
        var c = run.Client;
        var push = Count(c.Commands, "Order:Push:Ok");
        var hold = Count(c.Commands, "Order:Hold:Ok");
        var fall = Count(c.Commands, "Order:FallBack:Ok");
        var orders = push > 0 && hold > 0 && fall > 0 ? FordsResult.Pass : FordsResult.NotSeen;
        var refused = string.Join(" ", c.Commands.Where(p => p.Key.StartsWith("Order:") && !p.Key.EndsWith(":Ok")).Select(p => p.Key + "=" + p.Value));

        var holdResult = run.Lanes.Any(l => l.MaxInFireRun >= 2) ? FordsResult.Fail
            : run.Lanes.Any(l => l.HoldSamples >= 5) ? FordsResult.Pass : FordsResult.NotSeen;
        var fire = run.Facts?.FireRange ?? 0f;
        var perLane = string.Join(" ", run.Lanes.Where(l => l.HoldSamples > 0).Select(l =>
            l.Side + l.Lane + ":mean=" + N(l.HoldFrontSum / l.HoldSamples) + ",min=" + N(l.MinHoldFront) + ",inFireRun=" + l.MaxInFireRun
            + (l.FireTower == null ? "" : ",tower=" + l.FireTower)));
        var inFireBy = string.Join(" ", run.Lanes.Where(l => l.MaxInFireRun >= 2 && l.FireTower != null).Select(l => l.Side + l.Lane + " in fire of " + l.FireTower));

        return Row(8, new[]
        {
            Part("orders", orders, "push=" + push + " hold=" + hold + " fallBack=" + fall + (refused == "" ? "" : " refused " + refused)),
            Part("hold", holdResult, "settledSamples=" + run.Lanes.Sum(l => l.HoldSamples) + (inFireBy == "" ? "" : " " + inFireBy)),
        }, "fireRange=" + N(fire) + " baseDistance=" + N(run.Facts?.BaseDistance ?? 0f) + " holdFront " + (perLane == "" ? "-" : perLane),
            "Push, Hold and Fall back were each accepted once, and no held lane that had reached its hold point (its front unit within 10 m of it along the lane's axis) stood inside tower fire range for two samples in a row; a failing lane names the tower that covered it.",
            "The cooldown is drawn on the buttons, and the lane forms up about 60 m before the tower.");
    }
}
