using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Forms;
using TAOM.Core.Logging;
using Timer = System.Windows.Forms.Timer;

namespace TAOM.Features.ShaderCompileNotice;

/// <summary>
/// The shader notice's window and message loop (WinForms), run on the STA thread that
/// <see cref="ShaderCompileNotice"/> starts. Moved out of that class's nested NoticeContext (ADR-013): the static class
/// stays in TAOM.dll for the name contract and reaches this through <see cref="IShaderCompileNoticeWindowHost"/>.
/// </summary>
internal sealed class ShaderCompileNoticeContext : ApplicationContext
{
    private const int TickMilliseconds = 300;
    private const int MaxReadBytes = 4 << 20;

    private readonly ShaderCompileNoticeTexts _texts;
    private readonly ShaderCompileNoticeMode _mode;
    private readonly IModLogger _logger;
    private readonly Func<bool> _stopRequested;
    private readonly ShaderCompileProgress _progress = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Timer _timer = new() { Interval = TickMilliseconds };
    private readonly string _log;
    private readonly string _totalFile;
    private readonly int _expectedTotal;
    private long _position;
    private int _lastCount;
    private TimeSpan _lastCompileAt;
    private ShaderCompileNoticeWindow? _window;
    private ProcessPriorityClass? _priorityBefore;
    private bool _finished;

    public ShaderCompileNoticeContext(
        ShaderCompileNoticeTexts texts, ShaderCompileNoticeMode mode, IModLogger logger, Func<bool> stopRequested)
    {
        _texts = texts;
        _mode = mode;
        _stopRequested = stopRequested;
        _logger = logger;
        string data = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Mount and Blade II Bannerlord");
        _log = Path.Combine(data, "logs", $"rgl_log_{Process.GetCurrentProcess().Id}.txt");
        _totalFile = Path.Combine(data, "Shaders", "taom_shader_compile_total.txt");
        _expectedTotal = mode.UseRememberedTotal ? ShaderCompileProgress.ParseRememberedTotal(TryReadAll(_totalFile)) : 0;
        _position = mode.StartPosition(TryLength(_log));
        _timer.Tick += (_, _) => Tick();
        _timer.Start();
    }

    private void Tick()
    {
        if (_finished) return;
        try
        {
            if (_stopRequested() || _clock.Elapsed > _mode.GiveUpAfter)
            {
                Finish();
                return;
            }

            ReadNewLogText();
            if (_progress.Count != _lastCount)
            {
                _lastCount = _progress.Count;
                _lastCompileAt = _clock.Elapsed;
            }

            if (_mode.ShouldCloseIdle(_window != null, _clock.Elapsed - _lastCompileAt))
            {
                _logger.LogInfo($"[{_mode.LogTag}] no shader compiled for {(_clock.Elapsed - _lastCompileAt).TotalSeconds:0} s, closing");
                Finish();
                return;
            }

            if (_window == null && _mode.ShouldShow(_progress, _clock.Elapsed)) ShowWindow();
            if (_window != null) _window.TitleText = _progress.Title(_texts, _expectedTotal);
        }
        catch (Exception e)
        {
            _logger.LogWarning($"[{_mode.LogTag}] closed after an error: {e.GetType().Name}: {e.Message}");
            Finish();
        }
    }

    private void ReadNewLogText()
    {
        if (!File.Exists(_log)) return;
        using var stream = new FileStream(_log, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (stream.Length <= _position) return;
        stream.Position = _position;
        var bytes = new byte[(int)Math.Min(stream.Length - _position, MaxReadBytes)];
        int read = stream.Read(bytes, 0, bytes.Length);
        _position += read;
        _progress.Feed(Encoding.ASCII.GetString(bytes, 0, read));
    }

    private void ShowWindow()
    {
        IntPtr game = Process.GetCurrentProcess().MainWindowHandle;
        string widest = _texts.TitleWithTotal
            .Replace(ShaderCompileNoticeTexts.CountToken, "8888")
            .Replace(ShaderCompileNoticeTexts.TotalToken, Math.Max(_expectedTotal, 8888).ToString());
        _window = new ShaderCompileNoticeWindow(widest, _texts.Detail, game);
        if (game != IntPtr.Zero) _window.Show(new WindowHandleOwner(game));
        else _window.Show();
        if (_mode.LowerPriority) LowerPriority();
        _logger.LogInfo(_mode.UseRememberedTotal
            ? $"[{_mode.LogTag}] the engine is compiling its shaders: notice shown, expected about {_expectedTotal}"
            : $"[{_mode.LogTag}] window shown after {_clock.Elapsed.TotalSeconds:0.0} s, {_progress.Count} compiled so far");
    }

    private void Finish()
    {
        _finished = true;
        _timer.Stop();
        if (_window != null)
        {
            _logger.LogInfo($"[{_mode.LogTag}] {_progress.Count} shaders compiled, notice closed after {_clock.Elapsed.TotalSeconds:0} s");
            if (_mode.UseRememberedTotal && _progress.ShouldRememberTotal) TryWrite(_totalFile, _progress.Count.ToString());
            _window.Close();
            _window.Dispose();
            _window = null;
        }

        RestorePriority();
        ExitThread();
    }

    private void LowerPriority()
    {
        try
        {
            var process = Process.GetCurrentProcess();
            if (process.PriorityClass != ProcessPriorityClass.Normal) return;   // set on purpose by someone: leave it
            _priorityBefore = process.PriorityClass;
            process.PriorityClass = ProcessPriorityClass.BelowNormal;
        }
        catch (Exception e)
        {
            _logger.LogWarning($"[ShaderNotice] priority not lowered: {e.Message}");
        }
    }

    private void RestorePriority()
    {
        if (!_priorityBefore.HasValue) return;
        try { Process.GetCurrentProcess().PriorityClass = _priorityBefore.Value; }
        catch (Exception e) { _logger.LogWarning($"[ShaderNotice] priority not restored: {e.Message}"); }
        _priorityBefore = null;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
            _window?.Dispose();
        }

        base.Dispose(disposing);
    }

    private static long TryLength(string path)
    {
        try { return File.Exists(path) ? new FileInfo(path).Length : -1L; }
        catch (IOException) { return -1L; }
        catch (UnauthorizedAccessException) { return -1L; }
    }

    private static string? TryReadAll(string path)
    {
        try { return File.Exists(path) ? File.ReadAllText(path) : null; }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    private void TryWrite(string path, string text)
    {
        try { File.WriteAllText(path, text); }
        catch (Exception e) { _logger.LogWarning($"[ShaderNotice] total not remembered: {e.Message}"); }
    }
}
