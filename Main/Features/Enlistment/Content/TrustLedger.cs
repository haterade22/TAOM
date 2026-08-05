using System;
using TAOM.Features.Enlistment.Content.Domain;

namespace TAOM.Features.Enlistment.Content;

/// <summary>Pure clamp helpers for trust and the four reputation domains.</summary>
public static class TrustLedger
{
    public static int AdjustTrust(int current, int delta, SchedulerConfig config)
    {
        var min = config?.TrustMin ?? -10;
        var max = config?.TrustMax ?? 20;
        return Math.Max(min, Math.Min(max, current + delta));
    }

    public static int AdjustReputation(int current, int delta, SchedulerConfig config)
    {
        var max = config?.ReputationMax ?? 50;
        return Math.Max(0, Math.Min(max, current + delta));
    }

    public static void ApplyReputation(ServiceContentRecord record, ReputationDomain domain, int delta, SchedulerConfig config)
    {
        if (record == null || delta == 0)
            return;
        switch (domain)
        {
            case ReputationDomain.Field: record.FieldRep = AdjustReputation(record.FieldRep, delta, config); break;
            case ReputationDomain.Logistics: record.LogisticsRep = AdjustReputation(record.LogisticsRep, delta, config); break;
            case ReputationDomain.Command: record.CommandRep = AdjustReputation(record.CommandRep, delta, config); break;
            case ReputationDomain.Siege: record.SiegeRep = AdjustReputation(record.SiegeRep, delta, config); break;
        }
    }

    /// <summary>Dominant reputation domain for officer-duty selection + status flavor; None on all-zero.</summary>
    public static ReputationDomain DominantDomain(ServiceContentRecord record)
    {
        if (record == null)
            return ReputationDomain.None;
        var best = ReputationDomain.None;
        var bestValue = 0;
        if (record.FieldRep > bestValue) { best = ReputationDomain.Field; bestValue = record.FieldRep; }
        if (record.LogisticsRep > bestValue) { best = ReputationDomain.Logistics; bestValue = record.LogisticsRep; }
        if (record.CommandRep > bestValue) { best = ReputationDomain.Command; bestValue = record.CommandRep; }
        if (record.SiegeRep > bestValue) { best = ReputationDomain.Siege; bestValue = record.SiegeRep; }
        return best;
    }
}
