using System;

namespace TAOM.Features.ShadowfaxScreens.Diagnostics;

/// <summary>The timer the Harmony patches reach, handed over by the module's static initialisation. A safe no-op while unwired.</summary>
internal static class ScreenOpenTiming
{
    internal static ScreenOpenTimer? Timer { get; private set; }

    internal static void Initialize(ScreenOpenTimer? timer) => Timer = timer;

    internal static ScreenOpenWindow? Begin(string screen, string phase) => Timer?.Begin(screen, phase);

    internal static void End(ScreenOpenWindow? window, Exception? exception, string? detail = null) =>
        Timer?.End(window, exception, detail);
}
