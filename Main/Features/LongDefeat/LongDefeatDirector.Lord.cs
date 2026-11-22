using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Features.LongDefeat.Diagnostics;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Features.LongDefeat;

/// <summary>
/// The engine half of the death rule at the director (phase4-plan.md Slice 4, spike S12). At a clear the run orders the rise whenever the lord is not in a body of his own; the field hands a taken soldier to the AI,
/// spawns the lord, and gives the soldier back to the player if that spawn is refused (D40). The general's seat is written on the tick the run says it changed and read back on the next by <c>general-down</c> (D41), the bot takes a soldier five seconds after a fall
/// (D44a) and the <c>lord_falls</c> test option fells the lord's own body (D47). Every adapter call is made from <c>Tick</c> or <c>Apply</c>, on the main thread; a removal callback only notes the death. A director with no lord
/// adapter (the director's tests) keeps the old respawn and serves none of the rest; every run in the game has one, a co-op host's and a story mission's too.
/// </summary>
internal sealed partial class LongDefeatDirector
{
    private readonly ILongDefeatLordAdapter? _lord;
    private readonly HashSet<string> _noLordWarned = new();
    private LdPlace? _risePlace;
    private string _riseLabel = "scene";
    private string _riseVia = "";
    private RiseWatch? _riseWatch;
    private int _tickNo;
    private int _generalDownDeath;
    private int _generalDownAfter;
    private bool _botTookSoldier;
    private bool _lordReleased;
    private LdGeneralSeat _seatWritten = LdGeneralSeat.Lord;

    /// <summary>A rise waiting for its read-back: the wave it precedes, where it was booked, the place asked, the field's answer, and the tick it was served on.</summary>
    private sealed record RiseWatch(int Wave, string At, LdPlace? Place, LdRise Result, int Tick);

    /// <summary>The place the keep rise uses (D49): the recorded keep, else the scene's frame. The boot's <c>fort-points</c> check may move it to the scene's frame.</summary>
    private void ChooseRisePlace()
    {
        _risePlace = _keepAt is { } keep ? LdPlace.At(keep) : null;
        _riseLabel = _plan.Keep != null ? "recorded" : "scene";
    }

    /// <summary>The recorded keep has no navmesh or lies outside the boundary: the rise falls back to the scene's own frame and says so.</summary>
    private void UseSceneRise()
    {
        _risePlace = _field.KeepFromScene() is { } scene ? LdPlace.At(scene) : null;
        _riseLabel = "scene";
        _logger.LogWarning($"{Tag}[Boot] the rise point (the recorded keep) is unusable; the lord rises at the scene's frame {PlaceText(_risePlace)}");
    }

    private static string PlaceText(LdPlace? place) => place == null ? "none" : PointText(place.Point);

    /// <summary>
    /// Serves the rise the run orders at a clear (D40). The field hands a taken soldier to the AI, then spawns the lord at the rise place (the home point for a mounted class, D48; the engine's own attacker frame, booked <c>respawn</c>, when the player attacks
    /// in a story mission and the keep is the enemy's) in his kit, and the run is told where he rose and whether a soldier fights on. A refused spawn fails the check at once and answers the run with nothing, so the next clear orders it again. Without a lord adapter this is the old respawn.
    /// </summary>
    private void RiseLord(LordRiseOrder order)
    {
        if (_run.Result != null)
            return;
        if (_lord == null)
        {
            RespawnLord(order);
            return;
        }

        var attacks = _playerAttacks && !order.AtHome;
        var label = order.AtHome ? "home" : attacks ? "respawn" : _riseLabel;
        var place = order.AtHome ? _homePoint : attacks ? null : _risePlace;
        var wave = _run.Wave + 1;
        var loadout = _run.EffectiveLoadout;
        var answer = _lord.RiseLord(place, loadout);
        if (!answer.Spawned)
        {
            LongDefeatChecks.Record(_outcomes, LongDefeatChecks.RiseId, "w" + wave, LongDefeatLordChecks.RiseRefused(label));
            _logger.LogWarning($"{Tag}[Lord] rise before wave {wave} refused: the engine would not spawn the lord at {PlaceText(place)}; nothing was handed back, and the next clear orders the rise again");
            return;
        }

        if (order.HandBack && answer.HandBack == LdHandBack.None)
            answer = answer with { HandBack = LdHandBack.Dead };
        _riseVia = "rise";
        JudgeClass(true, loadout);
        _run.LordRose(label, answer.HandedBack);
        _riseWatch = new RiseWatch(wave, label, place, answer, _tickNo);
        _logger.LogInfo($"{Tag}[Lord] rise before wave {wave}: at={label} place={PlaceText(place)} handed_back={LongDefeatLordChecks.HandBackText(answer.HandBack)} was_using={(answer.WasUsing ? 1 : 0)}");
    }

    /// <summary>The respawn a director with no lord adapter has always made: only a lord with no body at all is raised, as a foot lord joins his formation or a mounted one stands at home.</summary>
    private void RespawnLord(LordRiseOrder order)
    {
        if (_mainBody != 0)
            return;

        _riseVia = "respawn";
        if (SpawnLord(true))
            _run.LordRose(order.AtHome ? "home" : "respawn", false);
        else
            LongDefeatChecks.Record(_outcomes, LongDefeatChecks.PlayerDeathId, "d" + _run.Stats.PlayerDeaths, LongDefeatChecks.PlayerDeath(false, "respawn-refused"));
    }

    /// <summary>How the body just adopted came, spent at its adoption: <c>rise</c>, <c>respawn</c>, or empty for a take-over. Spent whether or not a death was pending, so a rise never labels a later take-over.</summary>
    private string TakeRiseVia()
    {
        var via = _riseVia;
        _riseVia = "";
        return via;
    }

    /// <summary>
    /// The tick after the rise, once the new body was adopted: the field reads him back and the <c>rise</c> check judges it (<c>main_is_lord</c>, the place, the controller the mode expects, no second player body, the
    /// general and the order owner).
    /// </summary>
    private void JudgeRise()
    {
        if (_riseWatch is not { } watch || _lord == null || _tickNo <= watch.Tick)
            return;

        _riseWatch = null;
        var lord = _field.ReadLord(false);
        var control = _lord.ReadControl();
        var facts = _field.BootFacts();
        var verdict = LongDefeatLordChecks.Rise(watch.At, _plan.Options.Autoplay, watch.Result, lord, watch.Place, control, facts.GeneralIsMain, facts.OrderOwnerIsMain);
        LongDefeatChecks.Record(_outcomes, LongDefeatChecks.RiseId, "w" + watch.Wave, verdict);
        _logger.LogInfo($"{Tag}[Lord] rise before wave {watch.Wave} read back {verdict.Result.ToString().ToLowerInvariant()}: {verdict.Detail} armour={ArmourReadText(_lord.ReadLordArmour())}");
    }

    /// <summary>
    /// A body the player controlled fell. Notes the death for the seat's check, which runs on a later tick than the run's write, and forgets a rise or a bot take-over whose body never came to be
    /// adopted, so neither labels the next body.
    /// </summary>
    private void NoteLordFall(string troopId, bool forced)
    {
        _riseVia = "";
        _botTookSoldier = false;
        _generalDownDeath = _run.Stats.PlayerDeaths;
        _generalDownAfter = _tickNo;
        _logger.LogInfo($"{Tag}[Lord] fell troop={troopId} forced={(forced ? 1 : 0)} deaths={_run.Stats.PlayerDeaths}");
    }

    /// <summary>
    /// The tick after a body fell, after the run wrote the seat: the engine's general is a standing hero or nobody, never the dead body. With a main agent standing already the check is a skip.
    /// </summary>
    private void JudgeGeneralDown()
    {
        if (_generalDownDeath == 0 || _lord == null || _tickNo <= _generalDownAfter)
            return;

        var death = _generalDownDeath;
        _generalDownDeath = 0;
        var verdict = LongDefeatLordChecks.GeneralDown(_mainBody != 0, _lord.ReadSeat(), _seatWritten, WantedHeroStands());
        LongDefeatChecks.Record(_outcomes, LongDefeatChecks.GeneralDownId, "d" + death, verdict);
        _logger.LogInfo($"{Tag}[Lord] general down d{death} {verdict.Result.ToString().ToLowerInvariant()}: {verdict.Detail}");
    }

    /// <summary>Whether the hero the run last gave the seat to still stands on the player's own side, by the director's books (a removal is drained before this tick's read).</summary>
    private bool WantedHeroStands() => !_playerAttacks && _seatWritten.Kind == GeneralSeatKind.Hero && _heroesStanding.Contains(_seatWritten.HeroId);

    /// <summary>The run asks the bot to take a soldier (D44a). The field logs his troop, formation and order before the take; the adoption logs them after.</summary>
    private void AskBotTakeover()
    {
        if (_lord == null)
        {
            WarnNoLordAdapter("the bot's take-over");
            return;
        }

        var take = _lord.TakeOverForBot();
        _botTookSoldier = take.Took;
        _logger.LogInfo($"{Tag}[Lord] bot take-over asked: took={(take.Took ? 1 : 0)} troop={(take.Took ? take.Troop : "none")}");
    }

    /// <summary>The adoption of the soldier the bot took: his troop, formation, order and controller after the engine's own take and the adoption (A30).</summary>
    private void LogBotTakeoverAfter()
    {
        if (!_botTookSoldier)
            return;

        _botTookSoldier = false;
        var lord = _field.ReadLord(true);
        var controller = _lord?.ReadControl().Controller ?? "none";
        _logger.LogInfo($"{Tag}[Lord] takeover after troop={lord.Id} formation={lord.Formation} order_at={OrderText(lord)} to_formation={F1(lord.ToFormation)} controller={controller}");
    }

    /// <summary>The <c>lord_falls</c> test option (D47): the field fells the lord's own body with a blow owned by a standing attacker.</summary>
    private void AskForcedFall()
    {
        if (_lord == null)
        {
            WarnNoLordAdapter("lord_falls");
            return;
        }

        var felled = _lord.FellLord();
        _logger.LogInfo($"{Tag}[Lord] forced fall asked: felled={(felled ? 1 : 0)}");
    }

    /// <summary>An effect the run handed out that this director cannot serve (no lord adapter): said once, so a run armed with it never reads as proof of a death nobody carried out.</summary>
    private void WarnNoLordAdapter(string what)
    {
        if (_noLordWarned.Add(what))
            _logger.LogWarning($"{Tag}[Lord] {what} was asked for, and this run has no lord adapter, so nothing was done");
    }

    /// <summary>The run hands out a seat (D41): the field writes it, and the director keeps it as what <c>general-down</c> expects to read.</summary>
    private void WriteGeneral(LdGeneralSeat seat)
    {
        _seatWritten = seat;
        _lord?.SetGeneral(seat);
    }

    /// <summary>
    /// Drops what the field keeps for the death rule (the last position the lord stood at, the body marked forced): once, when the run ends. A read-back still owed then (a rise on the run's last tick, a fall
    /// on it) is written as a skip, so its row exists and nothing is judged from a field the run no longer drives.
    /// </summary>
    private void ReleaseLordHolds()
    {
        if (_lord == null || _lordReleased)
            return;

        _lordReleased = true;
        if (_riseWatch is { } watch)
            LongDefeatChecks.Record(_outcomes, LongDefeatChecks.RiseId, "w" + watch.Wave, LongDefeatLordChecks.RunEnded("at=" + watch.At));
        if (_generalDownDeath != 0)
            LongDefeatChecks.Record(_outcomes, LongDefeatChecks.GeneralDownId, "d" + _generalDownDeath, LongDefeatLordChecks.RunEnded("seat=unread"));
        _riseWatch = null;
        _generalDownDeath = 0;
        _lord.ReleaseLord();
    }
}
