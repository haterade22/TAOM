using System;
using System.Collections.Generic;
using TAOM.Core.Logging;
using TAOM.Core.Validation;
using TaleWorlds.CampaignSystem;

namespace TAOM.Features.Refuge.Domain;

/// <summary>
/// The save-load repair of the refuge book, moved out of <c>RefugeService.LoadFrom</c>
/// unchanged. The clock arrives as two delegates, bound by the service to its own
/// <c>DaysSince</c> and <c>NowTime</c> seams, and each is invoked only where the repair needs it:
/// a load whose rows are not building reads no clock at all.
/// </summary>
internal static class RefugeBookRepair
{
    /// <summary>Floor on a build's target hours so BuildProgress never divides by ~zero.</summary>
    internal const float MinBuildTargetHours = 0.1f;

    /// <summary>Scrubs null rows, then repairs every remaining row in place. The dictionary
    /// instance is kept (the service hands it back to SyncData by reference). The caller passes
    /// a non-null book.</summary>
    internal static void Repair(
        Dictionary<string, RefugeData> book,
        Func<CampaignTime, double> daysSince,
        Func<CampaignTime> now,
        IModLogger logger)
    {
        // A partially recovered or externally modified save can carry null rows; every tick path
        // dereferences the value, so scrub here rather than crash on the first FrameTick.
        List<string> nullKeys = null;
        foreach (var pair in book)
        {
            if (pair.Value == null)
                (nullKeys ?? (nullKeys = new List<string>())).Add(pair.Key);
        }
        if (nullKeys != null)
        {
            foreach (var key in nullKeys)
                book.Remove(key);
            logger.LogWarning($"[Refuge] dropped {nullKeys.Count} null book row(s) from the loaded save.");
        }
        foreach (var pair in book)
            RepairRow(pair.Key, pair.Value, daysSince, now, logger);
    }

    /// <summary>Canonicalizes and validates one persisted row (hostile or damaged saves). The
    /// DICTIONARY KEY is the identity every lookup resolves and every distance check measures, so
    /// a divergent inner PartyId would make Dismantle/visuals act on a DIFFERENT party than the
    /// one the player stands at -- cross-party destruction. Numeric repair kills the absorbing
    /// states: a non-finite BuildTargetHours on a Building row could otherwise never reach
    /// BuildProgress >= 1 (NaN fails every comparison; Infinity divides to 0) and the row eats a
    /// cap slot forever.</summary>
    private static void RepairRow(
        string key,
        RefugeData data,
        Func<CampaignTime, double> daysSince,
        Func<CampaignTime> now,
        IModLogger logger)
    {
        if (!string.Equals(data.PartyId, key, StringComparison.Ordinal))
        {
            logger.LogWarning(
                $"[Refuge] loaded row keyed '{key}' carried inner PartyId '{data.PartyId}'; canonicalized to the key.");
            data.PartyId = key;
        }
        if (data.Tier != (int)RefugeTier.Refuge && data.Tier != (int)RefugeTier.Stronghold)
        {
            logger.LogWarning($"[Refuge] row '{key}' had unknown tier {data.Tier}; reset to Refuge.");
            data.TierEnum = RefugeTier.Refuge;
        }
        // Only NON-FINITE targets are absorbing (Infinity never divides to >= 1; NaN fails every
        // comparison). Zero and negative targets already resolve as "done" in BuildProgress's
        // positive-requirement gate, so they are left alone.
        if (data.Building && !FiniteFloatValidator.IsFinite(data.BuildTargetHours))
        {
            logger.LogWarning(
                $"[Refuge] row '{key}' had non-finite BuildTargetHours {data.BuildTargetHours}; floored so the build can finish.");
            data.BuildTargetHours = MinBuildTargetHours;
        }
        // A FUTURE BuildStartTime is the same absorbing-state class from the other side: the
        // elapsed numerator stays negative for the whole offset, the build never finishes, and
        // the row is neither ready nor dismantlable while it eats a cap slot.
        if (data.Building && daysSince(data.BuildStartTime) < 0.0)
        {
            logger.LogWarning(
                $"[Refuge] row '{key}' had a future BuildStartTime; reset to now so the build can finish.");
            data.BuildStartTime = now();
        }
        if (data.MilitiaAdded < 0)
            data.MilitiaAdded = 0;
        if (data.MilitiaPreRallyCount < 0)
            data.MilitiaPreRallyCount = 0;
    }
}
