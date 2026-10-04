using System;
using TAOM.Core.Logging;
using TAOM.Features.BattleLoadDiagnostics;

namespace TAOM.Features.LoadTimeStamps;

/// <summary>
/// Reads "Enable Load-Time Stamps" for every detailed stamp and logs its state (on, off, or
/// unreadable) whenever it differs from the last one logged, so a log always says whether the
/// detailed lines are missing because the toggle is off, or because it could not be read (a WARNING
/// naming the exception).
/// </summary>
public sealed class LoadStampDetailGate
{
    private readonly IBattleLoadDiagnosticsSettingsProvider _settings;
    private readonly IModLogger _logger;

    public LoadStampDetailGate(IBattleLoadDiagnosticsSettingsProvider settings, IModLogger logger)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    private enum Logged { Nothing, On, Off, Unreadable }

    private readonly object _sync = new();
    private Logged _lastLogged = Logged.Nothing;

    /// <summary>The toggle; false when the read throws (MCM not ready, or a broken settings file), and
    /// that turning the detail off is logged with its reason.</summary>
    public bool Enabled
    {
        get
        {
            bool value;
            Exception? unreadable = null;
            try
            {
                value = _settings.LoadTimeStampsEnabled;
            }
            catch (Exception ex)
            {
                value = false;
                unreadable = ex;
            }

            // The reason counts, not only the value: an unreadable off and a readable off log apart.
            var state = unreadable != null ? Logged.Unreadable : value ? Logged.On : Logged.Off;
            lock (_sync)
            {
                if (_lastLogged != state)
                {
                    _lastLogged = state;
                    if (unreadable != null) _logger.LogWarning(LoadTimeStampLines.DetailUnreadable(unreadable));
                    else _logger.LogInfo(LoadTimeStampLines.Detail(value));
                }
            }

            return value;
        }
    }
}
