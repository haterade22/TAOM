using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace TAOM.Features.Enlistment.Content.Domain;

/// <summary>
/// The content-layer service record: rank/assignment progression, trust, wage arrears,
/// duty state and scheduler bookkeeping. Persisted as its own SyncData section (key
/// <c>_taom_enlistment_content</c>) in the same <c>key=value;</c> shape as the core record.
/// Unknown keys are ignored, non-finite day values are dropped field-level, and a
/// malformed record resets clean (content loss is progression loss, never a softlock).
/// </summary>
public sealed class ServiceContentRecord
{
    public ServiceRank Rank { get; set; } = ServiceRank.Recruit;
    public ServiceAssignment Assignment { get; set; } = ServiceAssignment.Infantry;
    public int ServiceXp { get; set; }
    public int DaysServed { get; set; }
    public int DutySuccesses { get; set; }
    public int Trust { get; set; }
    public int DeferredWages { get; set; }
    public double? LastAssignmentSwapDay { get; set; }

    // Active field duty (one at a time)
    public string ActiveDutyId { get; set; }
    public string ActiveDutyTargetPartyId { get; set; }
    public string ActiveDutySettlementId { get; set; }
    public double? ActiveDutyDeadlineDay { get; set; }
    public double? ShiftEndDay { get; set; }

    // Scheduler bookkeeping
    public double? LastOfferDay { get; set; }
    public double? LastIncidentDay { get; set; }
    public List<string> RecentDutyIds { get; set; } = new List<string>();

    /// <summary>Highest kit tier already drawn (monotonic; an <c>EnlistmentKitTier</c> ordinal, not a career rank), or null. Omitted from the serialized form when null; TryParse rejects negatives.</summary>
    public int? HighestIssuedKitTier { get; set; }

    /// <summary>One entry per issued item INSTANCE (duplicates preserved for the per-item reclaim at discharge).</summary>
    public List<string> IssuedEquipmentItemIds { get; set; } = new List<string>();

    public bool HasActiveDuty => !string.IsNullOrEmpty(ActiveDutyId);

    public void Reset()
    {
        Rank = ServiceRank.Recruit;
        Assignment = ServiceAssignment.Infantry;
        ServiceXp = 0;
        DaysServed = 0;
        DutySuccesses = 0;
        Trust = 0;
        DeferredWages = 0;
        LastAssignmentSwapDay = null;
        ClearActiveDuty();
        LastOfferDay = null;
        LastIncidentDay = null;
        RecentDutyIds.Clear();
        HighestIssuedKitTier = null;
        IssuedEquipmentItemIds.Clear();
    }

    public void ClearActiveDuty()
    {
        ActiveDutyId = null;
        ActiveDutyTargetPartyId = null;
        ActiveDutySettlementId = null;
        ActiveDutyDeadlineDay = null;
        ShiftEndDay = null;
    }

    public ServiceProgressSnapshot ToProgressSnapshot(int leadershipSkill)
    {
        return new ServiceProgressSnapshot
        {
            Rank = Rank,
            Assignment = Assignment,
            DaysServed = DaysServed,
            ServiceXp = ServiceXp,
            LeadershipSkill = leadershipSkill,
            DutySuccesses = DutySuccesses,
            Trust = Trust,
            DeferredWages = DeferredWages,
        };
    }

    public string Serialize()
    {
        var inv = CultureInfo.InvariantCulture;
        var parts = new List<string>
        {
            "rank=" + ((int)Rank).ToString(inv),
            "assignment=" + ((int)Assignment).ToString(inv),
            "xp=" + ServiceXp.ToString(inv),
            "days=" + DaysServed.ToString(inv),
            "dutyWins=" + DutySuccesses.ToString(inv),
            "trust=" + Trust.ToString(inv),
            "arrears=" + DeferredWages.ToString(inv),
        };
        AddDay(parts, "swapDay", LastAssignmentSwapDay);
        if (!string.IsNullOrEmpty(ActiveDutyId))
            parts.Add("dutyId=" + ActiveDutyId);
        if (!string.IsNullOrEmpty(ActiveDutyTargetPartyId))
            parts.Add("dutyParty=" + ActiveDutyTargetPartyId);
        if (!string.IsNullOrEmpty(ActiveDutySettlementId))
            parts.Add("dutyTown=" + ActiveDutySettlementId);
        AddDay(parts, "dutyDeadline", ActiveDutyDeadlineDay);
        AddDay(parts, "shiftEnd", ShiftEndDay);
        AddDay(parts, "offerDay", LastOfferDay);
        AddDay(parts, "incidentDay", LastIncidentDay);
        if (RecentDutyIds.Count > 0)
            parts.Add("recent=" + string.Join(",", RecentDutyIds));
        // The key stays "kitRank" for save compatibility.
        if (HighestIssuedKitTier.HasValue)
            parts.Add("kitRank=" + HighestIssuedKitTier.Value.ToString(inv));
        if (IssuedEquipmentItemIds.Count > 0)
            parts.Add("kitItems=" + string.Join(",", IssuedEquipmentItemIds));
        return string.Join(";", parts);
    }

    public static bool TryParse(string serialized, out ServiceContentRecord record)
    {
        record = null;
        if (string.IsNullOrEmpty(serialized))
            return false;

        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var part in serialized.Split(';'))
        {
            var eq = part.IndexOf('=');
            if (eq > 0)
                map[part.Substring(0, eq)] = part.Substring(eq + 1);
        }

        if (!map.TryGetValue("rank", out var rankRaw)
            || !int.TryParse(rankRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rankInt)
            || !Enum.IsDefined(typeof(ServiceRank), rankInt))
        {
            return false;
        }

        var parsed = new ServiceContentRecord
        {
            Rank = (ServiceRank)rankInt,
            Assignment = ParseEnum(map, "assignment", ServiceAssignment.Infantry),
            ServiceXp = ParseInt(map, "xp"),
            DaysServed = ParseInt(map, "days"),
            DutySuccesses = ParseInt(map, "dutyWins"),
            Trust = ParseInt(map, "trust"),
            DeferredWages = ParseInt(map, "arrears"),
            LastAssignmentSwapDay = ParseDay(map, "swapDay"),
            ActiveDutyId = ParseString(map, "dutyId"),
            ActiveDutyTargetPartyId = ParseString(map, "dutyParty"),
            ActiveDutySettlementId = ParseString(map, "dutyTown"),
            ActiveDutyDeadlineDay = ParseDay(map, "dutyDeadline"),
            ShiftEndDay = ParseDay(map, "shiftEnd"),
            LastOfferDay = ParseDay(map, "offerDay"),
            LastIncidentDay = ParseDay(map, "incidentDay"),
        };

        if (map.TryGetValue("recent", out var recent) && !string.IsNullOrEmpty(recent))
            parsed.RecentDutyIds = recent.Split(',').Where(s => !string.IsNullOrEmpty(s)).ToList();

        // The key stays "kitRank" for save compatibility.
        if (map.TryGetValue("kitRank", out var kitRank)
            && int.TryParse(kitRank, NumberStyles.Integer, CultureInfo.InvariantCulture, out var kitRankValue)
            && kitRankValue >= 0)
        {
            parsed.HighestIssuedKitTier = kitRankValue;
        }

        if (map.TryGetValue("kitItems", out var kitItems) && !string.IsNullOrEmpty(kitItems))
            parsed.IssuedEquipmentItemIds = kitItems.Split(',').Where(s => !string.IsNullOrEmpty(s)).ToList();

        record = parsed;
        return true;
    }

    public void CopyFrom(ServiceContentRecord other)
    {
        if (other == null)
            return;
        Rank = other.Rank;
        Assignment = other.Assignment;
        ServiceXp = other.ServiceXp;
        DaysServed = other.DaysServed;
        DutySuccesses = other.DutySuccesses;
        Trust = other.Trust;
        DeferredWages = other.DeferredWages;
        LastAssignmentSwapDay = other.LastAssignmentSwapDay;
        ActiveDutyId = other.ActiveDutyId;
        ActiveDutyTargetPartyId = other.ActiveDutyTargetPartyId;
        ActiveDutySettlementId = other.ActiveDutySettlementId;
        ActiveDutyDeadlineDay = other.ActiveDutyDeadlineDay;
        ShiftEndDay = other.ShiftEndDay;
        LastOfferDay = other.LastOfferDay;
        LastIncidentDay = other.LastIncidentDay;
        RecentDutyIds = new List<string>(other.RecentDutyIds ?? new List<string>());
        HighestIssuedKitTier = other.HighestIssuedKitTier;
        IssuedEquipmentItemIds = new List<string>(other.IssuedEquipmentItemIds ?? new List<string>());
    }

    private static void AddDay(List<string> parts, string key, double? value)
    {
        if (value.HasValue)
            parts.Add(key + "=" + value.Value.ToString("R", CultureInfo.InvariantCulture));
    }

    private static int ParseInt(Dictionary<string, string> map, string key)
    {
        return map.TryGetValue(key, out var raw)
            && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;
    }

    private static string ParseString(Dictionary<string, string> map, string key)
    {
        return map.TryGetValue(key, out var raw) && !string.IsNullOrEmpty(raw) ? raw : null;
    }

    private static double? ParseDay(Dictionary<string, string> map, string key)
    {
        if (map.TryGetValue(key, out var raw)
            && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            && !double.IsNaN(value) && !double.IsInfinity(value))
        {
            return value;
        }
        return null;
    }

    private static TEnum ParseEnum<TEnum>(Dictionary<string, string> map, string key, TEnum fallback)
        where TEnum : struct
    {
        if (map.TryGetValue(key, out var raw)
            && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            && Enum.IsDefined(typeof(TEnum), value))
        {
            return (TEnum)(object)value;
        }
        return fallback;
    }
}
