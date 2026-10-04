using System.Threading;
using TAOM.Core.Logging;

namespace TAOM.Features.TrollBruteForce;

/// <summary>
/// Whether the current mission has built a Brute Force troll (a Monster <see cref="ITrollBruteForceService.IsBruteForceTroll"/>
/// accepts). <see cref="TrollBruteForceMissionBehavior"/> ticks its formation-spacing tracker and clip trace only once
/// this is set, so a mission with no troll pays for neither agent scan; both write nothing in such a mission anyway
/// (no formation reaches the troll share, no troll is listed). A latch: a troll's death does not clear it, only
/// <see cref="Clear"/> at mission end. Set from OnAgentBuild and the first tick, read on the main thread.
/// Each tracker's 0.5 s stride therefore starts at its first tick, not at the mission's first tick. The first scan
/// after the first troll is built comes on the next tick: up to 0.5 s sooner than the old clock, never later. Every
/// later scan keeps the new phase, so a re-space that falls due after that (a formation that reaches the troll share
/// only once more trolls are built, a reinforcement troll's formation) can land up to about 0.5 s sooner or later
/// than before. The spacing tracker moves real formations, so this is a gameplay timing change, not only a logging one.
/// Logs one INFO line when the first troll sets it, and one at <see cref="Clear"/> when no troll ever did, naming
/// what was skipped (plan 030, DECISIONS D6).
/// </summary>
public sealed class TrollPresence
{
    private readonly ITrollBruteForceService _service;
    private readonly IModLogger _logger;
    // 0 or 1. An int so the first setter wins one CompareExchange and logs once, whichever thread raised the build.
    private int _seen;

    public TrollPresence(ITrollBruteForceService service, IModLogger logger)
    {
        _service = service;
        _logger = logger;
    }

    public bool Seen => Volatile.Read(ref _seen) != 0;

    public void Note(string? monsterId)
    {
        if (Seen || !_service.IsBruteForceTroll(monsterId)) return;
        if (Interlocked.CompareExchange(ref _seen, 1, 0) == 0)
            _logger.LogInfo($"[TrollBruteForce] First Brute Force troll built ('{monsterId}'): formation spacing and clip trace start ticking");
    }

    public void Clear()
    {
        if (Interlocked.Exchange(ref _seen, 0) == 0)
            _logger.LogInfo("[TrollBruteForce] No Brute Force troll built this mission: formation spacing and clip trace never ran");
    }
}
