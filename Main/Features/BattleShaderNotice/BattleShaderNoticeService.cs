using System;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;

namespace TAOM.Features.BattleShaderNotice;

/// <summary>
/// Battle Shader Notice (docs/features/battle-shader-notice.md). On a battle load that has to compile many shaders,
/// the game thread stops inside one frame until they are done (15 to 110 s on the dev PC), so nothing the game draws
/// can move. This shows the startup shader notice's window, which runs on its own thread and counts the engine's
/// <c>compile_shader</c> log lines, and puts one fixed line on the loading screen, set in the frames before the stop.
/// Called on the game thread only.
/// </summary>
public sealed class BattleShaderNoticeService
{
    private readonly BattleShaderNoticeGate _gate = new();
    private readonly IBattleShaderNoticeRunner _runner;
    private readonly IBattleShaderNoticeTextSource _textSource;
    private readonly IDedicatedServerProvider _server;
    private readonly IModLogger _logger;
    private bool? _isServer;

    public BattleShaderNoticeService(
        IBattleShaderNoticeRunner runner, IBattleShaderNoticeTextSource textSource, IDedicatedServerProvider server, IModLogger logger)
    {
        _runner = runner;
        _textSource = textSource;
        _server = server;
        _logger = logger;
    }

    /// <summary>True from the frame the notice starts until the frame the render wait ends.</summary>
    public bool IsShowing => _gate.Shown;

    /// <summary>The loading screen's line while the notice is up; null otherwise.</summary>
    public string? LoadingScreenLine { get; private set; }

    public void OnMissionStateTick(bool waitingForRender, int queuedShaders)
    {
        _isServer ??= _server.IsDedicatedServer;
        if (_isServer.Value) return;

        switch (_gate.Observe(waitingForRender, queuedShaders))
        {
            case BattleShaderNoticeAction.Start:
                Start(queuedShaders);
                break;
            case BattleShaderNoticeAction.Stop:
                LoadingScreenLine = null;
                _runner.Stop();
                break;
        }
    }

    private void Start(int queuedShaders)
    {
        BattleShaderNoticeTexts texts;
        try
        {
            texts = _textSource.Resolve();
        }
        catch (Exception e)
        {
            _logger.LogWarning($"[BattleShaderNotice] not shown, texts failed: {e.GetType().Name}");
            return;
        }

        LoadingScreenLine = texts.LoadingLine;
        _logger.LogInfo($"[BattleShaderNotice] {queuedShaders} shaders queued for this battle: notice shown");
        try
        {
            _runner.Start(texts.Window);
        }
        catch (Exception e)
        {
            _logger.LogWarning($"[BattleShaderNotice] window not started: {e.GetType().Name}");
        }
    }
}
