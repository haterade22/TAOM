using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.CreatureBandits.Diagnostics;

internal enum LineVerdict
{
    Write,
    Suppress,
    /// <summary>This line is suppressed, and it is the first one past the mission cap: write one cap warning.</summary>
    CapReached,
}

/// <summary>
/// Everything the diagnostics know about one creature in one mission (#692). The <c>long</c> counters are bumped
/// with <c>Interlocked</c> from any thread (engine callbacks, hot patches); every other field is main-thread only.
/// </summary>
internal sealed class CreatureDiagRecord
{
    internal CreatureDiagRecord(int serial, string troopId, float spawnTime, int agentIndex)
    {
        Serial = serial;
        TroopId = troopId;
        SpawnTime = spawnTime;
        AgentIndex = agentIndex;
    }

    internal int Serial { get; }
    internal string TroopId { get; }
    internal float SpawnTime { get; }
    internal int AgentIndex { get; set; }

    // Any thread (Interlocked).
    internal long HitsTaken, DamageTaken, MissileHitsTaken, MeleeHitsTaken, ChargeHitsTaken, PlayerHitsTaken, BlockedHitsTaken;
    internal long AimedHitsTaken;
    internal long BitesLanded, DamageDealt, NativeHitsDealt, NativeDamageDealt, Kills;
    internal long OnHitPanicBlocked, MoralePanicBlocked, RoutBlocked, Panicked, Fled;
    internal long GuardPrimary, GuardOffhand, GuardMissileRange;

    // Main thread.
    internal int SuppressedLines;
    internal long AttacksFired, Whiffs, EnemiesStruck, AlliesInArc, EngageChecks, EngagePasses;
    internal long EngageScanEmpty, EngageRejectRange, EngageRejectCone, EngageMidAttack;
    internal float EngageBestMissDistance = float.NaN, EngageBestMissAngle = float.NaN, EngageFacingSkew = float.NaN;
    internal string LastEngageKey = string.Empty;
    internal float LastEngageLine = float.MinValue;
    internal int TargetChanges, StuckEpisodes, TargetedByMax;
    internal long TargetedBySamples, TargetedBySum, ImmediateBySum;
    internal long NearSoldierSamples, NearTargetingIt, NearTargetingOther, NearTargetNull;
    internal float StalledInContactSeconds, MinContactDistance = float.MaxValue;
    internal long StaleHandleExits;
    internal bool OccupantLost;
    // Route A (#692): whether the creature was unmounted so soldiers can target it, and its first-contact milestones.
    internal string RouteA = "-";
    internal float UnmountTime = float.NaN, FirstHitTime = float.NaN;
    internal string FirstHitKind = "-";
    internal bool AliveLogged, FirstTargetedLogged, FirstHitSurvivedLogged;
    internal float LastNearestEnemy = float.NaN, LastAttackTime = float.MinValue;
    internal float SnapX = float.NaN, SnapY = float.NaN;
    internal string LastNode = "-";
    /// <summary>The hunt target's per-mission creation index (Agent.GetHashCode), never its recyclable slot index.</summary>
    internal int LastTargetKey = int.MinValue;
    internal float LastTargetDistance = float.NaN, MinEnemyDistance = float.MaxValue, MaxSpeed;
    internal bool Stuck;
    internal float StuckSince = float.NaN;
    internal float StuckSeconds;
    internal float LastSampleX = float.NaN, LastSampleY = float.NaN, LastSampleTime = float.NaN;
    internal float DistanceTravelled;
    internal int HpBand = 4;
    internal string LastStateKey = string.Empty;
    internal float LastHeartbeat;
    internal string Fate = "alive";
    internal float EndTime = float.NaN;
    internal string Killer = "-";
    internal readonly Dictionary<string, float> SecondsInNode = new();
    internal readonly Dictionary<string, int> LinesByKind = new();
}

/// <summary>
/// The per-mission ledger (#692): creature records by serial, and the line budget. Serials number creatures from 1
/// per mission; the engine recycles <c>Agent.Index</c>, so it is a display label only. Records live in a concurrent
/// map because hot patches look them up from worker threads; registration, budgets and reset are main-thread only.
/// The budget bounds the chatter: a per-creature cap for each kind of line, and a mission cap with one cap warning.
/// Outcome kinds (<see cref="IsOutcomeKind"/>) skip the mission cap, since the creature count already bounds them,
/// so a long fight cannot starve the deaths and warnings. The mission cap is larger than the crash report's 500-line
/// tail: the tail is not protected against a long, large fight, but the full log is in every crash bundle.
/// <see cref="Reset"/> runs at the mission boundary, so a second battle in one launch logs again.
/// </summary>
internal sealed class CreatureDiagLedger
{
    private readonly int _perCreatureCap;
    private readonly int _missionCap;
    private readonly ConcurrentDictionary<int, CreatureDiagRecord> _records = new();
    private int _nextSerial;
    private int _missionLines;
    private bool _capReported;

    internal CreatureDiagLedger(int perCreatureCap, int missionCap)
    {
        _perCreatureCap = perCreatureCap;
        _missionCap = missionCap;
    }

    // The serial, not _records.Count: ConcurrentDictionary.Count takes every lock, and the ticker's gate asks it each
    // frame in every mission. Records are only added by Register and dropped by Reset, both main-thread.
    internal int Count => _nextSerial;
    internal int MissionLines => _missionLines;
    internal int MissionSuppressed { get; private set; }

    internal IEnumerable<CreatureDiagRecord> Records => _records.Values.OrderBy(r => r.Serial);

    internal CreatureDiagRecord Register(string troopId, float spawnTime, int agentIndex)
    {
        var record = new CreatureDiagRecord(++_nextSerial, troopId, spawnTime, agentIndex);
        _records[record.Serial] = record;
        return record;
    }

    internal CreatureDiagRecord? Get(int serial) => _records.TryGetValue(serial, out var record) ? record : null;

    /// <summary>Whether a line of <paramref name="kind"/> may be written now. Serial 0 is a mission-level line.</summary>
    internal LineVerdict TryTakeLine(int serial, string kind)
    {
        var record = serial > 0 ? Get(serial) : null;
        if (_missionLines >= _missionCap && !IsOutcomeKind(kind))
        {
            MissionSuppressed++;
            if (record != null) record.SuppressedLines++;
            if (_capReported) return LineVerdict.Suppress;
            _capReported = true;
            return LineVerdict.CapReached;
        }

        if (record != null)
        {
            record.LinesByKind.TryGetValue(kind, out int taken);
            if (taken >= _perCreatureCap)
            {
                record.SuppressedLines++;
                return LineVerdict.Suppress;
            }
            record.LinesByKind[kind] = taken + 1;
        }

        _missionLines++;
        return LineVerdict.Write;
    }

    /// <summary>Deaths, backstops, panics, flights, mounts, deletions and the side count: bounded by creature count.</summary>
    internal static bool IsOutcomeKind(string kind) => kind is "removed" or "backstop" or "outcome" or "sides";

    internal void Reset()
    {
        _records.Clear();
        _nextSerial = 0;
        _missionLines = 0;
        _capReported = false;
        MissionSuppressed = 0;
    }

    /// <summary>Health as quarters: 4 above 75%, down to 0 at none; -1 for a non-finite or non-positive limit.</summary>
    internal static int HpBand(float health, float limit)
    {
        if (!(limit > 0f) || float.IsNaN(health) || float.IsInfinity(health) || float.IsInfinity(limit)) return -1;
        float ratio = health / limit;
        if (!(ratio > 0f)) return 0;
        if (ratio > 0.75f) return 4;
        if (ratio > 0.5f) return 3;
        if (ratio > 0.25f) return 2;
        return 1;
    }

    /// <summary>
    /// Chasing a target out of reach, not attacking, and barely moving across the sample window. Positive-polarity
    /// gates: a NaN distance or movement never yields a stuck verdict.
    /// </summary>
    internal static bool IsStuck(bool hasTarget, float targetDistance, float reach, bool attacking, float movedMeters)
        => hasTarget && !attacking && targetDistance > reach && movedMeters >= 0f && movedMeters < StuckMoveMeters;

    /// <summary>Less than this in a sample window, while chasing out of reach, counts as stuck.</summary>
    internal const float StuckMoveMeters = 0.5f;

    /// <summary>
    /// Standing inside the strike radius, barely moving, and not attacking: the contact stall the stuck check cannot
    /// see (the engage gate needs 1.5 m, the strike reaches 3.5 m). NaN speed or distance never yields a verdict.
    /// </summary>
    internal static bool IsStalledInContact(float speed, float targetDistance, float strikeRadius, bool attacking)
        => !attacking && speed < StuckMoveMeters && targetDistance < strikeRadius;

    /// <summary>A miss: an enemy was in the arc and nothing was struck. Brood-mates alone in the arc are not a miss.</summary>
    internal static bool IsWhiff(int enemiesInArc, int struck) => enemiesInArc > 0 && struck == 0;

    /// <summary>One engage evaluation's verdict, from its enemy candidates, those in reach, and those in reach and cone.</summary>
    internal static CreatureEngageOutcome ClassifyEngage(int candidates, int inRange, int passed)
    {
        if (passed > 0) return CreatureEngageOutcome.Passed;
        if (inRange > 0) return CreatureEngageOutcome.RejectedCone;
        return candidates > 0 ? CreatureEngageOutcome.RejectedRange : CreatureEngageOutcome.ScanEmpty;
    }

    /// <summary>
    /// A side with no live agent that the spawn logic still does not call depleted, for longer than the threshold:
    /// the battle cannot end. <paramref name="zeroSince"/> is NaN while the side has agents.
    /// </summary>
    internal static bool IsSideStalled(int liveAgents, bool depleted, float zeroSince, float now, float thresholdSeconds)
        => liveAgents == 0 && !depleted && now - zeroSince > thresholdSeconds;
}

internal enum CreatureEngageOutcome
{
    MidAttack,
    ScanEmpty,
    RejectedRange,
    RejectedCone,
    Passed,
}
