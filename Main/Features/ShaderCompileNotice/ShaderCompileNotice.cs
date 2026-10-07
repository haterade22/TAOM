using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using TAOM.Core.Logging;
using Timer = System.Windows.Forms.Timer;

namespace TAOM.Features.ShaderCompileNotice;

/// <summary>
/// While the engine rebuilds its runtime shader cache before the main menu (about 1,340 shaders after
/// a module-list change, every core busy, the loading symbol frozen for a minute or more), shows a
/// small window over the game with the progress and runs the game at BelowNormal priority so the rest
/// of the computer stays usable. Started from <c>SubModule.OnSubModuleLoad</c>, about six seconds
/// before the first compile; stopped at the first <c>OnBeforeInitialModuleScreenSetAsRoot</c>.
/// </summary>
/// <remarks>
/// The engine's main thread is blocked while it compiles, so nothing can be drawn inside the game:
/// this runs a Windows message loop on a thread of its own and reads the progress from the engine's
/// own log, one <c>compile_shader</c> line per shader in <c>rgl_log_&lt;pid&gt;.txt</c>. It never
/// touches the engine. Idea from yotthani's VanillaTuning (docs/features/shader-compile-notice.md).
/// </remarks>
public static class ShaderCompileNotice
{
    private const int TickMilliseconds = 300;
    private const int MaxReadBytes = 4 << 20;
    private static readonly TimeSpan GiveUpAfter = TimeSpan.FromMinutes(30);

    private static int _started;
    private static volatile bool _stopRequested;

    public static void Start(ShaderCompileNoticeTexts texts, IModLogger logger)
    {
        if (Interlocked.Exchange(ref _started, 1) == 1) return;
        var thread = new Thread(() => Run(texts, logger)) { IsBackground = true, Name = "TAOM shader compile notice" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }

    /// <summary>Idempotent; the main menu calls it every time it is shown.</summary>
    public static void Stop() => _stopRequested = true;

    private static void Run(ShaderCompileNoticeTexts texts, IModLogger logger)
    {
        try
        {
            using var context = new NoticeContext(texts, logger);
            Application.Run(context);
        }
        catch (Exception e)
        {
            logger.LogWarning($"[ShaderNotice] stopped after an error: {e.GetType().Name}: {e.Message}");
        }
    }

    private sealed class NoticeContext : ApplicationContext
    {
        private readonly ShaderCompileNoticeTexts _texts;
        private readonly IModLogger _logger;
        private readonly ShaderCompileProgress _progress = new();
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private readonly Timer _timer = new() { Interval = TickMilliseconds };
        private readonly string _log;
        private readonly string _totalFile;
        private readonly int _expectedTotal;
        private long _position;
        private ShaderCompileNoticeWindow _window;
        private ProcessPriorityClass? _priorityBefore;
        private bool _finished;

        public NoticeContext(ShaderCompileNoticeTexts texts, IModLogger logger)
        {
            _texts = texts;
            _logger = logger;
            string data = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Mount and Blade II Bannerlord");
            _log = Path.Combine(data, "logs", $"rgl_log_{Process.GetCurrentProcess().Id}.txt");
            _totalFile = Path.Combine(data, "Shaders", "taom_shader_compile_total.txt");
            _expectedTotal = ShaderCompileProgress.ParseRememberedTotal(TryReadAll(_totalFile));
            _timer.Tick += (_, _) => Tick();
            _timer.Start();
        }

        private void Tick()
        {
            if (_finished) return;
            try
            {
                if (_stopRequested || _clock.Elapsed > GiveUpAfter)
                {
                    Finish();
                    return;
                }

                ReadNewLogText();
                if (_window == null && _progress.ShouldShow) ShowWindow();
                if (_window != null) _window.TitleText = _progress.Title(_texts, _expectedTotal);
            }
            catch (Exception e)
            {
                _logger.LogWarning($"[ShaderNotice] closed after an error: {e.GetType().Name}: {e.Message}");
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
            LowerPriority();
            _logger.LogInfo($"[ShaderNotice] the engine is compiling its shaders: notice shown, expected about {_expectedTotal}");
        }

        private void Finish()
        {
            _finished = true;
            _timer.Stop();
            if (_window != null)
            {
                _logger.LogInfo($"[ShaderNotice] {_progress.Count} shaders compiled, notice closed after {_clock.Elapsed.TotalSeconds:0} s");
                if (_progress.ShouldRememberTotal) TryWrite(_totalFile, _progress.Count.ToString());
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

        private static string TryReadAll(string path)
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
}
