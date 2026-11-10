using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TAOM.Features.Realms.Domain.World;

namespace TAOM.Features.Realms;

/// <summary>Copies the kingdoms and fiefs of the running campaign into a <see cref="WorldSnapshot"/>.</summary>
internal static class WorldSnapshotReader
{
    public static WorldSnapshot Read()
    {
        var kingdoms = new List<KingdomInfo>();
        foreach (var kingdom in Kingdom.All)
        {
            if (kingdom == null || kingdom.IsEliminated)
                continue;
            kingdoms.Add(new KingdomInfo(kingdom.StringId, kingdom.Name?.ToString() ?? kingdom.StringId,
                kingdom.Culture?.StringId, kingdom.Leader?.StringId));
        }

        var fiefs = new List<FiefInfo>();
        foreach (var settlement in Settlement.All)
        {
            var info = ToFief(settlement);
            if (info != null)
                fiefs.Add(info);
        }

        return new WorldSnapshot(kingdoms, fiefs);
    }

    private static FiefInfo? ToFief(Settlement settlement)
    {
        if (settlement == null)
            return null;

        FiefKind kind;
        Settlement? landlord = settlement;
        string? boundId = null;
        if (settlement.IsTown)
            kind = FiefKind.Town;
        else if (settlement.IsCastle)
            kind = FiefKind.Castle;
        else if (settlement.IsVillage && settlement.Village?.Bound != null)
        {
            kind = FiefKind.Village;
            landlord = settlement.Village.Bound;
            boundId = landlord.StringId;
        }
        else
            return null;

        // A village has no owner of its own: it belongs to whoever holds its town or castle.
        var position = settlement.GetPosition2D;
        return new FiefInfo(
            settlement.StringId,
            settlement.Name?.ToString() ?? settlement.StringId,
            kind,
            landlord.OwnerClan?.Kingdom?.StringId,
            landlord.Owner?.StringId,
            boundId,
            position.x,
            position.y,
            settlement.Culture?.StringId);
    }
}
