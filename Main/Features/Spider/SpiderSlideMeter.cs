using System.Collections.Generic;

namespace TAOM.Features.Spider;

/// <summary>
/// Adds up the seconds in which a fast spider's leg tips hardly move against its body: a slide, which a speed cannot show
/// (#1559; the measurements are in docs/features/spider.md). It is keyed by the mount object, never by an agent index, which the engine gives away again. A window is
/// <see cref="WindowSeconds"/> of FAST readings of one mount: a slow reading is left out, and it ends the window only when
/// the mount has been slow for longer than <see cref="GapSeconds"/>. So a spider that dips under the speed for a tick cannot
/// hide the slide around it, and a run from before a halt is not weighed with a slide after it. <see cref="JudgedSeconds"/> says how much was
/// judged at all: a sum of 0 from a meter that judged nothing is no measurement.
/// </summary>
internal sealed class SpiderSlideMeter
{
    /// <summary>Under this speed a mount is not judged: a walk's in-place twin moves the tips little, and that is no slide.</summary>
    internal const float FastMetresPerSecond = 4f;

    /// <summary>Real seconds of one judged window.</summary>
    internal const float WindowSeconds = 0.5f;

    /// <summary>A window slides when the tips moved less than this share of the way the mount went: a slide reads 0.17, a gait 0.7 and more.</summary>
    internal const float LegShare = 0.3f;

    /// <summary>Two readings of one mount farther apart than this, or with a slow span longer than this between them, do not share a window.</summary>
    internal const float GapSeconds = 0.2f;

    private sealed class Window
    {
        internal float LastAt;
        internal float LastFastAt;
        internal float Seconds;
        internal float Body;
        internal float Legs;

        internal void Clear() => Seconds = Body = Legs = 0f;
    }

    private readonly Dictionary<object, Window> _windows = new();

    /// <summary>The seconds of every window that slid, summed over all mounts.</summary>
    internal float SlideSeconds { get; private set; }

    /// <summary>The seconds of every window that was judged, slid or not, summed over all mounts.</summary>
    internal float JudgedSeconds { get; private set; }

    /// <summary>
    /// One reading of one mount: the time, its speed in metres per second, and the mean metres its leg tips moved against its
    /// body since its last reading. A late reading and a leg travel that is no number drop the mount's open window; a reading
    /// under the speed is left out of it.
    /// </summary>
    internal void Add(object mount, float now, float speed, float legTravel)
    {
        if (!_windows.TryGetValue(mount, out var window))
        {
            _windows[mount] = new Window { LastAt = now, LastFastAt = now };
            return;
        }

        var passed = now - window.LastAt;
        window.LastAt = now;
        if (!(passed > 0f) || passed > GapSeconds || !(legTravel >= 0f) || float.IsInfinity(legTravel))
        {
            window.Clear();
            return;
        }

        if (!(speed >= FastMetresPerSecond) || float.IsInfinity(speed))
        {
            if (now - window.LastFastAt > GapSeconds)
                window.Clear();
            return;
        }

        window.LastFastAt = now;
        window.Seconds += passed;
        window.Body += speed * passed;
        window.Legs += legTravel;
        if (window.Seconds < WindowSeconds)
            return;

        JudgedSeconds += window.Seconds;
        if (window.Legs < LegShare * window.Body)
            SlideSeconds += window.Seconds;
        window.Clear();
    }
}
