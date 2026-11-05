using System;
using System.Collections.Generic;
using System.Diagnostics;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using TAOM.Core.Logging;

namespace TAOM.Features.TroopWeight.Diagnostics;

// TEMPORARY diagnostic. Player report 2026-10-17: about 60 non-hero troops vanished from the main
// party right after buying Elite Emissary troops. No TAOM path found removes a whole roster at once:
// AlignmentDesertion removes daily and shows a popup, special-resource desertion takes 10% per type
// per day. This prefix logs the caller chain of every removal of non-hero troops from the MAIN party.
//
// The funnel: TroopRoster.AddToCounts, Clear, RemoveIf, RemoveTroop, RemoveNumberOfNonHeroTroopsRandomly
// and Add all reach AddToCountsAtIndex (v1.4.8 decompile, TroopRoster.cs:312). SetElementNumber is the one
// public write that skips it; the sibling TroopRoster_SetElementNumber_Patch covers it.
//
// Threads: removals can arrive from Mission.OnAgentRemoved (battle deaths via
// MapEventParty.OnTroopKilled -> RemoveTroop), possibly off the main thread. The patch therefore
// holds no mutable state except the log-once flag, and FileLogger is thread-safe.
//
// Logging only: this patch never changes a roster. Delete both patch files and
// MainRosterLossDiagnostics once the report is root-caused (checklist: harmony-patch-registry.md).
[HarmonyPatch(typeof(TroopRoster), nameof(TroopRoster.AddToCountsAtIndex))]
[HarmonyPatchCategory("Patch_MainRosterLossDiag")]
public static class TroopRoster_AddToCountsAtIndex_Patch
{
    private static IModLogger? _logger;
    private static bool _failureLogged;

    public static void Initialize(IModLogger logger)
    {
        _logger = logger;
    }

    static void Prefix(TroopRoster __instance, int index, int countChange)
    {
        if (countChange < 0 && countChange != int.MinValue)
            LogMainRemoval(__instance, index, -countChange);
    }

    /// <summary>Shared by the SetElementNumber sibling. Nothing may throw out of here.</summary>
    internal static void LogMainRemoval(TroopRoster roster, int index, int removed)
    {
        try
        {
            var logger = _logger;
            if (logger == null || removed <= 0)
                return;

            // Campaign.Current.MainParty, never PartyBase.MainParty (it throws while a campaign has no main party).
            var campaign = Campaign.Current;
            var main = campaign?.MainParty;
            if (campaign == null || main == null || !ReferenceEquals(roster, main.MemberRoster))
                return;

            if (index < 0 || index >= roster.Count)
                return;

            var element = roster.GetElementCopyAtIndex(index);
            if (element.Character == null || element.Character.IsHero)
                return;

            // The engine's TroopRosterElement.Number setter throws on a negative value, so an
            // over-removal aborts and nothing leaves the roster on that call.
            if (removed > element.Number)
                return;

            // LogDebug is queued, so the casualty path does no synchronous flush.
            logger.LogDebug(MainRosterLossDiagnostics.FormatRemoval(
                campaign.CurrentTickCount,
                element.Character.StringId,
                removed,
                element.Number,
                roster.TotalRegulars,
                CaptureFrames()));
        }
        catch (Exception ex)
        {
            if (_failureLogged)
                return;
            _failureLogged = true;
            _logger?.LogWarning($"{MainRosterLossDiagnostics.Prefix} diagnostic failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static IReadOnlyList<string> CaptureFrames()
    {
        var frames = new StackTrace(1, false).GetFrames();
        var result = new List<string>(frames?.Length ?? 0);
        if (frames == null)
            return result;

        foreach (var frame in frames)
        {
            var method = frame.GetMethod();
            if (method == null)
                continue;
            var d = method.DeclaringType?.FullName;
            result.Add(string.IsNullOrEmpty(d) ? method.Name : d + "." + method.Name);
        }
        return result;
    }
}
