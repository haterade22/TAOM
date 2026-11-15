namespace TAOM.Features.BattleShaderNotice;

/// <summary>What the battle shader notice does on one mission frame.</summary>
public enum BattleShaderNoticeAction
{
    None,
    Start,
    Stop,
}

/// <summary>
/// The decision behind the battle shader notice, one call per mission frame. A battle load whose render wait
/// (<c>MissionState.FirstMissionTickAfterLoading</c> with the mission <c>Continuing</c>) starts with at least
/// <see cref="ShowFromQueued"/> shaders queued gets the notice once; the first frame that is no longer waiting
/// takes it down. Pure and single-threaded: the game thread is its only caller.
/// </summary>
public sealed class BattleShaderNoticeGate
{
    /// <summary>
    /// Warm battle loads on the dev PC queue 0 to 20 shaders (taom_debug logs 14-15 Nov 2026); a cold one after a
    /// mod-list change queued 1,192 and held the game for 45 s. 50 sits clear of the warm loads.
    /// </summary>
    public const int ShowFromQueued = 50;

    public bool Shown { get; private set; }

    /// <param name="waitingForRender">The mission is loaded and held only by the render-ready check.</param>
    /// <param name="queuedShaders">The engine's queued shader count, or -1 when it was not read.</param>
    public BattleShaderNoticeAction Observe(bool waitingForRender, int queuedShaders)
    {
        if (!waitingForRender)
        {
            if (!Shown) return BattleShaderNoticeAction.None;
            Shown = false;
            return BattleShaderNoticeAction.Stop;
        }

        if (Shown || queuedShaders < ShowFromQueued) return BattleShaderNoticeAction.None;
        Shown = true;
        return BattleShaderNoticeAction.Start;
    }
}
