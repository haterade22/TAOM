using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TAOM.Features.FiefManagement.Models;

namespace TAOM.Adapters;

public class SettlementOwnershipAdapter : ISettlementOwnershipAdapter
{
    public IReadOnlyList<FiefSummary> GetPlayerOwnedFiefs()
    {
        var clan = Clan.PlayerClan;
        if (clan == null) return System.Array.Empty<FiefSummary>();

        var result = new List<FiefSummary>();
        foreach (var s in Settlement.All)
        {
            if (s == null) continue;
            if (!(s.IsTown || s.IsCastle)) continue;
            if (s.OwnerClan != clan) continue;
            var name = s.Name?.ToString() ?? string.Empty;
            result.Add(new FiefSummary(s.StringId, name, s.IsTown, s.IsCastle));
        }
        return result;
    }

    public int GetPlayerOwnedFiefCount()
    {
        // Fast path for Patch36_MapScreenF6.Postfix (polled every frame) and the FiefHubService
        // Count / Clamp / Next / Previous routines. Iterates Clan.PlayerClan.Settlements — a
        // cached MBReadOnlyList of just the player's own settlements (typically 1-10 entries) —
        // instead of Settlement.All (~862 entries). Filters to towns + castles to match
        // GetPlayerOwnedFiefs (the cache also contains BoundVillages). Audit issue #143.
        var clan = Clan.PlayerClan;
        if (clan == null) return 0;
        var settlements = clan.Settlements;
        if (settlements == null) return 0;
        int count = 0;
        foreach (var s in settlements)
        {
            if (s == null) continue;
            if (s.IsTown || s.IsCastle) count++;
        }
        return count;
    }

    public bool IsPlayerCurrentlyAt(string settlementId)
    {
        if (string.IsNullOrEmpty(settlementId)) return false;
        var current = MobileParty.MainParty?.CurrentSettlement;
        return current?.StringId == settlementId;
    }

    public Settlement Resolve(string settlementId)
    {
        if (string.IsNullOrEmpty(settlementId)) return null;
        var clan = Clan.PlayerClan;
        if (clan == null) return null;
        foreach (var s in Settlement.All)
        {
            if (s == null) continue;
            if (s.StringId != settlementId) continue;
            // Re-verify ownership at resolution time — the player may have lost the fief
            // between FiefSummary creation and consequence callback execution.
            if (s.OwnerClan != clan) return null;
            return s;
        }
        return null;
    }
}
