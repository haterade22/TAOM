using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.FordsOfWar.Diagnostics;

internal static partial class FordsVerdicts
{
    private static bool IsFullFlow(FordsMenuFlow flow, int maps) =>
        flow.Shown.Contains("faction") && flow.Shown.Contains("enemy") && flow.Shown.Contains("difficulty")
        && (maps <= 1 || flow.Shown.Contains("map"));

    private static FordsVerdictRow Check01(FordsSessionEvidence session)
    {
        var m = session.Menu;
        var installed = m.Installed && m.HasContent ? FordsResult.Pass : FordsResult.Fail;

        var polled = m.Polls == 0 ? FordsResult.NotSeen : m.LastPollHidden == true ? FordsResult.Fail : FordsResult.Pass;

        var good = m.Flows.Count(f => (f.Ended == "start" || f.Ended == "cancel") && IsFullFlow(f, m.Maps));
        var faults = m.Flows.Count(f => f.Ended == "fault");
        var flow = faults > 0 ? FordsResult.Fail : good > 0 ? FordsResult.Pass : FordsResult.NotSeen;

        var lastEnd = m.Flows.FindLastIndex(f => f.Ended == "cancel" || f.Ended == "abandoned");
        var cancel = lastEnd < 0 ? FordsResult.NotSeen : FordsResult.Pass;

        var best = m.Flows.OrderByDescending(f => f.Shown.Count).FirstOrDefault();
        var steps = best == null ? "-" : string.Join(">", best.Shown);
        var info = "factions=" + m.Factions + " maps=" + m.Maps + " polls=" + m.Polls + " firstHidden=" + Tri(m.FirstPollHidden)
            + " changes=" + m.PollChanges + " clicks=" + m.Clicks + " steps=" + steps;

        return Row(1, new[]
        {
            Part("installed", installed, "installed=" + m.Installed + " content=" + m.HasContent),
            Part("polled", polled, "polls=" + m.Polls + " lastHidden=" + Tri(m.LastPollHidden)),
            Part("flow", flow, "flows=" + m.Flows.Count + " good=" + good + " faults=" + faults),
            Part("cancel", cancel, lastEnd < 0 ? "" : "ended=" + m.Flows[lastEnd].Ended + " run=" + m.Flows[lastEnd].Run),
        }, info,
            "Installed with content, the last poll says visible, a full prompt flow ended in a start or a cancel and no flow ended in a fault, and a cancel or abandoned flow was seen.",
            "The entry and its hint are drawn on the main menu.");
    }

    private static string Tri(bool? v) => v == null ? "-" : v.Value ? "true" : "false";

    private static FordsVerdictRow Check02(FordsSessionEvidence session, FordsRunEvidence run)
    {
        var records = session.Console.Where(c => c.Command == "fords_start").ToList();

        var named = StartResult(records, withArgs: true);
        var defaults = StartResult(records, withArgs: false);
        var unknown = records.Any(c => c.Ran && c.Run == 0 && c.Reply.StartsWith(UnknownPrefix, System.StringComparison.Ordinal))
            ? FordsResult.Pass
            : FordsResult.NotSeen;

        var stages = run.Run == 0 ? "-" : string.Join(" ", run.Stages.Select(s => s.Name + "@" + N(s.Wall * 1000.0) + "ms"));
        var refused = records.Count(c => !c.Ran);
        var setups = string.Join(" | ", records.Where(c => c.Args.Count == 0 && c.Setup != "").Select(c => c.Setup));
        var info = "stages(run " + run.Run + ")=" + stages + " refused=" + refused + " defaultsSetup=" + (setups == "" ? "-" : setups);

        return Row(2, new[]
        {
            Part("named", named, "records=" + records.Count(c => c.Args.Count >= 1)),
            Part("unknown", unknown),
            Part("defaults", defaults, "records=" + records.Count(c => c.Args.Count == 0)),
        }, info,
            "A start with arguments or none reached a started match, and an unknown argument got the resolver's reply; the reply text itself is pinned by unit tests.",
            "The console showed the reply.");
    }

    /// <summary>
    /// The newest start that ran with (or without) arguments and either recorded a failure or reached a started match decides:
    /// Fail when it recorded a failure, else Pass.
    /// </summary>
    private static FordsResult StartResult(List<FordsConsoleRecord> records, bool withArgs)
    {
        var index = records.FindLastIndex(c => c.Ran && (c.Args.Count >= 1) == withArgs && (c.Failure != "" || (c.Run > 0 && c.MatchStarted)));
        if (index < 0)
            return FordsResult.NotSeen;
        return records[index].Failure != "" ? FordsResult.Fail : FordsResult.Pass;
    }

    private static FordsVerdictRow Check03(FordsRunEvidence run)
    {
        var started = Stage(run, "matchStarted") != null;
        var s = run.Start;
        var failure = run.Failure;
        var startedPart = started ? FordsResult.Pass : failure != null ? FordsResult.Fail : FordsResult.NotSeen;
        var startedNumbers = started ? "" : failure != null ? "route=" + failure.Route + " reason=" + FordsLogLine.Q(failure.Reason) : "";
        var ids = !started ? FordsResult.NotSeen : s.MissingIds.Count > 0 ? FordsResult.Fail : FordsResult.Pass;
        var spawn = run.Spawn.Failed > 0 ? FordsResult.Fail : run.Spawn.Spawned > 0 ? FordsResult.Pass : FordsResult.NotSeen;
        var heroes = run.Heroes.Count(h => h.Kind == "spawned");

        var info = "idsChecked=" + s.IdsChecked + " missing=" + s.MissingIds.Count + (s.MissingIds.Count > 0 ? "(" + Join(s.MissingIds) + ")" : "")
            + " spawned=" + run.Spawn.Spawned + " failed=" + run.Spawn.Failed + (run.Spawn.Failed > 0 ? "(" + Pairs(run.Spawn.FailedByTroop) + ")" : "")
            + " heroesSpawned=" + heroes;

        return Row(3, new[]
        {
            Part("started", startedPart, startedNumbers),
            Part("ids", ids, "checked=" + s.IdsChecked + " missing=" + s.MissingIds.Count),
            Part("spawn", spawn, "spawned=" + run.Spawn.Spawned + " failed=" + run.Spawn.Failed),
        }, info,
            "The match started (a recorded start failure with no matchStarted fails; a route A failure is expected on the run where check 15 was provoked on purpose), every troop and hero id resolves in this game (checked even for a hero never bought), and no spawn failed.",
            "-");
    }
}
