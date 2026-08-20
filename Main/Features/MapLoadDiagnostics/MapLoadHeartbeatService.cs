using System;
using System.Globalization;

namespace TAOM.Features.MapLoadDiagnostics;

/// <summary>
/// Emits one line every few seconds while the campaign map is ticking, so a load that never
/// finishes leaves a record of WHY rather than a log that simply stops.
///
/// <para>
/// Written for the v1.5.0 map-load stall, where the map screen is live and
/// <c>Campaign.RealTick</c> is executing while the player sees a frozen loading screen, and where
/// both engine hot paths are byte-identical to v1.4.8. The useful question is therefore which
/// quantity fails to settle, and each field answers one candidate:
/// </para>
/// <list type="bullet">
///   <item><c>fps</c> and <c>tickMs</c> — a slow load or a stopped one, and how much of the frame
///   the campaign tick itself accounts for. A small <c>tickMs</c> against a long frame puts the
///   cost in rendering or UI instead; a large one puts it in the simulation.</item>
///   <item><c>parties</c> with its per-type census — a climbing total means something spawns
///   without end, and the breakdown says what. A flat total exonerates spawning outright, which is
///   worth as much as a climbing one.</item>
///   <item><c>heroes</c> / <c>clans</c> — the usual upstream cause when lord parties climb.</item>
///   <item><c>campaignTime</c> — advancing or paused, which a campaign is at its start.</item>
///   <item><c>stack</c> / <c>activeState</c> / <c>topScreen</c> — with the map running at 85 fps
///   behind an overlay that never lifts, a state left pushed above <c>MapState</c> is the shape
///   that fits, and only the whole stack shows it.</item>
///   <item><c>loadingWindow</c> — the signal that splits the diagnosis. Up means the engine still
///   considers itself loading; down means the map is live and merely not visible, a different bug
///   with a different fix.</item>
/// </list>
///
/// <para>
/// Split into <see cref="ShouldEmit"/> and <see cref="BuildLine"/> deliberately: the census walks
/// every mobile party, which is the cost under investigation, so it must run only on the frames
/// that emit. A per-frame census would distort the very measurement it is here to take.
/// </para>
/// </summary>
public class MapLoadHeartbeatService : IMapLoadHeartbeatService
{
    // Long enough not to perturb the timing it measures, short enough that a player who quits
    // after a minute still sends something usable.
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);

    private bool _started;
    private DateTime _firstUtc;
    private DateTime _lastEmitUtc;
    private int _framesSinceEmit;
    private double _tickMsSum;
    private int _lastParties;
    private int _lastHeroes;
    private double _lastCampaignTime;

    public double TickMsAverage => _framesSinceEmit > 0 ? _tickMsSum / _framesSinceEmit : 0d;

    public bool ShouldEmit(DateTime nowUtc, double tickMs)
    {
        _framesSinceEmit++;
        _tickMsSum += tickMs;

        if (!_started) return true;
        return nowUtc - _lastEmitUtc >= Interval;
    }

    public string BuildLine(DateTime nowUtc, MapLoadSample s)
    {
        var c = CultureInfo.InvariantCulture;

        if (!_started)
        {
            _started = true;
            _firstUtc = nowUtc;
            _lastEmitUtc = nowUtc;
        }

        var seconds = (nowUtc - _lastEmitUtc).TotalSeconds;
        // The first line has no window to average over, so report fps as 0 rather than divide by zero.
        var fps = seconds > 0d ? _framesSinceEmit / seconds : 0d;
        var partyDelta = s.PartyCount - _lastParties;
        var heroDelta = s.HeroCount - _lastHeroes;
        var timeDelta = s.CampaignTime - _lastCampaignTime;

        var line = string.Format(c,
            "[MapLoad] t=+{0:0}s frames={1} fps={2:0.0} tickMs={3:0.0} "
            + "parties={4}({5}{6}) [lord={7} villager={8} caravan={9} bandit={10} militia={11} garrison={12} other={13}] "
            + "heroes={14}({15}{16}) clans={17} settlements={18} campaignTime={19:0.000}({20}{21:0.000}) "
            + "loadingWindow={22} timeControl={23} topScreen={24} activeState={25} stack=[{26}]",
            (nowUtc - _firstUtc).TotalSeconds, _framesSinceEmit, fps, s.TickMsAvg,
            s.PartyCount, partyDelta >= 0 ? "+" : "", partyDelta,
            s.LordParties, s.Villagers, s.Caravans, s.Bandits, s.Militia, s.Garrisons, s.OtherParties,
            s.HeroCount, heroDelta >= 0 ? "+" : "", heroDelta, s.ClanCount, s.SettlementCount,
            s.CampaignTime, timeDelta >= 0 ? "+" : "", timeDelta,
            s.IsLoadingWindowActive, s.TimeControl, s.TopScreen, s.ActiveState, s.StateStack);

        _lastEmitUtc = nowUtc;
        _framesSinceEmit = 0;
        _tickMsSum = 0d;
        _lastParties = s.PartyCount;
        _lastHeroes = s.HeroCount;
        _lastCampaignTime = s.CampaignTime;
        return line;
    }
}
