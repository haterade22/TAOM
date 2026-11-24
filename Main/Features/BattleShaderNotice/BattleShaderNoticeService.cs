using System;
using TAOM.Adapters;
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
public sealed class BattleShaderNoticeService : IScreenShaderNotice
{
    private readonly BattleShaderNoticeGate _gate = new();
    private readonly BattleShaderNoticeGate _screenGate = new();
    private readonly IBattleShaderNoticeRunner _runner;
    private readonly IBattleShaderNoticeTextSource _textSource;
    private readonly IDedicatedServerProvider _server;
    private readonly IModLogger _logger;
    private readonly IShaderCompileQueueAdapter _queue;
    private bool? _isServer;
    private bool _queueUnreadableReported;

    public BattleShaderNoticeService(
        IBattleShaderNoticeRunner runner, IBattleShaderNoticeTextSource textSource, IDedicatedServerProvider server, IModLogger logger,
        IShaderCompileQueueAdapter queue)
    {
        _runner = runner;
        _textSource = textSource;
        _server = server;
        _logger = logger;
        _queue = queue;
    }

    /// <summary>True from the frame the notice starts until the frame the render wait ends.</summary>
    public bool IsShowing => _gate.Shown;

    /// <summary>The loading screen's line while the notice is up; null otherwise.</summary>
    public string? LoadingScreenLine { get; private set; }

    /// <summary>
    /// The line to write on this frame: <see cref="LoadingScreenLine"/> only while the caller reports the engine's
    /// render wait (the current mission state's first-tick flag with its mission <c>Continuing</c>). Once a mission state
    /// is finalized nothing writes the line again; a line already on the window stays until its next enable clears it.
    /// </summary>
    public string? LineDuringRenderWait(bool missionWaiting) => missionWaiting ? LoadingScreenLine : null;

    /// <summary>Read once and cached. The tick hook asks it before the native shader count, so a server makes no native read.</summary>
    public bool IsDedicatedServer => _isServer ??= _server.IsDedicatedServer;

    public void OnMissionStateTick(bool waitingForRender, int queuedShaders)
    {
        if (IsDedicatedServer) return;

        switch (_gate.Observe(waitingForRender, queuedShaders))
        {
            case BattleShaderNoticeAction.Start:
                Start(queuedShaders, "mission", withLoadingLine: true);
                break;
            case BattleShaderNoticeAction.Stop:
                LoadingScreenLine = null;
                _runner.Stop();
                break;
        }
    }

    /// <summary>
    /// Character creation (Luiz, 2026-11-24): the faction screen's and face generator's 3D characters compile
    /// their shaders on a cold cache. The same window shows from the same 50 queued and closes when the queue is empty
    /// or the screen closes. A battle notice already up owns the window.
    /// </summary>
    public void OnScreenTick(bool screenOpen)
    {
        if (IsDedicatedServer || _gate.Shown) return;
        if (!screenOpen && !_screenGate.Shown) return;

        int queued = screenOpen ? _queue.QueuedShaders() : 0;
        if (queued < 0 && !_queueUnreadableReported)
        {
            _queueUnreadableReported = true;
            _logger.LogWarning("[BattleShaderNotice] shader queue unreadable: no notice on this screen");
        }
        switch (_screenGate.Observe(screenOpen && queued > 0, queued))
        {
            case BattleShaderNoticeAction.Start:
                Start(queued, "screen", withLoadingLine: false);
                break;
            case BattleShaderNoticeAction.Stop:
                _runner.Stop();
                break;
        }
    }

    private void Start(int queuedShaders, string where, bool withLoadingLine)
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

        if (withLoadingLine)
            LoadingScreenLine = texts.LoadingLine;
        _logger.LogInfo($"[BattleShaderNotice] {queuedShaders} shaders queued for this {where}: notice started");
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
