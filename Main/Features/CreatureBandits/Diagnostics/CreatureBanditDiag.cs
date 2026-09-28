using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using TAOM.Core.Collections;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.CreatureBandits.Diagnostics;

internal enum CreatureGuardKind
{
    PrimaryWield,
    OffhandWield,
    MissileRange,
}

/// <summary>
/// The Creature Bandits diagnostics facade (#692): the per-mission ledger, the creature serials, the queue that
/// carries engine callbacks to the main thread, and the hot-path counters. Temporary, strip after sign-off with the
/// recipe in docs/features/creature-bandits.md ("Stripping the diagnostics"), which lists every call site outside
/// this folder.
///
/// Thread rules (csharp-architecture.md): <see cref="Write"/> and anything touching the ledger's budget run on the
/// main thread only. Any thread may call <see cref="SerialOf"/>, <see cref="RecordOf"/>, <see cref="Enqueue"/> and
/// the <c>Note*</c> counters, which use Interlocked and concurrent collections only; the one exception is the first
/// weapon-guard hit per kind per mission, which writes one durable WARNING with its thread and caller stack.
/// </summary>
internal static class CreatureBanditDiag
{
    internal const int PerCreatureCap = 25;
    internal const int MissionCap = 1500;

    internal static IModLogger? Logger => CreatureBanditLog.Logger;
    internal static readonly CreatureDiagLedger Ledger = new(PerCreatureCap, MissionCap);
    internal static readonly ConcurrentQueue<CreatureDiagEvent> Events = new();
    internal static readonly ConcurrentDictionary<string, long[]> CallbackThreads = new();

    private static readonly ConcurrentDictionary<Agent, int> Serials = new(ReferenceIdentity.Instance);

    // Mission counters, any thread.
    internal static long SpawnsAttempted, SpawnsBuilt, SpawnFallbacks, SpawnsDeclined, MountRefusals, BackstopCount;
    internal static long RouteAOn, RouteASkipped;

    /// <summary>How many monster.Flags reads the route A transpiler hooked in Mission.CreateAgent (1 expected); set at patch time.</summary>
    internal static int CreationHookSites = -1;

    /// <summary>Main thread (the spawner): counts a route A outcome for the summary.</summary>
    internal static void NoteRouteA(string outcome)
    {
        if (outcome == "on") Interlocked.Increment(ref RouteAOn);
        else Interlocked.Increment(ref RouteASkipped);
    }
    private static int _firstPrimary, _firstOffhand, _firstMissile;

    internal static int SerialOf(Agent? agent) => agent != null && Serials.TryGetValue(agent, out int serial) ? serial : 0;

    internal static CreatureDiagRecord? RecordOf(Agent? agent)
    {
        int serial = SerialOf(agent);
        return serial > 0 ? Ledger.Get(serial) : null;
    }

    /// <summary>Main thread: reserve a serial for a creature about to spawn (the breadcrumbs need it first).</summary>
    internal static CreatureDiagRecord Reserve(string troopId, float time) => Ledger.Register(troopId, time, -1);

    /// <summary>Main thread: tie the built agent to its reserved record.</summary>
    internal static void Bind(Agent agent, CreatureDiagRecord record)
    {
        record.AgentIndex = agent.Index;
        Serials[agent] = record.Serial;
    }

    internal static void Forget(Agent agent) => Serials.TryRemove(agent, out _);

    /// <summary>Every bound creature and its serial. Main thread readers still gate each handle before a native read.</summary>
    internal static System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<Agent, int>> Creatures => Serials;

    /// <summary>Set when the ticker failed this mission: nothing will drain the queue, so producers stop filling it.</summary>
    internal static volatile bool QueueDisabled;

    /// <summary>
    /// Any thread. After a ticker failure only removals are still queued: the summary's drain needs them for each
    /// creature's fate, and the creature count bounds them.
    /// </summary>
    internal static void Enqueue(in CreatureDiagEvent diagEvent)
    {
        if (!QueueDisabled || diagEvent.Kind == CreatureDiagEventKind.Removed) Events.Enqueue(diagEvent);
    }

    /// <summary>Main thread: write a line if the budget allows it.</summary>
    internal static void Write(int serial, string kind, string line, bool warning = false)
    {
        var logger = Logger;
        if (logger == null) return;
        switch (Ledger.TryTakeLine(serial, kind))
        {
            case LineVerdict.Write:
                if (warning) logger.LogWarning(line); else logger.LogInfo(line);
                break;
            case LineVerdict.CapReached:
                logger.LogWarning(CreatureDiagFormat.Line("cap", 0f, 0, "missionCap", CreatureDiagFormat.I(MissionCap),
                    "note", "further diag lines this mission are counted, not written"));
                break;
        }
    }

    /// <summary>Any thread: an engine callback site's thread, for the summary's thread census.</summary>
    internal static void NoteCallbackThread(string site)
    {
        var counts = CallbackThreads.GetOrAdd(site, _ => new long[2]);
        Interlocked.Increment(ref counts[MissionThreadGuard.IsOnMainThread ? 0 : 1]);
    }

    internal static void NoteOnHitPanicBlocked(Agent agent)
    {
        var record = RecordOf(agent);
        if (record != null) Interlocked.Increment(ref record.OnHitPanicBlocked);
    }

    internal static void NoteMoralePanicBlocked(Agent agent)
    {
        var record = RecordOf(agent);
        if (record != null) Interlocked.Increment(ref record.MoralePanicBlocked);
    }

    internal static void NoteRoutBlocked(Agent agent)
    {
        var record = RecordOf(agent);
        if (record != null) Interlocked.Increment(ref record.RoutBlocked);
    }

    internal static void NoteMountRefused() => Interlocked.Increment(ref MountRefusals);

    /// <summary>
    /// Main thread (the hunt task, ~4 times a second). Keeps the chase distance for the stuck check; writes a line
    /// only when the target changes or the hunt goes idle, never per tick.
    /// </summary>
    internal static void NoteHunt(Agent creature, Agent? target, float distance, float time)
    {
        var record = RecordOf(creature);
        if (record == null) return;
        record.LastTargetDistance = target == null ? float.NaN : distance;
        // Agent.GetHashCode is the per-mission creation index (Agent.cs:5026-5028); Index is recycled.
        int key = target == null ? -1 : target.GetHashCode();
        if (key == record.LastTargetKey) return;
        record.LastTargetKey = key;
        if (target == null)
        {
            Write(record.Serial, "hunt", CreatureDiagFormat.Line("hunt-idle", time, record.Serial, "note", "no enemy to hunt"));
            return;
        }
        record.TargetChanges++;
        Write(record.Serial, "hunt", CreatureDiagFormat.Line("hunt-target", time, record.Serial,
            "target", target.Character?.StringId ?? "-", "targetName", CreatureDiagFormat.Name(target.Name),
            "targetIdx", CreatureDiagFormat.I(target.Index), "targetIsPlayer", CreatureDiagFormat.B(target.IsMainAgent),
            "targetMounted", CreatureDiagFormat.B(target.HasMount), "dist", CreatureDiagFormat.F(distance),
            "changes", CreatureDiagFormat.I(record.TargetChanges)));
    }

    /// <summary>
    /// Main thread (the attack task). One line per attack while the budget lasts; every attack counted. A whiff is an
    /// evaluated strike with an enemy in the arc and nothing struck; allies in the arc are counted apart, since the
    /// team check skips them by design.
    /// </summary>
    internal static void NoteAttack(Agent creature, string kind, string clip, int inArc, int alliesInArc, int struck,
        int maxTargets, float velocityY, float bearing, float time)
    {
        var record = RecordOf(creature);
        if (record == null) return;
        record.AttacksFired++;
        record.EnemiesStruck += struck;
        record.AlliesInArc += alliesInArc;
        record.LastAttackTime = time;
        int enemiesInArc = inArc - alliesInArc;
        bool whiff = CreatureDiagLedger.IsWhiff(enemiesInArc, struck);
        if (whiff) record.Whiffs++;
        Write(record.Serial, "attack", CreatureDiagFormat.Line(whiff ? "attack-whiff" : "attack", time, record.Serial,
            "kind", kind, "clip", clip, "enemiesInArc", CreatureDiagFormat.I(enemiesInArc),
            "alliesInArc", CreatureDiagFormat.I(alliesInArc), "struck", CreatureDiagFormat.I(struck),
            "cap", maxTargets == int.MaxValue ? "-" : CreatureDiagFormat.I(maxTargets),
            "velY", CreatureDiagFormat.F(velocityY), "bearing", CreatureDiagFormat.F(bearing),
            "targetDist", CreatureDiagFormat.F(record.LastTargetDistance), "attacks", CreatureDiagFormat.I(record.AttacksFired)));
    }

    /// <summary>
    /// Main thread (the engage gate, ~5 times a second). Counts why the gate passed or failed, and writes one
    /// <c>engage</c> line per creature at most every 5 s, only when the counts moved, under the per-kind cap.
    /// <paramref name="bestMissDistance"/> and <paramref name="bestMissAngle"/> describe the nearest rejected enemy;
    /// <paramref name="facingSkew"/> is the angle between the gate's facing (Frame.rotation.f) and the strike's
    /// (LookDirection), degrees.
    /// </summary>
    internal static void NoteEngage(Agent creature, CreatureEngageOutcome outcome, float bestMissDistance, float bestMissAngle,
        float facingSkew, float time)
    {
        var record = RecordOf(creature);
        if (record == null) return;
        record.EngageChecks++;
        switch (outcome)
        {
            case CreatureEngageOutcome.MidAttack: record.EngageMidAttack++; break;
            case CreatureEngageOutcome.ScanEmpty: record.EngageScanEmpty++; break;
            case CreatureEngageOutcome.RejectedRange: record.EngageRejectRange++; break;
            case CreatureEngageOutcome.RejectedCone: record.EngageRejectCone++; break;
            default: record.EngagePasses++; break;
        }
        if (!float.IsNaN(bestMissDistance))
        {
            record.EngageBestMissDistance = bestMissDistance;
            record.EngageBestMissAngle = bestMissAngle;
        }
        if (!float.IsNaN(facingSkew)) record.EngageFacingSkew = facingSkew;
        if (time - record.LastEngageLine < 5f) return;

        string key = string.Join("|", record.EngagePasses, record.EngageRejectRange, record.EngageRejectCone, record.EngageScanEmpty);
        if (key == record.LastEngageKey) return;
        record.LastEngageKey = key;
        record.LastEngageLine = time;
        Write(record.Serial, "engage", CreatureDiagFormat.Line("engage", time, record.Serial,
            "checks", CreatureDiagFormat.I(record.EngageChecks), "passed", CreatureDiagFormat.I(record.EngagePasses),
            "rejRange", CreatureDiagFormat.I(record.EngageRejectRange), "rejCone", CreatureDiagFormat.I(record.EngageRejectCone),
            "scanEmpty", CreatureDiagFormat.I(record.EngageScanEmpty), "midAttack", CreatureDiagFormat.I(record.EngageMidAttack),
            "bestMissDist", CreatureDiagFormat.F(record.EngageBestMissDistance),
            "bestMissAngle", CreatureDiagFormat.F(record.EngageBestMissAngle),
            "facingSkew", CreatureDiagFormat.F(record.EngageFacingSkew)));
    }

    /// <summary>Main thread (the hunt task): the tree held a creature handle whose slot changed hands.</summary>
    internal static void NoteStaleHandle(Agent creature)
    {
        var record = RecordOf(creature);
        if (record != null) record.StaleHandleExits++;
    }

    /// <summary>
    /// Soldiers whose target (GetTargetAgent or ImmediateEnemy) was a creature at the last 1 s sample. Rebuilt on the
    /// main thread by the ticker; read off-thread by the hit capture (a managed ContainsKey) to tag aimed hits.
    /// </summary>
    internal static readonly ConcurrentDictionary<Agent, byte> SoldiersAimingAtCreatures = new(ReferenceIdentity.Instance);

    /// <summary>Main thread (the spawn prefix): a creature troop the rules sent to the vanilla spawn.</summary>
    internal static void NoteDeclined(string troopId, bool isPlayerSide, bool isFieldBattle, bool hasCreatureItem, float time)
    {
        Interlocked.Increment(ref SpawnsDeclined);
        if (!MissionThreadGuard.IsOnMainThread) return;
        Write(0, "declined", CreatureDiagFormat.Line("spawn-declined", time, 0, "troop", troopId,
            "playerSide", CreatureDiagFormat.B(isPlayerSide), "fieldBattle", CreatureDiagFormat.B(isFieldBattle),
            "hasCreatureItem", CreatureDiagFormat.B(hasCreatureItem), "note", "vanilla spawn: the husk on its mount"));
    }

    /// <summary>Any thread, hot: a weapon guard answered for a creature. The first per kind per mission is a WARNING.</summary>
    internal static void NoteGuard(CreatureGuardKind kind, Agent agent)
    {
        var record = RecordOf(agent);
        ref int first = ref _firstPrimary;
        switch (kind)
        {
            case CreatureGuardKind.PrimaryWield:
                if (record != null) Interlocked.Increment(ref record.GuardPrimary);
                break;
            case CreatureGuardKind.OffhandWield:
                if (record != null) Interlocked.Increment(ref record.GuardOffhand);
                first = ref _firstOffhand;
                break;
            default:
                if (record != null) Interlocked.Increment(ref record.GuardMissileRange);
                first = ref _firstMissile;
                break;
        }
        if (Interlocked.CompareExchange(ref first, 1, 0) != 0) return;

        try
        {
            Logger?.LogWarning(CreatureDiagFormat.Line("guard-first", float.NaN, record?.Serial ?? 0,
                "kind", kind.ToString(), "thread", CreatureDiagFormat.I(Thread.CurrentThread.ManagedThreadId),
                "main", CreatureDiagFormat.B(MissionThreadGuard.IsOnMainThread),
                "stack", CreatureDiagFormat.Name(new StackTrace(2, false).ToString().Replace("\r\n", " | "))));
        }
        catch
        {
            // A diagnostic never breaks the guard it reports on.
        }
    }

    /// <summary>Main thread, at the mission boundary (the behavior's OnCreated and OnRemoveBehavior).</summary>
    internal static void ResetForMission()
    {
        Ledger.Reset();
        Serials.Clear();
        SoldiersAimingAtCreatures.Clear();
        QueueDisabled = false;
        while (Events.TryDequeue(out _)) { }
        CallbackThreads.Clear();
        SpawnsAttempted = SpawnsBuilt = SpawnFallbacks = SpawnsDeclined = MountRefusals = BackstopCount = 0;
        RouteAOn = RouteASkipped = 0;
        _firstPrimary = _firstOffhand = _firstMissile = 0;
        CreatureDiagSpawn.MissionContextWritten = false;
    }

    public static void ResetForUnload() => ResetForMission();
}
