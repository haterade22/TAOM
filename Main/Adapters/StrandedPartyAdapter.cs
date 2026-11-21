using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TAOM.Core.Logging;

namespace TAOM.Adapters;

/// <summary>
/// The engine side of releasing a stranded party. <c>PartyBase._mapEventSide</c> is private, so it is reached by name
/// through <c>AccessTools</c> (gated by ReflectionSiteBindingTests).
/// </summary>
internal sealed class StrandedPartyAdapter : IStrandedPartyAdapter
{
    private static readonly AccessTools.FieldRef<PartyBase, MapEventSide>? SideField = BindSideField();

    private readonly MobileParty _party;
    private readonly IModLogger _logger;

    public StrandedPartyAdapter(MobileParty party, IModLogger logger)
    {
        _party = party;
        _logger = logger;
    }

    public string Label()
    {
        try
        {
            string clan = _party.ActualClan?.Name?.ToString() ?? "no clan";
            var mapEvent = _party.Party.MapEvent;
            string battle = mapEvent == null
                ? "no battle"
                : $"{new StuckBattleEventAdapter(mapEvent, _logger).Label()}, finalized={mapEvent.IsFinalized}";
            return $"{_party.Name} ({clan}) in {battle}";
        }
        catch (Exception ex)
        {
            return $"a party ({ex.GetType().Name} reading its names)";
        }
    }

    public void Release()
    {
        if (SideField == null)
            throw new InvalidOperationException("PartyBase._mapEventSide is not available.");
        SideField(_party.Party) = null!;
        _party.Party.SetVisualAsDirty();
    }

    private static AccessTools.FieldRef<PartyBase, MapEventSide>? BindSideField()
    {
        try
        {
            return AccessTools.FieldRefAccess<PartyBase, MapEventSide>("_mapEventSide");
        }
        catch (Exception)
        {
            return null;
        }
    }
}
