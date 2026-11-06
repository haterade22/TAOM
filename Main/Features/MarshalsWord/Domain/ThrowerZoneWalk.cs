using System;

namespace TAOM.Features.MarshalsWord.Domain;

/// <summary>
/// The control step's measure (build-spec 9.4) of how a company crossed the gaps at which a run could stand: above CloseMetres and at most the
/// stand gap plus StandArriveMetres, since a run stands within that distance of its point, or at its own place once the point is behind it.
/// Speeds are metres a second between two samples at least a quarter mission second apart, both inside the zone.
/// </summary>
internal sealed class ThrowerZoneWalk
{
    private const double MinSampleSeconds = 0.25;
    private readonly float _top;
    private GroundPoint? _last;
    private double _lastAt;

    internal ThrowerZoneWalk(float standGap) => _top = standGap + MarshalTuning.StandArriveMetres;

    internal float Walked { get; private set; }

    internal float Slowest { get; private set; } = float.NaN;

    internal void Sample(GroundPoint? at, double missionTime, float gap)
    {
        if (at is not { } here || float.IsNaN(here.X) || float.IsNaN(here.Y) || !(gap > MarshalTuning.CloseMetres && gap <= _top))
        {
            _last = null;
            return;
        }

        if (_last is not { } last)
        {
            _last = here;
            _lastAt = missionTime;
            return;
        }

        var elapsed = missionTime - _lastAt;
        if (elapsed < MinSampleSeconds)
            return;
        var dx = here.X - last.X;
        var dy = here.Y - last.Y;
        var metres = (float)Math.Sqrt(dx * dx + dy * dy);
        var speed = (float)(metres / elapsed);
        Walked += metres;
        Slowest = float.IsNaN(Slowest) ? speed : Math.Min(Slowest, speed);
        _last = here;
        _lastAt = missionTime;
    }
}
