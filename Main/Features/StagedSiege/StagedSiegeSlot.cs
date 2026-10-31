using System;
using System.Collections.Generic;
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
    private static HudRecordBuilder? _hudRecord;
    private static string? _campaignSettlement;

    /// <summary>What the HUD showed during one mission: the <c>letter:kind</c> pairs and the marker letters, for the run report.</summary>
    public sealed class HudRecord
    {
        public HudRecord(IReadOnlyCollection<string> pairs, IReadOnlyCollection<string> markers)
        {
            Pairs = pairs;
            Markers = markers;
        }

        public IReadOnlyCollection<string> Pairs { get; }
        public IReadOnlyCollection<string> Markers { get; }
    }

    private sealed class HudRecordBuilder
    {
        public HudRecordBuilder(object token) => Token = token;

        public object Token { get; }
        public readonly SortedSet<string> Pairs = new(StringComparer.Ordinal);
        public readonly SortedSet<string> Markers = new(StringComparer.Ordinal);
    }

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
            _hudRecord = null;
            _campaignSettlement = null;
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
            _hudRecord = null;
        }
    }

    // --- the lord's hall: its own armed service and snapshot, under the same token rules ---------------------------------

    public static void ArmHall(StagedHallService hall)
    {
        lock (WriteLock)
        {
            _hall = hall;
            _hallSnapshot = null;
            _hudRecord = null;
            _campaignSettlement = null;
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

    // --- the run report's records ------------------------------------------------------------------------------------

    /// <summary>Records that the HUD showed this <c>letter:kind</c> pair. A new mission token starts a fresh record. Main thread.</summary>
    public static void RecordHud(object token, string pair)
    {
        lock (WriteLock)
            HudFor(token, create: true)!.Pairs.Add(pair);
    }

    /// <summary>Records that the HUD placed its marker over this point letter. Main thread.</summary>
    public static void RecordHudMarker(object token, string letter)
    {
        lock (WriteLock)
            HudFor(token, create: true)!.Markers.Add(letter);
    }

    // Caller holds WriteLock.
    private static HudRecordBuilder? HudFor(object token, bool create)
    {
        var record = _hudRecord;
        if (record != null && ReferenceEquals(record.Token, token))
            return record;
        return create ? _hudRecord = new HudRecordBuilder(token) : null;
    }

    /// <summary>A copy of the HUD record for <paramref name="mission"/>, or null when the HUD showed nothing for it.</summary>
    public static HudRecord? HudFor(object? mission)
    {
        lock (WriteLock)
        {
            var record = mission == null ? null : HudFor(mission, create: false);
            return record == null ? null : new HudRecord(new List<string>(record.Pairs), new List<string>(record.Markers));
        }
    }

    /// <summary>
    /// The settlement of the last staged campaign battle, until its map event ends. The walls and hall services set it at shutdown,
    /// <c>StagedSiegeCampaignLog</c> reads it. Deliberately not cleared by <see cref="ClearArmed"/>: the hall mission's
    /// <c>OnCreated</c> runs that between the walls fight and the hall fight.
    /// </summary>
    public static string? CampaignSettlementId
    {
        get
        {
            lock (WriteLock)
                return _campaignSettlement;
        }
    }

    public static void RecordCampaignSettlement(string settlementId)
    {
        if (string.IsNullOrEmpty(settlementId))
            return;
        lock (WriteLock)
            _campaignSettlement = settlementId;
    }

    public static void ClearCampaignSettlement()
    {
        lock (WriteLock)
            _campaignSettlement = null;
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
