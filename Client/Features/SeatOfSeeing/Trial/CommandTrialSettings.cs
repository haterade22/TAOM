using TAOM.Features.SeatOfSeeing;

namespace TAOM.Client.Features.SeatOfSeeing.Trial;

/// <summary>
/// The settings an armed field trial runs with (build-spec section 12), over the plain provider and in memory only: it never writes the
/// MCM file, because json2 keeps saved values. The steps need the Seat on, a battle that does not begin in it, the cursor with the panel
/// and the panel kept open, whatever the player saved.
/// </summary>
internal sealed class CommandTrialSettings : ISeatSettingsProvider
{
    public bool Enabled => true;

    public bool BeginBattlesInSeat => false;

    public bool CursorWithPanel => true;

    public bool KeepPanelOpen => true;
}
