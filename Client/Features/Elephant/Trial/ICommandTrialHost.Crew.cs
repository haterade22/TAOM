using TAOM.Features.Elephant;

namespace TAOM.Client.Features.SeatOfSeeing.Trial;

internal partial interface ICommandTrialHost
{
    /// <summary>
    /// Chooses the first living beast of the kind whose every seat holds an archer, and one of its archers as the target of the next
    /// keep. Returns the beast's seat count, 0 when no such beast is on the field. It reads agents: call it after the in-mission-tick marker.
    /// </summary>
    int ChooseCrew(CrewBeast beast);

    /// <summary>Chooses a seated archer of the chosen beast as the target of the next keep; false when no seat holds one. It reads agents: call it after the in-mission-tick marker.</summary>
    bool ChooseCrewArcher();

    /// <summary>The chosen beast's crew now. It reads agents: call it after the in-mission-tick marker.</summary>
    CrewReading ReadCrew();

    /// <summary>Gives every company of the player's team the Charge order, so the beast walks to the enemy. A formation write: call it after the in-mission-tick marker.</summary>
    void ChargeAll();
}
