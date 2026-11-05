using TAOM.Features.SeatOfSeeing;

namespace TAOM.Client.Features.SeatOfSeeing.Trial;

/// <summary>
/// The settings an armed field trial runs with (build-spec section 12), over the plain provider and in memory only: it never writes the
/// MCM file, because json2 keeps saved values. The steps need the Seat on, a battle that does not begin in it, the cursor with the panel,
/// the panel kept open and the take of a soldier, whatever the player saved. The fight value starts off and the steps that need the hand-off
/// turn it on for their own entry and off again (<c>back_to_stand</c> turns it off for its second entry).
/// </summary>
internal sealed class CommandTrialSettings : ISeatSettingsProvider
{
    public bool Enabled => true;

    public bool BeginBattlesInSeat => false;

    public bool CursorWithPanel => true;

    public bool KeepPanelOpen => true;

    // Off until a step turns it on: with it on from the start, the hero is on the AI one tick after the first entry, and the S1 steps that
    // read his controller and his speed would fail on a correct build.
    public bool HeroFightsOn { get; set; }

    public bool TakeSoldierAfterFall => true;
}
