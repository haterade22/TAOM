using System.Collections.Generic;

namespace TAOM.Features.FordsOfWar.Diagnostics;

/// <summary>The members TAOM.Client calls. Each does nothing before the first run.</summary>
internal sealed partial class FordsRunReport
{
    private static int Count(Dictionary<string, int> map, string key) => map.TryGetValue(key, out var n) ? n : 0;

    /// <summary>Hot, throw-free: a few counters per frame.</summary>
    internal void CameraFrame(bool pan, bool rotate, bool zoom, bool moved, bool turned, bool zoomed)
    {
        var run = _run;
        if (run == null)
            return;

        var c = run.Client;
        c.OwnedFrames++;
        if (pan)
        {
            c.PanInputFrames++;
            if (moved)
                c.PanMovedFrames++;
        }

        if (rotate)
        {
            c.RotateInputFrames++;
            if (turned)
                c.TurnedFrames++;
        }

        if (zoom)
        {
            c.ZoomInputFrames++;
            if (zoomed)
                c.ZoomedFrames++;
        }

        if (c.OwnedFrames == 1)
            Info("Camera", "first owned frame");
    }

    internal void CameraInside(bool inside)
    {
        var run = _run;
        if (run == null)
            return;

        var c = run.Client;
        if (inside)
        {
            c.InsideSamples++;
            c.OutsideRun = 0;
            return;
        }

        c.OutsideSamples++;
        c.OutsideRun++;
        if (c.OutsideRun > c.MaxOutsideRun)
            c.MaxOutsideRun = c.OutsideRun;
    }

    internal void CameraFailed() => Guard(nameof(CameraFailed), () =>
    {
        var run = _run;
        if (run == null)
            return;

        run.Client.CameraFailed = true;
        Warn("Camera", "failed; the commander camera gave up");
    });

    internal void PhotoVetoed()
    {
        var run = _run;
        if (run == null)
            return;

        if (++run.Client.PhotoVetoes == 1)
            Info("Camera", "first photo veto");
    }

    /// <summary>Hot: one compare per frame; counts a false-to-true edge and logs edges.</summary>
    internal void Paused(bool paused)
    {
        var run = _run;
        if (run == null)
            return;

        var c = run.Client;
        if (c.LastPaused == paused)
            return;

        c.LastPaused = paused;
        if (paused)
            c.PauseEdges++;
        var n = NextOrdinal(run, FordsLogCategory.Pause);
        if (n <= PauseSampleCap)
            Info("EscMenu", "paused=" + (paused ? "true" : "false"));
        CappedLine(FordsLogCategory.Pause, n);
    }

    internal void PanelAttached()
    {
        var run = _run;
        if (run != null)
            run.Client.PanelAttached = true;
    }

    internal void Command(string action, string result)
    {
        var run = _run;
        if (run == null)
            return;

        var key = action + ":" + result;
        run.Client.Commands[key] = Count(run.Client.Commands, key) + 1;
    }

    internal void PackBoughtLine()
    {
        var run = _run;
        if (run != null)
            run.Client.PackBoughtLines++;
    }
}
