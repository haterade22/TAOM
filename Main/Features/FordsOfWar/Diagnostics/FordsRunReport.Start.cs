using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TAOM.Features.FordsOfWar.Diagnostics;

internal sealed partial class FordsRunReport
{
    internal void MenuInstalled(bool hasContent, int factions, int maps) => Guard(nameof(MenuInstalled), () =>
    {
        var m = Session.Menu;
        m.Installed = true;
        m.HasContent = hasContent;
        m.Factions = factions;
        m.Maps = maps;
        Info("Menu", "installed hasContent=" + hasContent + " factions=" + factions + " maps=" + maps);
    });

    /// <summary>Hot: counts every poll; logs only the first poll and a change of answer.</summary>
    internal void MenuPolled(bool hidden)
    {
        var m = Session.Menu;
        m.Polls++;
        if (m.FirstPollHidden == null)
        {
            m.FirstPollHidden = hidden;
            Info("Menu", "first poll hidden=" + hidden);
        }
        else if (m.LastPollHidden != hidden)
        {
            m.PollChanges++;
            Info("Menu", "poll change hidden=" + hidden);
        }

        m.LastPollHidden = hidden;
    }

    internal void MenuClicked() => Guard(nameof(MenuClicked), () =>
    {
        var m = Session.Menu;
        m.Clicks++;
        var open = OpenFlow();
        if (open != null)
        {
            open.Ended = "abandoned";
            Info("Menu", "abandoned step=" + (open.Shown.Count == 0 ? "-" : open.Shown[open.Shown.Count - 1]));
            Rejudge("menu-abandoned");
        }

        NewFlow();
        Info("Menu", "click");
    });

    internal void PromptShown(string step) => Guard(nameof(PromptShown), () =>
    {
        (OpenFlow() ?? NewFlow()).Shown.Add(step);
        Info("Menu", "prompt shown step=" + step);
    });

    internal void PromptAnswered(string step) => Guard(nameof(PromptAnswered), () =>
    {
        (OpenFlow() ?? NewFlow()).Answered.Add(step);
        Info("Menu", "answered step=" + step);
    });

    internal void PromptCancelled(string step) => Guard(nameof(PromptCancelled), () =>
    {
        (OpenFlow() ?? NewFlow()).Ended = "cancel";
        Info("Menu", "cancelled step=" + step);
        Rejudge("menu-cancel");
    });

    /// <summary>Menu evidence that lands after the latest run was finalized (a cancel, an abandoned or a faulted flow) is judged against that run once more.</summary>
    private void Rejudge(string reason)
    {
        var run = _run;
        if (run != null && run.Closed && run.FinalPasses > 0)
            Pass(run, true, reason);
    }

    private FordsMenuFlow? OpenFlow()
    {
        var flows = Session.Menu.Flows;
        return flows.Count > 0 && flows[flows.Count - 1].Ended == "open" ? flows[flows.Count - 1] : null;
    }

    private FordsMenuFlow NewFlow()
    {
        var flows = Session.Menu.Flows;
        var flow = new FordsMenuFlow();
        flows.Add(flow);
        if (flows.Count > FlowCap)
            flows.RemoveAt(0);
        return flow;
    }

    internal void ConsoleCommand(string command, IReadOnlyList<string> args, bool ran, string reply) => Guard(nameof(ConsoleCommand), () =>
    {
        var run = _run;
        var record = new FordsConsoleRecord
        {
            Command = command,
            Args = args.ToList(),
            Ran = ran,
            Reply = reply,
            Run = _parkedRun,
            Setup = _parkedSetup,
            MatchLive = run != null && run.Live && !run.Closed,
        };
        if (_parkedRun > 0 && run != null && run.Run == _parkedRun)
        {
            record.MatchStarted = run.Stages.Any(s => s.Name == "matchStarted");
            record.Failure = run.Failure?.Reason ?? "";
            _bound = record;
        }

        var sameRun = record.Run > 0 && run != null && run.Run == record.Run;
        _parkedRun = 0;
        _parkedSetup = "";
        Session.Console.Add(record);
        if (Session.Console.Count > ConsoleCap)
            Session.Console.RemoveAt(0);
        Info("Console", "cmd=" + command + " args=" + FordsLogLine.Q(string.Join(" ", args)) + " ran=" + ran + " run=" + record.Run
            + " reply=" + FordsLogLine.Q(reply));
        // A route C failure closes the run inside the command, before this record exists; any record after a finalized run is session
        // evidence the run's check 2 has not judged yet: write its final block again with the record.
        Rejudge(sameRun ? "start-failed" : "late-record");
    });

    internal int BeginRun(string trigger, FordsMatchSetup setup)
    {
        try
        {
            var previous = _run;
            if (previous != null && previous.FinalPasses == 0)
                Pass(previous, true, "superseded");
            else if (previous != null && NoticeLost(previous.End))
            {
                previous.End.NoticeUnacknowledged = true;
                Pass(previous, true, "notice-unacknowledged");
            }

            var number = ++Session.RunsBegun;
            var setupText = "scene=" + setup.SceneName + " west=" + setup.WestFaction + " east=" + setup.EastFaction
                + " ai=" + setup.AiProfile + " lanes=" + setup.LaneCount;
            var run = new FordsRunEvidence { Run = number, Trigger = trigger, Setup = setupText, Started = DateTime.Now.ToString("o") };
            _file = FordsReportFile.For(Logger.LogFilePath);

            lock (_sync)
            {
                _run = run;
                _windowSeconds = 0.0;
                _lastFieldMt = -1.0;
                _liveWest = 0;
                _liveEast = 0;
                _backlog = 0;
                _frames.Reset();
            }

            _wallZero = _now();
            _bound = null;
            if (trigger == "menu")
            {
                var open = OpenFlow();
                if (open != null)
                {
                    open.Ended = "start";
                    open.Run = number;
                }
            }
            else
            {
                _parkedRun = number;
                _parkedSetup = setupText;
            }

            var log = string.IsNullOrEmpty(Logger.LogFilePath) ? "none" : Path.GetFullPath(Logger.LogFilePath!);
            Info("Run", "begin trigger=" + trigger + " setup=" + FordsLogLine.Q(setupText) + " log=" + FordsLogLine.Q(log)
                + " report=" + FordsLogLine.Q(_file?.PathFor(number) ?? "none") + " taom=" + FordsLogLine.Q(Informational()));
            return number;
        }
        catch (Exception ex)
        {
            Logger.LogWarning("[FordsOfWar][Report] BeginRun faulted: " + ex.GetType().Name + ": " + ex.Message);
            return _run?.Run ?? 0;
        }
    }

    private static bool NoticeLost(FordsEndEvidence end) =>
        end.NoticeRequested && end.NoticeOk == true && end.MenuReturnedWall != null && end.NoticeAckWall == null;

    internal void Stage(string name, string detail = "") => Guard(nameof(Stage), () =>
    {
        var run = _run;
        if (run == null)
            return;

        if (run.Stages.Count < StageCap)
            run.Stages.Add(new FordsStageMark { Name = name, Wall = Wall, Mt = run.Mt, Detail = detail });
        if (name == "live")
            run.Live = true;
        if (name == "matchStarted" && _bound != null)
            _bound.MatchStarted = true;
        Info("Stage", "name=" + name + (detail == "" ? "" : " detail=" + FordsLogLine.Q(detail)));
    });

    /// <summary>Route A stays open until the mission closes; B and C close the run because no mission is open. First failure wins.</summary>
    internal void Failure(string route, string reason) => Guard(nameof(Failure), () =>
    {
        var run = _run;
        if (run == null || run.Closed)
        {
            // No open run: a prompt step faulted before BeginRun, so the flow is the thing that failed, not an earlier run.
            var open = OpenFlow();
            if (open != null)
                open.Ended = "fault";
            Logger.LogWarning(FordsLogLine.Of("Run", 0, Wall, MissionSeconds, "failure route=" + route + " reason=" + FordsLogLine.Q(reason)));
            if (open != null)
                Rejudge("menu-fault");
            return;
        }

        if (!RecordFailure(run, route, reason))
            return;
        if (route != "A")
            Pass(run, route == "C", "start-failed");
    });

    private bool RecordFailure(FordsRunEvidence run, string route, string reason)
    {
        if (run.Failure != null)
            return false;

        run.Failure = new FordsFailure { Route = route, Reason = reason, Wall = Wall, Mt = run.Mt };
        if (_bound != null)
            _bound.Failure = reason;
        if (route != "A")
            run.Closed = true;
        Warn("Run", "failure route=" + route + " reason=" + FordsLogLine.Q(reason));
        return true;
    }

    internal void NoticeRequested(bool ok) => Guard(nameof(NoticeRequested), () =>
    {
        var run = _run;
        if (run == null)
            return;

        run.End.NoticeRequested = true;
        run.End.NoticeOk = ok;
        Info("Stage", "name=noticeRequested ok=" + ok);
    });

    internal void NoticeAcknowledged() => Guard(nameof(NoticeAcknowledged), () =>
    {
        var run = _run;
        if (run == null)
            return;

        run.End.NoticeAckWall = Wall;
        run.End.AckAfterMenu = run.End.MenuReturnedWall != null;
        Info("Stage", "name=noticeAck ackAfterMenu=" + run.End.AckAfterMenu);
        if (run.Closed)
            Pass(run, true, "notice-ok");
    });

    /// <summary>
    /// The main menu is back. A run still open means the game ended before its mission closed: route B, then close it. A route C run was
    /// finalized at once and never left the menu, so its return is not a second final pass.
    /// </summary>
    internal void MainMenuReturned() => Guard(nameof(MainMenuReturned), () =>
    {
        var run = _run;
        if (run == null || run.End.MenuReturnedWall != null || run.Failure?.Route == "C")
            return;

        if (!run.Closed)
        {
            RecordFailure(run, "B", "the game ended before the match mission closed");
            run.Closed = true;
            run.End.ClosedWall ??= Wall;
        }

        run.End.MenuReturnedWall = Wall;
        Info("Stage", "name=menuReturned");
        Pass(run, true, "menu-return");
    });
}
