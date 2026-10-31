using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Collections;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.FordsOfWar.Diagnostics;

namespace TAOM.Features.Warg;

/// <summary>
/// Counts what the warg rage sequence does to a rider, for the Fords of War run report: how many rages started, how many riders got
/// their formation back, and whether each came back to it. It is switched on only by a Fords mission (<see cref="Activate"/>), so the
/// behavior tree pays one volatile read in every other mission and records, queues and logs nothing. Riders and formations are plain
/// <c>object</c> handles (ADR-007): only <c>ReadRider</c> reads engine agents, and <c>Active</c> compares against <c>Mission.Current</c>. Episodes are keyed on the rider by reference, never
/// on <c>Agent.Index</c> (indices recycle). At most 100 are open. Every member takes this log's own lock; under it the log calls only
/// its two seams, the clock and the rider reader, and the logger runs after the lock is released.
/// </summary>
internal sealed class WargRageLog
{
    private const int MaxOpen = 100;
    private const int MaxLogged = 25;
    private const float NotReturnedSeconds = 60f;
    private const float MinReturnDistance = 15f;
    private const float ReturnMargin = 5f;

    private static WargRageLog? _active;
    private static object? _activeMission;

    private readonly IModLogger _logger;
    private readonly object _gate = new();
    private readonly Dictionary<object, Episode> _open = new(ReferenceIdentity.Instance);
    private int _started;
    private int _restored;
    private int _nullFormation;
    private int _riderInactive;
    private int _stranded;
    private int _returned;
    private int _notReturned;
    private int _superseded;
    private float _maxReturnSeconds;

    private readonly Func<double> _clock;
    private readonly Func<object, object?, RiderView> _read;

    internal WargRageLog(IModLogger logger, Func<double> clock)
        : this(logger, clock, ReadRider)
    {
    }

    internal WargRageLog(IModLogger logger, Func<double> clock, Func<object, object?, RiderView> read)
    {
        _logger = logger;
        _clock = clock;
        _read = read;
    }

    /// <summary>The log of the running Fords mission; null unless a Fords mission activated it and <c>Mission.Current</c> is that mission.</summary>
    internal static WargRageLog? Active
    {
        get
        {
            var log = Volatile.Read(ref _active);
            return log != null && ReferenceEquals(Volatile.Read(ref _activeMission), Mission.Current) ? log : null;
        }
    }

    internal static void Activate(WargRageLog log, object? mission)
    {
        Volatile.Write(ref _activeMission, mission);
        Volatile.Write(ref _active, log);
    }

    internal static void Deactivate(WargRageLog? log)
    {
        if (log != null && ReferenceEquals(Interlocked.CompareExchange(ref _active, null, log), log))
            Volatile.Write(ref _activeMission, null);
    }

    /// <summary>
    /// How far from the formation's front-middle the rider may be and still count as back: 15 m, or half the width plus 5 m when that
    /// is more, plus the formation's depth (a rider at the rear rank is a whole depth behind the point the distance is measured to).
    /// </summary>
    internal static bool HasReturned(float distance, float formationWidth, float formationDepth) =>
        distance <= Math.Max(MinReturnDistance, formationWidth / 2f + ReturnMargin) + formationDepth;

    /// <summary>What the reset did with a rage that ended: nothing (rider gone), nothing to restore, or the formation given back.</summary>
    internal static string Outcome(bool riderActive, bool formationKnown) =>
        !riderActive ? "rider-inactive" : !formationKnown ? "null-formation" : "restored";

    /// <summary>A rage began for an AI rider: the tree is about to take the formation away. Main thread.</summary>
    internal void Started(object rider, bool formationKnown)
    {
        List<string>? lines = null;
        lock (_gate)
        {
            var number = ++_started;
            if (rider != null && _open.TryGetValue(rider, out var previous) && previous.Restored)
            {
                _superseded++;
                Add(ref lines, previous.Number, "superseded episode=" + previous.Number);
            }

            if (rider != null && (_open.Count < MaxOpen || _open.ContainsKey(rider)))
                _open[rider] = new Episode(number);
            Add(ref lines, number, "start episode=" + number + " formationKnown=" + formationKnown);
        }

        WriteAll(lines);
    }

    /// <summary>The reset ran for a rider. Any thread; ignored when the rider has no open episode.</summary>
    internal void Ended(object? rider, object? formation)
    {
        if (rider == null)
            return;

        string? line = null;
        lock (_gate)
        {
            if (!_open.TryGetValue(rider, out var episode) || episode.Restored)
                return;

            var outcome = Outcome(_read(rider, null).Active, formation != null);
            if (outcome == "rider-inactive")
            {
                _riderInactive++;
                _open.Remove(rider);
            }
            else if (outcome == "null-formation")
            {
                _nullFormation++;
                _open.Remove(rider);
            }
            else
            {
                _restored++;
                episode.Restored = true;
                episode.Formation = formation;
                episode.RestoredAt = _clock();
            }

            if (episode.Number <= MaxLogged)
                line = "end episode=" + episode.Number + " outcome=" + outcome;
        }

        Write(line);
    }

    /// <summary>
    /// Checks every open episode: a restored one is back in place, given up on after 60 mission seconds, or silently closed; one never
    /// restored closes as rider-inactive or stranded. Main thread, 1 Hz.
    /// </summary>
    internal void Watch()
    {
        List<string>? lines = null;
        lock (_gate)
        {
            if (_open.Count == 0)
                return;

            var closed = new List<object>();
            foreach (var pair in _open)
            {
                var episode = pair.Value;
                var rider = pair.Key;
                if (!episode.Restored)
                {
                    CloseUnrestored(rider, episode, closed, ref lines);
                    continue;
                }

                if (episode.Formation == null)
                    continue;

                var view = _read(rider, episode.Formation);
                if (!view.Active || !view.InTarget)
                {
                    closed.Add(rider);
                    continue;
                }

                var seconds = (float)(_clock() - episode.RestoredAt);
                if (HasReturned(view.Distance, view.Width, view.Depth))
                {
                    _returned++;
                    _maxReturnSeconds = Math.Max(_maxReturnSeconds, seconds);
                    closed.Add(rider);
                    Add(ref lines, episode.Number, "returned episode=" + episode.Number + " s=" + seconds.ToString("0.0", CultureInfo.InvariantCulture));
                }
                else if (seconds >= NotReturnedSeconds)
                {
                    _notReturned++;
                    closed.Add(rider);
                    Add(ref lines, episode.Number, "not-returned episode=" + episode.Number + " s=" + seconds.ToString("0.0", CultureInfo.InvariantCulture));
                }
            }

            foreach (var rider in closed)
                _open.Remove(rider);
        }

        WriteAll(lines);
    }

    internal FordsWargEvidence Counts()
    {
        lock (_gate)
        {
            return new FordsWargEvidence
            {
                Started = _started,
                Restored = _restored,
                NullFormation = _nullFormation,
                RiderInactive = _riderInactive,
                Stranded = _stranded,
                Returned = _returned,
                NotReturned = _notReturned,
                Superseded = _superseded,
                MaxReturnSeconds = _maxReturnSeconds,
                Open = _open.Count,
            };
        }
    }

    // A rage whose reset never ran: the rider died or left the fight, or it is on foot with no formation and nothing will give one back.
    private void CloseUnrestored(object rider, Episode episode, List<object> closed, ref List<string>? lines)
    {
        var view = _read(rider, null);
        if (!view.Active)
        {
            _riderInactive++;
            closed.Add(rider);
            Add(ref lines, episode.Number, "end episode=" + episode.Number + " outcome=rider-inactive");
        }
        else if (!view.HasMount && !view.HasFormation)
        {
            // The warg-death reset can reach the rider a tick after the dismount, so only a second look in a row counts.
            if (!episode.OnFootOnce)
            {
                episode.OnFootOnce = true;
                return;
            }

            _stranded++;
            closed.Add(rider);
            Add(ref lines, episode.Number, "stranded episode=" + episode.Number);
        }
        else
        {
            episode.OnFootOnce = false;
        }
    }

    // The only place that reads engine agents (<c>Active</c> also names Mission.Current); a stale agent handle reads as inactive (the agent-slot rule).
    private static RiderView ReadRider(object rider, object? target)
    {
        var inactive = new RiderView(false, false, false, false, 0f, 0f, 0f);
        if (rider is not Agent agent || !AgentSlotIdentity.IsCurrentOccupant(agent) || agent.State != AgentState.Active)
            return inactive;

        var formation = target as Formation;
        var distance = formation == null ? 0f : (agent.Position.AsVec2 - formation.CurrentPosition).Length;
        return new RiderView(true, agent.MountAgent != null, agent.Formation != null, formation != null && ReferenceEquals(agent.Formation, formation),
            distance, formation?.Width ?? 0f, formation?.Depth ?? 0f);
    }

    private static void Add(ref List<string>? lines, int episode, string line)
    {
        if (episode > MaxLogged)
            return;

        lines ??= new List<string>();
        lines.Add(line);
    }

    private void Write(string? line)
    {
        if (line != null)
            _logger.LogInfo("[Warg][Rage] " + line);
    }

    private void WriteAll(List<string>? lines)
    {
        if (lines == null)
            return;

        foreach (var line in lines)
            Write(line);
    }

    /// <summary>What one look at a rider found: alive, mounted, in any formation, in the formation looked for, and its distance, width and depth.</summary>
    internal readonly struct RiderView
    {
        public RiderView(bool active, bool hasMount, bool hasFormation, bool inTarget, float distance, float width, float depth)
        {
            Active = active;
            HasMount = hasMount;
            HasFormation = hasFormation;
            InTarget = inTarget;
            Distance = distance;
            Width = width;
            Depth = depth;
        }

        public bool Active { get; }
        public bool HasMount { get; }
        public bool HasFormation { get; }
        public bool InTarget { get; }
        public float Distance { get; }
        public float Width { get; }
        public float Depth { get; }
    }

    private sealed class Episode
    {
        public Episode(int number)
        {
            Number = number;
        }

        public int Number { get; }
        public object? Formation { get; set; }
        public bool Restored { get; set; }
        public double RestoredAt { get; set; }
        public bool OnFootOnce { get; set; }
    }
}
