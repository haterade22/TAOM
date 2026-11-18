using System;
using TAOM.Core.Logging;
using TAOM.Features.ImeGuard.Domain;

namespace TAOM.Features.ImeGuard;

/// <summary>
/// The IME guard (#1447, ported from Silas He's v1.5.6 fix package). A CJK IME opened its candidate window on hotkeys
/// on the map and in battle. While the game window is in front and no text field has focus, the window's input
/// context is detached; any other state puts it back. Ticked from the client's application tick. A failure puts the
/// context back, logs one warning and stops the guard for the session.
/// </summary>
public sealed class ImeGuardService
{
    private readonly IImeWindowAdapter _window;
    private readonly IImeGuardSettingsProvider _settings;
    private readonly IModLogger _logger;
    private bool _detached;
    private IntPtr _detachedWindow;
    private bool _stopped;

    public ImeGuardService(IImeWindowAdapter window, IImeGuardSettingsProvider settings, IModLogger logger)
    {
        _window = window;
        _settings = settings;
        _logger = logger;
    }

    public void Tick()
    {
        if (_stopped)
            return;
        try
        {
            // The switch first: while it is off, no window call runs at all.
            bool enabled = _settings.Enabled;
            var state = enabled ? _window.Read() : default;
            if (!ImeGuardRules.ShouldDetach(enabled, state.OwnForeground, state.TextFocus))
            {
                Restore();
                return;
            }
            if (_detached && state.Window != _detachedWindow)
                Restore();
            if (_detached)
                return;
            _detached = true;
            _detachedWindow = state.Window;
            _window.Detach(state.Window);
        }
        catch (Exception ex)
        {
            _stopped = true;
            // Put the context back first, whatever the service believes: the adapter's re-attach does nothing when
            // nothing is detached, and a stopped guard must not leave the input method off.
            _detached = false;
            _detachedWindow = IntPtr.Zero;
            try
            {
                _window.Reattach();
            }
            catch
            {
                // swallow-ok: the guard is already off; this runs in the engine's tick, which must not throw.
            }
            _logger.LogWarning($"[ImeGuard] stopped for this session: {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// At unload (ClientSubModule.OnSubModuleUnloaded): stops the guard and has the adapter remove its window thread hook,
    /// whose managed delegate would otherwise be collected by the engine's last GC while the hook still calls it.
    /// </summary>
    public void Shutdown()
    {
        _stopped = true;
        _window.Shutdown();
    }

    /// <summary>Puts back a detached input context; does nothing when none is detached.</summary>
    public void Restore()
    {
        if (!_detached)
            return;
        _detached = false;
        _detachedWindow = IntPtr.Zero;
        _window.Reattach();
    }
}
