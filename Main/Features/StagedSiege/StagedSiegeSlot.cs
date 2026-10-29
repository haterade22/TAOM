using System;
using TAOM.Features.StagedSiege.Domain;

namespace TAOM.Features.StagedSiege;

/// <summary>
/// The one armed staged siege of the process: its service (main thread only) and the latest immutable
/// <see cref="StageSnapshot"/> (any thread). The AI thread's tactic weights, the spawn-frame patch and the HUD read the snapshot
/// with one volatile read and a reference compare against the mission they serve, so a record a faulted mission never cleared
/// cannot apply to the next battle. Writers are the main thread and take a lock so a publish never interleaves with a clear.
/// </summary>
internal static class StagedSiegeSlot
{
    private static readonly object WriteLock = new();
    private static volatile StagedSiegeService? _service;
    private static volatile StageSnapshot? _snapshot;
    private static (string Scene, StagedEnd End, DateTime Utc)? _lastEnded;

    public static bool HasArmed => _service != null;

    /// <summary>How the previous staged siege ended; the next battle's inert log names it once, then clears it.</summary>
    public static (string Scene, StagedEnd End, DateTime Utc)? LastEnded
    {
        get
        {
            lock (WriteLock)
                return _lastEnded;
        }
    }

    public static void Arm(StagedSiegeService service)
    {
        lock (WriteLock)
        {
            _service = service;
            _snapshot = null;
        }
    }

    /// <summary>The armed service when it belongs to <paramref name="mission"/>. Main thread.</summary>
    public static StagedSiegeService? ServiceFor(object? mission)
    {
        var service = _service;
        return service != null && ReferenceEquals(service.MissionToken, mission) ? service : null;
    }

    /// <summary>Makes the snapshot current, unless it names a mission other than the armed one.</summary>
    public static void Publish(StageSnapshot snapshot)
    {
        lock (WriteLock)
        {
            var service = _service;
            if (service != null && ReferenceEquals(service.MissionToken, snapshot.MissionToken))
                _snapshot = snapshot;
        }
    }

    /// <summary>The snapshot for <paramref name="mission"/>, or null. One volatile read: safe on any thread.</summary>
    public static StageSnapshot? SnapshotFor(object? mission)
    {
        var snapshot = _snapshot;
        return snapshot != null && ReferenceEquals(snapshot.MissionToken, mission) ? snapshot : null;
    }

    public static void ClearIf(object? mission)
    {
        lock (WriteLock)
        {
            var service = _service;
            if (service != null && ReferenceEquals(service.MissionToken, mission))
            {
                _service = null;
                _snapshot = null;
            }
        }
    }

    public static void ClearArmed()
    {
        lock (WriteLock)
        {
            _service = null;
            _snapshot = null;
        }
    }

    public static void SetLastEnded(string scene, StagedEnd end, DateTime utc)
    {
        lock (WriteLock)
            _lastEnded = (scene, end, utc);
    }

    public static void ClearLastEnded()
    {
        lock (WriteLock)
            _lastEnded = null;
    }
}
