using System;
using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>
/// Capture points between the lanes. A side standing alone on a point works toward owning it, taking SecondsToCapture
/// however many units it has; both sides present holds the progress (contested); an owner standing on its own point, or
/// nobody there, drains an enemy's unfinished progress at the same rate. An owned point stays owned until an enemy
/// finishes the capture, and pays the owner CaptureIncomePerMinute.
/// </summary>
internal sealed class CapturePointRules
{
    private sealed class Point
    {
        public MatchSide? Owner;
        public MatchSide? Capturer;
        public float Progress;
        public bool Contested;
        public bool Rising;
    }

    private readonly FordsCaptureConfig _capture;
    private readonly FordsEconomyConfig _economy;
    private readonly List<string> _ids;
    private readonly Dictionary<string, Point> _points = new();
    private readonly Dictionary<MatchSide, double> _incomeRemainder = new()
    {
        [MatchSide.West] = 0d,
        [MatchSide.East] = 0d,
    };

    public CapturePointRules(FordsCaptureConfig capture, FordsEconomyConfig economy, IReadOnlyList<CapturePointSlot> points)
    {
        _capture = capture;
        _economy = economy;
        _ids = points.Select(p => p.Id).ToList();
        foreach (var id in _ids)
            _points[id] = new Point();
    }

    public IReadOnlyList<CapturePointState> Points => _ids.Select(id => Get(id)!).ToList();

    public CapturePointState? Get(string pointId) =>
        _points.TryGetValue(pointId, out var p) ? new CapturePointState(pointId, p.Owner, p.Capturer, p.Progress, p.Contested, p.Rising) : null;

    public int OwnedBy(MatchSide side) => _points.Values.Count(p => p.Owner == side);

    /// <summary>Advances every point by <paramref name="dt"/> seconds. A point missing from the counts has nobody on it.</summary>
    public IReadOnlyList<CaptureChange> Tick(float dt, IReadOnlyList<CaptureCounts> counts)
    {
        var changes = new List<CaptureChange>();
        if (dt <= 0f)
            return changes;

        var byId = new Dictionary<string, CaptureCounts>();
        foreach (var c in counts)
            byId[c.PointId] = c;

        var step = _capture.SecondsToCapture <= 0f ? 1f : dt / _capture.SecondsToCapture;
        foreach (var id in _ids)
        {
            byId.TryGetValue(id, out var c);
            var change = Advance(id, _points[id], c?.West ?? 0, c?.East ?? 0, step);
            if (change != null)
                changes.Add(change);
        }

        return changes;
    }

    /// <summary>
    /// The whole gold the side's owned points earned over <paramref name="dt"/> seconds. The fraction carries to the
    /// next call, so a short tick does not lose income.
    /// </summary>
    public int CollectIncome(MatchSide side, float dt)
    {
        if (dt <= 0f)
            return 0;

        var owned = OwnedBy(side);
        var total = _incomeRemainder[side] + (double)owned * _economy.CaptureIncomePerMinute * dt / 60d;
        var whole = (int)Math.Floor(total + 1e-6);
        _incomeRemainder[side] = Math.Max(0d, total - whole);
        return whole;
    }

    private static CaptureChange? Advance(string id, Point p, int west, int east, float step)
    {
        p.Contested = west > 0 && east > 0;
        p.Rising = false;
        if (p.Contested)
            return null;

        MatchSide? present = west > 0 ? MatchSide.West : east > 0 ? MatchSide.East : null;
        if (present == null || present == p.Owner)
        {
            Drain(p, step);
            return null;
        }

        var side = present.Value;
        if (p.Capturer != null && p.Capturer != side)
        {
            // A rival's unfinished progress is worked off first; what is left counts for the new capturer.
            var left = step - p.Progress;
            Drain(p, step);
            if (p.Capturer != null)
                return null;
            step = Math.Max(0f, left);
        }

        p.Capturer = side;
        p.Progress += step;
        p.Rising = step > 0f;
        if (p.Progress < 1f)
            return null;

        p.Owner = side;
        p.Capturer = null;
        p.Progress = 0f;
        p.Rising = false;
        return new CaptureChange(id, side);
    }

    private static void Drain(Point p, float step)
    {
        if (p.Capturer == null)
            return;

        p.Progress -= step;
        if (p.Progress > 1e-6f)
            return;

        p.Progress = 0f;
        p.Capturer = null;
    }
}
