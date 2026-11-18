using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;

namespace TAOM.Features.NavalTravel;

/// <summary>
/// Old saves (docs/features/naval-travel.md "At-sea repair"): a save can hold parties flagged at sea although TAOM
/// registers no naval navigation model, so vanilla treats water as invalid terrain and such a party cannot path off it.
/// At session launch each such party leader goes to a settlement gate through <c>DisembarkToPosition</c>, which also
/// moves its attached parties, then holds and thinks again at the next hourly AI tick. A party in a MapEvent or a siege
/// is engine-owned and stays. No save data: a repaired party is on land, so the next load finds nothing.
/// </summary>
public sealed class AtSeaRepairBehavior : CampaignBehaviorBase
{
    private readonly IModLogger _logger;
    private readonly ICoopSessionProvider _coop;

    public AtSeaRepairBehavior(IModLogger logger, ICoopSessionProvider coop)
    {
        _logger = logger;
        _coop = coop;
    }

    public override void RegisterEvents() =>
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);

    public override void SyncData(IDataStore dataStore) { }

    private void OnSessionLaunched(CampaignGameStarter _)
    {
        // Co-op: party position and flags are saved state; only the host writes them. Clients get them in the join save.
        if (!_coop.IsAuthority || !CoopSessionPolicy.MayWriteSaveBackedState(_coop.IsCoopClient))
            return;

        int moved = 0, skipped = 0, leftToEngine = 0;
        try
        {
            var atSea = MobileParty.All.Where(p => p != null && p.IsActive && p.IsCurrentlyAtSea).ToList();
            if (atSea.Count == 0)
                return;

            var gates = Settlement.All.Where(s => s.IsTown || s.IsCastle || s.IsVillage).ToList();
            var anchors = gates.Select(s => { var g = s.GatePosition.ToVec2(); return (g.X, g.Y); }).ToList();
            foreach (var party in atSea)
            {
                var action = AtSeaRepair.Decide(party.IsCurrentlyAtSea, party.AttachedTo != null,
                    party.HasNavalNavigationCapability, party.IsTransitionInProgress, party.MapEvent != null,
                    party.BesiegerCamp != null, party.CurrentSettlement != null);
                switch (action)
                {
                    case AtSeaRepairAction.None:
                        leftToEngine++;
                        break;
                    case AtSeaRepairAction.SkipEngineOwned:
                        skipped++;
                        _logger.LogInfo($"[NavalTravel] at-sea repair: {party.StringId} is in a battle or siege; left at sea");
                        break;
                    default:
                        if (MoveToLand(party, action, gates, anchors)) moved++;
                        else skipped++;
                        break;
                }
            }

            _logger.LogInfo($"[NavalTravel] at-sea repair: {atSea.Count} part(ies) flagged at sea: {moved} moved to land, "
                + $"{skipped} left at sea, {leftToEngine} attached, able to sail or mid-transition (vanilla moves them)");
        }
        catch (Exception ex)
        {
            _logger.LogError($"[NavalTravel] at-sea repair stopped after {moved} move(s) ({ex.GetType().Name}: {ex.Message}); "
                + "the other parties stay as loaded");
        }
    }

    private bool MoveToLand(MobileParty party, AtSeaRepairAction action, List<Settlement> gates, List<(float X, float Y)> anchors)
    {
        Settlement? target = party.CurrentSettlement;
        if (action == AtSeaRepairAction.MoveToNearestGate)
        {
            var from = party.Position.ToVec2();
            var i = NavalLandFallback.NearestIndex(from.X, from.Y, anchors);
            target = i < 0 ? null : gates[i];
        }
        if (target == null)
        {
            _logger.LogInfo($"[NavalTravel] at-sea repair: no land settlement for {party.StringId}; left at sea");
            return false;
        }

        var was = party.Position.ToVec2();
        party.DisembarkToPosition(target.GatePosition);
        party.SetMoveModeHold();
        party.Ai.RethinkAtNextHourlyTick = true;
        _logger.LogInfo($"[NavalTravel] at-sea repair: {party.StringId} moved from ({was.X:F1}, {was.Y:F1}) to the gate of {target.StringId}");
        return true;
    }
}
