using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Windows.Forms;
using TAOM.Core.Logging;
using TAOM.Features.BattleLoadDiagnostics;
using TAOM.Features.CoopInterop;
using Timer = System.Windows.Forms.Timer;

namespace TAOM.Features.ShaderCompileNotice;

/// <summary>
/// The window over a battle's render wait. The engine's main thread is blocked, so this runs a Windows
/// message loop on a thread of its own while a decision is showing, then exits that thread. It shows
/// what <see cref="RenderWaitNoticeDecider"/> decided from the engine log; it never touches the engine
/// and never changes the process priority. Constructed on the main thread (the freeze watchdog is first
/// resolved at the main menu), which is where its texts and module list are read. Fail-open: when it
/// cannot start it is inert and the sampler carries on.
/// </summary>
public sealed class RenderWaitNoticeHost : IRenderWaitNotice
{
    private const int TickMilliseconds = 300;
    private static readonly TimeSpan CloseAfterHidden = TimeSpan.FromSeconds(2);

    private readonly IModLogger _logger;
    private readonly RenderWaitNoticeTexts? _texts;
    private volatile object _latest = RenderWaitNoticeDecision.Hidden;
    private int _running;

    public RenderWaitNoticeHost(IModLogger logger, IDedicatedServerProvider server)
    {
        _logger = logger;
        try
        {
            if (ShaderCompileProgress.ShouldStart(server.IsDedicatedServer, ShaderCompileNoticeEntry.ActiveModuleIds()))
                _texts = RenderWaitNoticeTexts.Resolve();
        }
        catch (Exception e)
        {
            _logger.LogWarning($"[RenderWaitNotice] not started: {e.GetType().Name}: {e.Message}");
        }
    }

    public void Show(RenderWaitNoticeDecision decision)
    {
        if (_texts == null) return;
        _latest = decision;
        if (decision.Kind == RenderWaitNoticeKind.Hidden) return;
        if (Interlocked.CompareExchange(ref _running, 1, 0) != 0) return;

        try
        {
            var thread = new Thread(Run) { IsBackground = true, Name = "TAOM render-wait notice" };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }
        catch (Exception e)
        {
            Interlocked.Exchange(ref _running, 0);
            _logger.LogWarning($"[RenderWaitNotice] not shown: {e.GetType().Name}: {e.Message}");
        }
    }

    private void Run()
    {
        try
        {
            using var context = new NoticeContext(this);
            Application.Run(context);
        }
        catch (Exception e)
        {
            _logger.LogWarning($"[RenderWaitNotice] stopped after an error: {e.GetType().Name}: {e.Message}");
        }
        finally
        {
            Interlocked.Exchange(ref _running, 0);
        }
    }

    private sealed class NoticeContext : ApplicationContext
    {
        private readonly RenderWaitNoticeHost _host;
        private readonly RenderWaitNoticeTexts _texts;
        private readonly Timer _timer = new() { Interval = TickMilliseconds };
        private readonly Stopwatch _hiddenFor = new();
        private ShaderCompileNoticeWindow? _window;
        private bool _finished;

        public NoticeContext(RenderWaitNoticeHost host)
        {
            _host = host;
            _texts = host._texts!;
            _timer.Tick += (_, _) => Tick();
            _timer.Start();
        }

        private void Tick()
        {
            if (_finished) return;
            try
            {
                var decision = (RenderWaitNoticeDecision)_host._latest;
                if (decision.Kind == RenderWaitNoticeKind.Hidden)
                {
                    if (!_hiddenFor.IsRunning) _hiddenFor.Start();
                    if (_hiddenFor.Elapsed >= CloseAfterHidden) Finish();
                    return;
                }

                _hiddenFor.Reset();
                _window ??= CreateWindow();
                _window.TitleText = Title(decision);
                _window.DetailText = decision.Kind == RenderWaitNoticeKind.Compiling ? _texts.CompilingDetail : _texts.StalledDetail;
            }
            catch (Exception e)
            {
                _host._logger.LogWarning($"[RenderWaitNotice] closed after an error: {e.GetType().Name}: {e.Message}");
                Finish();
            }
        }

        private string Title(RenderWaitNoticeDecision decision)
        {
            return decision.Kind == RenderWaitNoticeKind.Compiling
                ? _texts.CompilingTitle.Replace(RenderWaitNoticeTexts.CountToken, decision.Compiles.ToString(CultureInfo.CurrentCulture))
                : _texts.StalledTitle.Replace(RenderWaitNoticeTexts.SecondsToken, decision.Seconds.ToString(CultureInfo.CurrentCulture));
        }

        private ShaderCompileNoticeWindow CreateWindow()
        {
            string compiling = _texts.CompilingTitle.Replace(RenderWaitNoticeTexts.CountToken, "8888");
            string stalled = _texts.StalledTitle.Replace(RenderWaitNoticeTexts.SecondsToken, "8888");
            string widestTitle = compiling.Length >= stalled.Length ? compiling : stalled;
            string widestDetail = _texts.CompilingDetail.Length >= _texts.StalledDetail.Length ? _texts.CompilingDetail : _texts.StalledDetail;

            using var process = Process.GetCurrentProcess();
            IntPtr game = process.MainWindowHandle;
            var window = new ShaderCompileNoticeWindow(widestTitle, widestDetail, game);
            if (game != IntPtr.Zero) window.Show(new WindowHandleOwner(game));
            else window.Show();
            _host._logger.LogInfo("[RenderWaitNotice] shown");
            return window;
        }

        private void Finish()
        {
            _finished = true;
            _timer.Stop();
            if (_window != null)
            {
                _window.Close();
                _window.Dispose();
                _window = null;
                _host._logger.LogInfo("[RenderWaitNotice] closed");
            }

            ExitThread();
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
    }
}
