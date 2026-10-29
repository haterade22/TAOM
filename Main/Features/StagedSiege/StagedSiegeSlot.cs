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
    private static volatile StagedHallService? _hall;
    private static volatile HallSnapshot? _hallSnapshot;
    private static (string Scene, StagedEnd End, DateTime Utc)? _lastEnded;

    public static bool HasArmed => _service != null;

    /// <summary>
    /// How the previous staged walls fight ended. The next walls battle reads it once, then clears it, for its inert log; the lord's
    /// hall arms from the saved siege state instead and leaves it alone.
    /// </summary>
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
            _hall = null;
            _hallSnapshot = null;
        }
    }

    // --- the lord's hall: its own armed service and snapshot, under the same token rules ---------------------------------

    public static void ArmHall(StagedHallService hall)
    {
        lock (WriteLock)
        {
            _hall = hall;
            _hallSnapshot = null;
        }
    }

    /// <summary>Makes the hall snapshot current, unless it names a mission other than the armed hall's.</summary>
    public static void PublishHall(HallSnapshot snapshot)
    {
        lock (WriteLock)
        {
            var hall = _hall;
            if (hall != null && ReferenceEquals(hall.MissionToken, snapshot.MissionToken))
                _hallSnapshot = snapshot;
        }
    }

    /// <summary>The hall snapshot for <paramref name="mission"/>, or null. One volatile read: safe on any thread.</summary>
    public static HallSnapshot? HallFor(object? mission)
    {
        var snapshot = _hallSnapshot;
        return snapshot != null && ReferenceEquals(snapshot.MissionToken, mission) ? snapshot : null;
    }

    public static void ClearHallIf(object? mission)
    {
        lock (WriteLock)
        {
            var hall = _hall;
            if (hall != null && ReferenceEquals(hall.MissionToken, mission))
            {
                _hall = null;
                _hallSnapshot = null;
            }
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
