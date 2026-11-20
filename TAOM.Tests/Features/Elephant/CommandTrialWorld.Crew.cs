using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Features.Elephant;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Tests.Features.SeatOfSeeing;

// The crewed beasts of the stand-in (#1503): one mumakil and one elephant whose archers shoot after the Charge order.
internal sealed partial class CommandTrialWorld
{
    internal int MumakilSeats = 8;
    internal int HowdahSeats = 2;
    internal bool NoCrew;
    internal float CrewAppearsAt;
    internal int EmptySeatsAtStart;
    internal float SecondsToFirstShot = 5f;
    internal float SecondsPerShot = 4f;
    internal int? SilentSeat;
    internal int RescuesOnTheFirstSeat;
    internal float BeastSpeed = 3f;
    internal float? BeastDiesAfterSeconds;
    internal float? KeptArcherDiesAfterSeconds;
    internal bool NoOtherArcher;
    internal bool WheelDoesNothing;

    internal readonly List<CrewBeast> CrewChoices = new();
    internal int ChargeOrders;
    internal int ArcherChoices;
    internal int CrewReadsOutsideTheMissionTick;
    internal float? ZoomedAt;
    internal int KeepPresses;

    private CrewBeast _crewBeast;
    private float _crewChargedAt = float.NaN;
    private bool _keptArcherFell;

    private float SinceCharge => float.IsNaN(_crewChargedAt) ? 0f : Time - _crewChargedAt;

    private bool BeastLives => !(BeastDiesAfterSeconds.HasValue && SinceCharge >= BeastDiesAfterSeconds.Value);

    private int SeatsOfTheChosen => _crewBeast == CrewBeast.Mumakil ? MumakilSeats : HowdahSeats;

    public int ChooseCrew(CrewBeast beast)
    {
        NoteCrewRead();
        CrewChoices.Add(beast);
        _crewBeast = beast;
        // The real host takes only a beast whose every seat holds an archer (CommandTrialHost.HasFullCrew).
        if (NoCrew || Time < CrewAppearsAt || EmptySeatsAtStart > 0)
            return 0;

        ResetTheKeepTarget();
        return SeatsOfTheChosen;
    }

    public bool ChooseCrewArcher()
    {
        NoteCrewRead();
        ArcherChoices++;
        if (NoOtherArcher)
            return false;

        ResetTheKeepTarget();
        return true;
    }

    public CrewReading ReadCrew()
    {
        NoteCrewRead();
        if (!BeastLives)
            return new CrewReading(false, Array.Empty<CrewSeatReading>(), float.NaN);

        var seats = Enumerable.Range(0, SeatsOfTheChosen).Select(Seat).ToList();
        return new CrewReading(true, seats, BeastSpeed * SinceCharge);
    }

    public void ChargeAll()
    {
        Note("charge-all");
        ChargeOrders++;
        if (float.IsNaN(_crewChargedAt))
            _crewChargedAt = Time;
    }

    private CrewSeatReading Seat(int index)
    {
        var empty = index >= SeatsOfTheChosen - EmptySeatsAtStart || (index == 0 && _keptArcherFell);
        var sinceFirst = SinceCharge - SecondsToFirstShot;
        var shots = empty || SilentSeat == index || sinceFirst < 0f ? 0 : 1 + (int)(sinceFirst / SecondsPerShot);
        return new CrewSeatReading(shots, index == 0 ? RescuesOnTheFirstSeat : 0);
    }

    private void ResetTheKeepTarget()
    {
        _choseCompany = false;
        _everKept = false;
        _deathNoticed = false;
        _soldierDead = false;
    }

    private void NoteCrewRead()
    {
        if (!InMissionTick)
            CrewReadsOutsideTheMissionTick++;
    }

    // Once per mission tick, before the keep is resolved: the wheel moves the orbit, and the kept archer can fall once.
    private void CrewFrame()
    {
        if (_keepPressed)
            KeepPresses++;
        if (Keeping != SeatKeepKind.None && !WheelDoesNothing && Input is { ZoomNotches: not 0f } input)
        {
            KeptDistance *= (float)Math.Pow(SeatOrbit.ZoomPerNotch, input.ZoomNotches);
            ZoomedAt ??= Time;
        }

        if (!_keptArcherFell && KeptArcherDiesAfterSeconds.HasValue && Keeping == SeatKeepKind.Soldier && SinceCharge >= KeptArcherDiesAfterSeconds.Value)
        {
            _keptArcherFell = true;
            _soldierDead = true;
        }
    }
}
