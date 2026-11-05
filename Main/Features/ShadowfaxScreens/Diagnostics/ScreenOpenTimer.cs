using System;
using System.Globalization;
using TAOM.Core.Diagnostics;
using TAOM.Core.Logging;
using TAOM.Features.LoadTimeStamps;

namespace TAOM.Features.ShadowfaxScreens.Diagnostics;

/// <summary>
/// Times one phase of a screen open and writes one <c>[ScreenOpen] {screen} {phase}: {ms} ms[, threw T][ ({detail})]</c>
/// line to taom_debug.log. Off by default ("Log Screen Open Times"): <see cref="Begin"/> then returns null without reading
/// the clock, and <see cref="End"/> ignores a null window. Neither method ever throws, because a diagnostic must not
/// break the screen it measures. A phase that runs many times per open is not a window: its service totals it and passes
/// the totals as <c>detail</c> on the screen's open line.
/// </summary>
public sealed class ScreenOpenTimer
{
    private readonly IStampClock _clock;
    private readonly IModLogger _logger;
    private readonly IShadowfaxScreensSettingsProvider _settings;

    public ScreenOpenTimer(IStampClock clock, IModLogger logger, IShadowfaxScreensSettingsProvider settings)
    {
        _clock = clock;
        _logger = logger;
        _settings = settings;
    }

    /// <summary>Starts a window, or returns null when the timer is off or unreadable.</summary>
    public ScreenOpenWindow? Begin(string screen, string phase)
    {
        try
        {
            return _settings.LogScreenOpenTimes ? new ScreenOpenWindow(screen, phase, _clock.Now) : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Writes the window's one line; a null or already-ended window writes nothing.</summary>
    public void End(ScreenOpenWindow? window, Exception? exception, string? detail = null)
    {
        if (window == null || window.Ended) return;
        window.Ended = true;
        try
        {
            var ms = LoadTimeStampLines.ToMs(_clock.Now - window.StartTick, _clock.Frequency);
            var line = string.Format(CultureInfo.InvariantCulture, "[ScreenOpen] {0} {1}: {2:0.0} ms", window.Screen, window.Phase, ms);
            if (exception != null) line += ", threw " + exception.GetType().Name;
            if (!string.IsNullOrEmpty(detail)) line += " (" + detail + ")";
            _logger.LogInfo(line);
        }
        catch
        {
            // The measurement must never be the thing that breaks the screen.
        }
    }
}
