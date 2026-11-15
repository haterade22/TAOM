using System;
using System.Threading;
using TAOM.Core.Logging;
using TAOM.Features.ShaderCompileNotice;

namespace TAOM.Features.BattleShaderNotice;

/// <summary>
/// Runs the shader notice window in <see cref="ShaderCompileNoticeMode.Battle"/> on an STA thread of its own, once per
/// battle load. The game thread is stopped inside one frame for most of the wait, so the window must not need it:
/// the host reads the engine's log and never touches the engine. Separate from the startup notice's
/// <see cref="ShaderCompileNotice.ShaderCompileNotice"/>, which runs once per process and keeps its name for
/// VanillaTuning.
/// </summary>
internal sealed class BattleShaderNoticeRunner : IBattleShaderNoticeRunner
{
    private readonly IShaderCompileNoticeWindowHost _host;
    private readonly IModLogger _logger;
    private Run? _current;

    public BattleShaderNoticeRunner(IShaderCompileNoticeWindowHost host, IModLogger logger)
    {
        _host = host;
        _logger = logger;
    }

    public void Start(ShaderCompileNoticeTexts texts)
    {
        // The previous battle's window closes within one 300 ms tick of its stop; a second load inside that tick
        // keeps its loading-screen line and goes without the window.
        if (_current is { Ended: false }) return;
        var run = new Run();
        _current = run;
        var thread = new Thread(() => Execute(run, texts)) { IsBackground = true, Name = "TAOM battle shader notice" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }

    public void Stop()
    {
        var run = _current;
        if (run != null) run.StopRequested = true;
    }

    private void Execute(Run run, ShaderCompileNoticeTexts texts)
    {
        try
        {
            _host.Run(texts, ShaderCompileNoticeMode.Battle, _logger, () => run.StopRequested);
        }
        catch (Exception e)
        {
            _logger.LogWarning($"[BattleShaderNotice] stopped after an error: {e.GetType().Name}");
        }
        finally
        {
            run.Ended = true;
        }
    }

    private sealed class Run
    {
        public volatile bool StopRequested;
        public volatile bool Ended;
    }
}
