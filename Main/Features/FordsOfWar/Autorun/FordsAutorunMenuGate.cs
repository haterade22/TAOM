using System;

namespace TAOM.Features.FordsOfWar.Autorun;

/// <summary>
/// Waits until the main menu has been up for more than <see cref="SettleSeconds"/> seconds, hiding any inquiry that appears meanwhile
/// (a boot notice nobody will click). Leaving the menu restarts the wait.
/// </summary>
internal sealed class FordsAutorunMenuGate
{
    internal const double SettleSeconds = 4.0;
    private const int HiddenLogCap = 20;

    private double _seconds;
    private int _hidden;

    /// <summary>True once the menu has settled; <paramref name="log"/> receives one line per hidden inquiry, capped.</summary>
    internal bool Settled(IFordsAutorunHost host, float dt, Action<string> log)
    {
        if (!host.AtMainMenu)
        {
            _seconds = 0.0;
            return false;
        }

        _seconds += dt;
        if (host.HideAnyInquiry() && ++_hidden <= HiddenLogCap)
            log("inquiry hidden n=" + _hidden + " phase=WaitMenu");

        return _seconds > SettleSeconds;
    }
}
