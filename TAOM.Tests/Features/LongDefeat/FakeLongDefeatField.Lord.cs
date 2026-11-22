using System.Collections.Generic;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat;

/// <summary>
/// The death rule's half of the field fake (phase4-plan.md Slice 4): a rise spawns a body of the lord's own and answers as the test sets it, a take-over takes a soldier, and the general's seat is read back
/// from what was written, so a run that never writes it leaves the dead body as the engine would (nothing clears <c>Team.GeneralAgent</c>, Mission.cs:3015-3041). The controller follows the adoption the real
/// field makes: a body the engine builds for the player is <c>Player</c>, and under autoplay the adoption hands it to the AI.
/// </summary>
internal sealed partial class FakeLongDefeatField
{
    /// <summary>Every rise asked for: the place and the loadout, in order.</summary>
    public List<(LdPlace? Place, LdLoadout? Loadout)> Rises { get; } = new();

    /// <summary>When false the engine refuses the lord's spawn: nothing is spawned and nothing is handed back.</summary>
    public bool RiseSpawnOk { get; set; } = true;

    /// <summary>What a rise that spawned answers about the soldier; by default nobody was taken.</summary>
    public LdRise RiseAnswer { get; set; } = new(true, LdHandBack.None, false);

    /// <summary>What the bot's take-over answers; by default he takes a militiaman.</summary>
    public LdTake TakeAnswer { get; set; } = new(true, "fow_militia");

    /// <summary>What <see cref="FellLord"/> answers; false when no attacker stands or his own body is not the main agent.</summary>
    public bool FellLordResult { get; set; } = true;

    /// <summary>Every seat the director wrote, in order.</summary>
    public List<LdGeneralSeat> Seats { get; } = new();

    /// <summary>Overrides what <see cref="ReadSeat"/> derives from the writes, when a test needs a seat the engine would not give.</summary>
    public LdSeatRead? SeatOverride { get; set; }

    /// <summary>The controller of the main agent, as the adoption and a spawn leave it.</summary>
    public string ControllerName { get; set; } = "Player";

    /// <summary>The other <c>Player</c>-controlled bodies of the player's side that <see cref="ReadControl"/> reports.</summary>
    public int OtherPlayerBodies { get; set; }

    private int _nextBody = 10;

    public LdRise RiseLord(LdPlace? place, LdLoadout? loadout)
    {
        Calls.Add("Rise:" + (place == null ? "none" : F(place.X)));
        Rises.Add((place, loadout));
        if (!RiseSpawnOk)
            return LdRise.Refused;

        MainBody = _nextBody++;
        ControllerName = "Player";
        Lord = Lord with { Own = true, Id = "lord_4_1", At = place?.Point ?? Lord.At };
        return RiseAnswer;
    }

    public LdTake TakeOverForBot()
    {
        Calls.Add("TakeOver");
        if (TakeAnswer.Took)
        {
            MainBody = _nextBody++;
            ControllerName = "Player";
            Lord = Lord with { Own = false, Id = TakeAnswer.Troop };
        }

        return TakeAnswer;
    }

    private LdSeatKind _seatKind = LdSeatKind.Main;
    private string _seatHero = "";
    private int _seatBody = 1;

    /// <summary>The adoption writes the main agent as general (<c>AdoptMain</c>), as the real field does.</summary>
    private void NoteAdopted()
    {
        _seatKind = LdSeatKind.Main;
        _seatBody = MainBody;
    }

    public void SetGeneral(LdGeneralSeat seat)
    {
        Calls.Add("General:" + seat.Kind + (seat.HeroId.Length > 0 ? ":" + seat.HeroId : ""));
        Seats.Add(seat);
        _seatHero = seat.HeroId;
        _seatBody = MainBody;
        _seatKind = seat.Kind switch
        {
            GeneralSeatKind.Lord => MainBody != 0 ? LdSeatKind.Main : LdSeatKind.None,
            GeneralSeatKind.Hero => LdSeatKind.Hero,
            _ => LdSeatKind.None,
        };
    }

    /// <summary>The seat as the engine holds it: a seat written for a body that is no longer the main agent is that dead body, because nothing in the engine clears <c>Team.GeneralAgent</c>.</summary>
    public LdSeatRead ReadSeat()
    {
        Calls.Add("ReadSeat");
        if (SeatOverride is { } forced)
            return forced;
        if (_seatKind == LdSeatKind.Main)
            return MainBody != 0 && MainBody == _seatBody ? new LdSeatRead(LdSeatKind.Main) : new LdSeatRead(LdSeatKind.Stale);

        return new LdSeatRead(_seatKind, _seatKind == LdSeatKind.Hero ? _seatHero : "");
    }

    public LdControlRead ReadControl()
    {
        Calls.Add("ReadControl");
        return new LdControlRead(MainBody == 0 ? "none" : ControllerName, OtherPlayerBodies);
    }

    public bool FellLord()
    {
        Calls.Add("FellLord");
        return FellLordResult;
    }

    public void ReleaseLord() => Calls.Add("ReleaseLord");
}
