using TaleWorlds.CampaignSystem.MapEvents;

namespace TAOM.Features.FieldCommission.Hooks;

/// <summary>Pure sealed-type boundary helper — isolates the AttackerSide/DefenderSide comparison
/// so <see cref="FieldCommissionBehavior"/> stays under the ADR-002 line budget.</summary>
internal static class MapEventSideHelper
{
    public static MapEventSide GetPlayerSide(MapEvent mapEvent) =>
        mapEvent.PlayerSide == mapEvent.AttackerSide.MissionSide ? mapEvent.AttackerSide : mapEvent.DefenderSide;

    public static MapEventSide GetEnemySide(MapEvent mapEvent)
    {
        var playerSide = GetPlayerSide(mapEvent);
        return ReferenceEquals(playerSide, mapEvent.AttackerSide) ? mapEvent.DefenderSide : mapEvent.AttackerSide;
    }
}
