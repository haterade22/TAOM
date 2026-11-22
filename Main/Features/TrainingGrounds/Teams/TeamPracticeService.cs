using TAOM.Features.CoopInterop;
using TAOM.Features.Execution;

namespace TAOM.Features.TrainingGrounds.Teams;

/// <summary>
/// #1528: decides whether the practice fight in a town is a team fight (docs/features/team-practice.md). It is
/// when the switch is on, this peer runs the single-player campaign, and the town's owner is of the Free Peoples.
/// A town's side is its owner's, the rule the tournament alignment filter uses (#744), so a captured town follows
/// its conqueror and an Evil or Neutral town keeps vanilla's free-for-all. The caller builds the owner's pair
/// with <c>FactionSides.Of(settlement.MapFaction)</c>: the faction's id and that faction's own culture, never a
/// kingdom id with another entity's culture (rca-tournament-alignment-filter-2026-10-06.md).
/// </summary>
public sealed class TeamPracticeService
{
    private readonly IAlignmentService _alignment;
    private readonly ITeamPracticeSettingsProvider _settings;
    private readonly ICoopSessionProvider _coop;
    private readonly IDedicatedServerProvider _dedicated;

    public TeamPracticeService(
        IAlignmentService alignment,
        ITeamPracticeSettingsProvider settings,
        ICoopSessionProvider coop,
        IDedicatedServerProvider dedicated)
    {
        _alignment = alignment;
        _settings = settings;
        _coop = coop;
        _dedicated = dedicated;
    }

    public bool IsTeamTown(SideIds owner) =>
        _settings.IsEnabled
        && CoopSessionPolicy.IsSinglePlayerAuthority(_coop, _dedicated)
        && _alignment.ResolveSide(owner) == FactionSide.Free;
}
