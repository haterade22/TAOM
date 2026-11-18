using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using TAOM.Core.Logging;
using TAOM.Features.ImeGuard;
using TaleWorlds.ScreenSystem;

namespace TAOM.Client.Features.ImeGuard;

/// <summary>
/// The IME guard's Windows and screen calls (#1447, ported from Silas He's v1.5.6 fix package).
///
/// Windows re-attaches a saved input context, and wakes the IME for it, only when the call comes from the thread that
/// owns the window. The engine creates and pumps the game window on its own "Message Pump" thread, not the tick thread,
/// so the context calls are queued to the window thread through a thread message hook (WH_GETMESSAGE) and an empty
/// message that wakes it. Without the hook they run on the tick thread: Windows then ignores the re-attach and the window
/// keeps no context, so the lease attaches the window's default context (IACE_DEFAULT), which the IME may not wake for. Each kind of outcome is logged once. The hook stays
/// installed until <see cref="Shutdown"/> at unload, also with the switch off; it then only passes each message on.
/// </summary>
internal sealed class ImeWindowAdapter : IImeWindowAdapter
{
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassNameW(IntPtr window, StringBuilder name, int size);

    // The engine's game window class (TaleWorlds.Native creates it on its "Message Pump" thread).
    private const string GameWindowClass = "TWApplication";

    private readonly IModLogger _logger;
    private readonly uint _process = (uint)Process.GetCurrentProcess().Id;
    private readonly ImeContextLease _lease = new();
    private WindowThreadDispatcher? _dispatcher;
    private IntPtr _window;
    // Each kind of outcome is logged once: the window thread writes it, the tick thread never reads it.
    private readonly HashSet<string> _logged = new();
    private bool _dispatchReadyLogged, _dispatchMissingLogged;

    public ImeWindowAdapter(IModLogger logger) => _logger = logger;

    public ImeWindowState Read()
    {
        IntPtr foreground = GetForegroundWindow();
        GetWindowThreadProcessId(foreground, out uint owner);
        bool own = foreground != IntPtr.Zero && owner == _process && IsGameWindow(foreground);
        return new ImeWindowState(foreground, own, own && TextFocus());
    }

    // Only the game window: another window of this process (a console, a notice) keeps its own context.
    private static bool IsGameWindow(IntPtr window)
    {
        var name = new StringBuilder(32);
        return GetClassNameW(window, name, name.Capacity) > 0 && name.ToString() == GameWindowClass;
    }

    // An edit box with keyboard focus on the focused layer, or on a global layer: native text popups (a clan or
    // kingdom name) sit on a global layer that does not always become the focused layer. A top-screen layer that is not
    // the focused layer can keep a stale focused widget, so it does not count.
    private static bool TextFocus()
    {
        if (ScreenManager.FocusedLayer?.IsFocusedOnInput() ?? false)
            return true;
        var top = ScreenManager.TopScreen;
        foreach (var layer in ScreenManager.SortedLayers)
        {
            if (layer == null || (top != null && top.Layers.Contains(layer)))
                continue;
            if (layer.IsFocusedOnInput())
                return true;
        }
        return false;
    }

    public void Detach(IntPtr window)
    {
        _window = window;
        Run(window, () =>
        {
            if (_lease.Suspend(window))
                LogOnce("paused", "[ImeGuard] input method paused while no text field has focus");
            else if (!_lease.Suspended)
                LogOnce("no_context", "[ImeGuard] the game window has no input context to pause");
        });
    }

    public void Reattach()
    {
        Run(_window, () =>
        {
            if (!_lease.Suspended)
                return;
            string how = _lease.Restore();
            LogOnce("restored " + how, $"[ImeGuard] input method restored: {how}");
        });
        _window = IntPtr.Zero;
    }

    public void Shutdown()
    {
        _dispatcher?.Dispose();
        _dispatcher = null;
    }

    private void LogOnce(string kind, string line)
    {
        if (_logged.Add(kind))
            _logger.LogInfo(line);
    }

    private void Run(IntPtr window, Action action)
    {
        uint thread = GetWindowThreadProcessId(window, out _);
        if (thread == 0 || thread == GetCurrentThreadId() || !Dispatcher(thread))
        {
            action();
            return;
        }
        _dispatcher!.Post(window, action);
    }

    private bool Dispatcher(uint thread)
    {
        if (_dispatcher != null && _dispatcher.Thread == thread)
            return true;
        _dispatcher?.Dispose();
        _dispatcher = WindowThreadDispatcher.TryCreate(thread, _logger);
        if (_dispatcher != null && !_dispatchReadyLogged)
        {
            _dispatchReadyLogged = true;
            _logger.LogInfo("[ImeGuard] window thread hook ready");
        }
        else if (_dispatcher == null && !_dispatchMissingLogged)
        {
            _dispatchMissingLogged = true;
            _logger.LogInfo("[ImeGuard] window thread hook unavailable; the context calls run on the tick thread");
        }
        return _dispatcher != null;
    }
}

/// <summary>Runs queued calls on one thread of this process, from inside its message loop (WH_GETMESSAGE).</summary>
internal sealed class WindowThreadDispatcher : IDisposable
{
    private delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int id, HookProc proc, IntPtr module, uint thread);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

    private const int WhGetMessage = 3;
    private const uint WmNull = 0;

    // Held in a field: the hook keeps only a native pointer, so a collected delegate would crash the window thread.
    private readonly HookProc _proc;
    private readonly ConcurrentQueue<Action> _work = new();
    private readonly IModLogger _logger;
    private IntPtr _hook;
    private bool _failureLogged;

    private WindowThreadDispatcher(uint thread, IModLogger logger)
    {
        Thread = thread;
        _logger = logger;
        _proc = OnMessage;
    }

    public uint Thread { get; }

    public static WindowThreadDispatcher? TryCreate(uint thread, IModLogger logger)
    {
        var dispatcher = new WindowThreadDispatcher(thread, logger);
        // A thread of this process with the hook in this process's code: no module handle (SetWindowsHookEx docs).
        dispatcher._hook = SetWindowsHookEx(WhGetMessage, dispatcher._proc, IntPtr.Zero, thread);
        return dispatcher._hook != IntPtr.Zero ? dispatcher : null;
    }

    /// <summary>Queues the call; the empty message makes the window thread run it at once, with no input arriving.</summary>
    public void Post(IntPtr window, Action action)
    {
        _work.Enqueue(action);
        PostMessage(window, WmNull, IntPtr.Zero, IntPtr.Zero);
    }

    private IntPtr OnMessage(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0)
        {
            while (_work.TryDequeue(out var action))
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    // swallow-ok: a throw here would unwind the engine's message loop; logged once.
                    if (!_failureLogged)
                    {
                        _failureLogged = true;
                        _logger.LogWarning($"[ImeGuard] a window thread call failed: {ex.GetType().Name}: {ex.Message}");
                    }
                }
            }
        }
        return CallNextHookEx(_hook, code, wParam, lParam);
    }

    public void Dispose()
    {
        if (_hook == IntPtr.Zero)
            return;
        UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
    }
}

/// <summary>
/// The game window's input context, detached and kept. The context stays owned by the window; this only moves it off
/// and back.
/// </summary>
internal sealed class ImeContextLease
{
    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr window);
    [DllImport("imm32.dll")] private static extern IntPtr ImmAssociateContext(IntPtr window, IntPtr context);
    [DllImport("imm32.dll")] private static extern bool ImmAssociateContextEx(IntPtr window, IntPtr context, uint flags);
    [DllImport("imm32.dll")] private static extern IntPtr ImmGetContext(IntPtr window);
    [DllImport("imm32.dll")] private static extern bool ImmReleaseContext(IntPtr window, IntPtr context);

    private const uint IaceDefault = 0x10;

    private IntPtr _window;
    private IntPtr _context;

    public bool Suspended { get; private set; }

    public bool Suspend(IntPtr window)
    {
        if (Suspended || !IsWindow(window))
            return false;
        IntPtr old = ImmAssociateContext(window, IntPtr.Zero);
        if (old == IntPtr.Zero)
            return false;
        _window = window;
        _context = old;
        Suspended = true;
        return true;
    }

    /// <summary>
    /// "original" when the kept context is back (a call from the window's own thread); "default" when Windows ignored
    /// that and the window's default context was attached instead; "window_gone" or "FAILED" otherwise.
    /// </summary>
    public string Restore()
    {
        if (!Suspended)
            return "not_suspended";
        string how = "window_gone";
        if (IsWindow(_window))
        {
            ImmAssociateContext(_window, _context);
            if (Current(_window) == _context)
                how = "original";
            else if (ImmAssociateContextEx(_window, IntPtr.Zero, IaceDefault) && Current(_window) != IntPtr.Zero)
                how = "default";
            else
                how = "FAILED";
        }
        _window = IntPtr.Zero;
        _context = IntPtr.Zero;
        Suspended = false;
        return how;
    }

    private static IntPtr Current(IntPtr window)
    {
        IntPtr value = ImmGetContext(window);
        if (value != IntPtr.Zero)
            ImmReleaseContext(window, value);
        return value;
    }
}
