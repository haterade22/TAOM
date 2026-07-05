using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace TAOM.Features.ArmyTargeting.Diagnostics;

/// <summary>
/// Flat, TaleWorlds-free snapshot of an AI army that hit the vanilla gathering-fortification
/// dead end (the NRE guarded by Patch49). Built at the Harmony boundary via <see cref="FromArmy"/>;
/// consumed by <see cref="ISiegeGatheringDiagnosticsService"/>. Mirrors the
/// <c>TownFoodSnapshot.FromTown</c> boundary-DTO convention (ADR-002/007).
/// </summary>
public sealed class SiegeGatheringFailureInfo
{
    public const string Unknown = "unknown";
    public const int CountUnavailable = -1;

    // The army that failed to gather.
    public string ArmyName { get; set; } = Unknown;
    public string LeaderName { get; set; } = Unknown;
    public string ClanId { get; set; } = Unknown;
    public string KingdomId { get; set; } = Unknown;
    public string KingdomName { get; set; } = Unknown;
    public bool KingdomIsNull { get; set; }

    // The settlement the army was gathering toward (a besieged target on the OnSiegeStarted path;
    // generic "focus" on the other call sites — OnSettlementOwnerChanged etc.).
    public string FocusSettlementId { get; set; } = Unknown;
    public string FocusSettlementName { get; set; } = Unknown;
    public string FocusCultureId { get; set; } = Unknown;
    public string FocusFactionId { get; set; } = Unknown;

    // Kingdom fortification census (CountUnavailable when the kingdom is null / unreadable).
    public int FortificationsTotal { get; set; } = CountUnavailable;
    public int FortificationsUnderSiege { get; set; } = CountUnavailable;

    // Map positions to locate the pair, plus the campaign time of the failure.
    public float LeaderPartyX { get; set; } = float.NaN;
    public float LeaderPartyY { get; set; } = float.NaN;
    public float FocusX { get; set; } = float.NaN;
    public float FocusY { get; set; } = float.NaN;
    public string CampaignTimeText { get; set; } = Unknown;

    /// <summary>
    /// Reads the sealed <see cref="Army"/> + focus <see cref="Settlement"/> into a flat DTO. Every
    /// access is null-guarded so this never throws a secondary exception out of the finalizer — a
    /// missing member yields <see cref="Unknown"/> / <see cref="CountUnavailable"/> / NaN rather
    /// than propagating. Does NOT re-run the MapDistanceModel (the fortification census is a single
    /// cheap walk of <c>Kingdom.Settlements</c>).
    /// </summary>
    public static SiegeGatheringFailureInfo FromArmy(Army army, Settlement focusSettlement)
    {
        var info = new SiegeGatheringFailureInfo();

        try { info.ArmyName = army?.Name?.ToString() ?? Unknown; } catch { }
        try { info.LeaderName = army?.ArmyOwner?.Name?.ToString() ?? Unknown; } catch { }
        try { info.ClanId = army?.ArmyOwner?.Clan?.StringId ?? Unknown; } catch { }

        Kingdom kingdom = null;
        try { kingdom = army?.Kingdom; } catch { }
        info.KingdomIsNull = kingdom == null;
        if (kingdom != null)
        {
            try { info.KingdomId = kingdom.StringId ?? Unknown; } catch { }
            try { info.KingdomName = kingdom.Name?.ToString() ?? Unknown; } catch { }
            CensusFortifications(kingdom, info);
        }

        try { info.FocusSettlementId = focusSettlement?.StringId ?? Unknown; } catch { }
        try { info.FocusSettlementName = focusSettlement?.Name?.ToString() ?? Unknown; } catch { }
        try { info.FocusCultureId = focusSettlement?.Culture?.StringId ?? Unknown; } catch { }
        try { info.FocusFactionId = focusSettlement?.MapFaction?.StringId ?? Unknown; } catch { }

        try
        {
            var lp = army?.LeaderParty;
            if (lp != null) { var p = lp.GetPosition2D; info.LeaderPartyX = p.X; info.LeaderPartyY = p.Y; }
        }
        catch { }
        try
        {
            if (focusSettlement != null) { var p = focusSettlement.GetPosition2D; info.FocusX = p.X; info.FocusY = p.Y; }
        }
        catch { }

        try { info.CampaignTimeText = CampaignTime.Now.ToString() ?? Unknown; } catch { }

        return info;
    }

    private static void CensusFortifications(Kingdom kingdom, SiegeGatheringFailureInfo info)
    {
        try
        {
            int total = 0;
            int underSiege = 0;
            foreach (Settlement s in kingdom.Settlements)
            {
                if (s == null || !s.IsFortification) continue;
                total++;
                if (s.IsUnderSiege) underSiege++;
            }
            info.FortificationsTotal = total;
            info.FortificationsUnderSiege = underSiege;
        }
        catch
        {
            info.FortificationsTotal = CountUnavailable;
            info.FortificationsUnderSiege = CountUnavailable;
        }
    }
}
