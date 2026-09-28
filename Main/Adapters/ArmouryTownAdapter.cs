using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.ObjectSystem;
using TAOM.Core.Validation;

namespace TAOM.Adapters;

/// <summary>Boundary implementation of <see cref="IArmouryTownAdapter"/>.</summary>
public class ArmouryTownAdapter : IArmouryTownAdapter
{
    // DefaultBuildingTypes ids (v1.5.3 DefaultBuildingTypes.cs:145 and :157). Compared by id, not by
    // instance, so a building type a module re-registers still matches.
    private const string TownBarracks = "building_settlement_barracks";
    private const string CastleBarracks = "building_castle_barracks";

    public int GetBarracksLevel(string settlementId)
    {
        // Town.Buildings is a public MBList field (Town.cs:89); Settlement.Town is the fief component for
        // towns and castles alike, null for villages and hideouts.
        var buildings = Find(settlementId)?.Town?.Buildings;
        if (buildings == null)
            return 0;
        foreach (var building in buildings)
        {
            var id = building?.BuildingType?.StringId;
            if (building != null && (id == TownBarracks || id == CastleBarracks))
                return building.CurrentLevel;
        }
        return 0;
    }

    public string GetName(string settlementId) => Find(settlementId)?.Name?.ToString() ?? settlementId ?? string.Empty;

    public string? GetCultureId(string settlementId) => Find(settlementId)?.Culture?.StringId;

    public bool IsTown(string settlementId) => Find(settlementId)?.IsTown == true;

    public string? CurrentSettlementId => Settlement.CurrentSettlement?.StringId;

    public IReadOnlyList<string> AllTownIds()
    {
        var ids = new List<string>();
        // Town.AllTowns is Campaign.Current.AllTowns (v1.5.3 Town.cs:294), the engine's own cached town list;
        // it dereferences Campaign.Current, so there must be a campaign.
        if (Campaign.Current == null || Town.AllTowns == null)
            return ids;
        foreach (var town in Town.AllTowns)
        {
            if (town?.Settlement?.StringId is { Length: > 0 } id)
                ids.Add(id);
        }
        return ids;
    }

    public int Today
    {
        get
        {
            // A float-to-int cast of NaN is int.MinValue (csharp-architecture.md, "Engine-Float Decision Gates"),
            // which would end every visiting armourer's stay at once; refuse it at the cast.
            var days = CampaignTime.Now.ToDays;
            if (!FiniteFloatValidator.IsFinite(days) || days < 0)
                return 0;
            return days >= int.MaxValue ? int.MaxValue : (int)Math.Floor(days);
        }
    }

    // Settlement.Find reads MBObjectManager.Instance without a null check, and a null id throws from its
    // dictionary lookup (v1.5.3 Settlement.cs:1163-1166, MBObjectManager.cs:174), so both are refused here.
    private static Settlement? Find(string settlementId) =>
        string.IsNullOrEmpty(settlementId) || MBObjectManager.Instance == null ? null : Settlement.Find(settlementId);
}
