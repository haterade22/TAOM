using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TAOM.Features.TroopWeight.Diagnostics;

/// <summary>
/// Engine-free description of one member-roster slot for the troop-count diagnostic dump.
/// </summary>
public readonly struct DiagnosticSlot
{
    public string TroopId { get; }
    public int Number { get; }
    public int WoundedNumber { get; }
    public float Weight { get; }
    public bool IsSpecialCurrency { get; }

    public DiagnosticSlot(string troopId, int number, int woundedNumber, float weight, bool isSpecialCurrency)
    {
        TroopId = troopId;
        Number = number;
        WoundedNumber = woundedNumber;
        Weight = weight;
        IsSpecialCurrency = isSpecialCurrency;
    }
}

/// <summary>
/// Engine-free snapshot of the main party's count state at the moment the party screen was opened.
/// Carries everything the diagnostic needs so the (pure, tested) formatter never touches a sealed
/// TaleWorlds type.
/// </summary>
public readonly struct DiagnosticSnapshot
{
    public bool EnableTroopWeight { get; }
    public int SlotCount { get; }
    public int TotalManCount { get; }
    public int TotalWounded { get; }
    public int NumberOfAllMembers { get; }
    public int NumberOfHealthyMembers { get; }
    public int WeightedHealthy { get; }
    public int WeightedWounded { get; }
    public int PartySizeLimit { get; }
    public IReadOnlyList<DiagnosticSlot> Slots { get; }

    public DiagnosticSnapshot(
        bool enableTroopWeight,
        int slotCount,
        int totalManCount,
        int totalWounded,
        int numberOfAllMembers,
        int numberOfHealthyMembers,
        int weightedHealthy,
        int weightedWounded,
        int partySizeLimit,
        IReadOnlyList<DiagnosticSlot> slots)
    {
        EnableTroopWeight = enableTroopWeight;
        SlotCount = slotCount;
        TotalManCount = totalManCount;
        TotalWounded = totalWounded;
        NumberOfAllMembers = numberOfAllMembers;
        NumberOfHealthyMembers = numberOfHealthyMembers;
        WeightedHealthy = weightedHealthy;
        WeightedWounded = weightedWounded;
        PartySizeLimit = partySizeLimit;
        Slots = slots ?? new List<DiagnosticSlot>();
    }
}

/// <summary>
/// Pure formatter for the <c>[TroopCountDiag]</c> log block. Turns an engine-free snapshot into
/// ready-to-log lines: a header, one line per roster slot, a special-currency summary, and a
/// MISMATCH warning when the summed slot bodies disagree with the cached <c>TotalManCount</c>
/// (the stale-count hypothesis). No TaleWorlds dependency — fully unit-testable.
/// </summary>
public static class TroopCountDiagnosticsFormatter
{
    public const string Prefix = "[TroopCountDiag]";

    public static IReadOnlyList<string> Format(DiagnosticSnapshot s)
    {
        var lines = new List<string>();
        var inv = CultureInfo.InvariantCulture;

        lines.Add(
            $"{Prefix} main party: EnableTroopWeight={s.EnableTroopWeight} slots={s.SlotCount} " +
            $"totalManCount={s.TotalManCount} healthy={s.TotalManCount - s.TotalWounded} wounded={s.TotalWounded} " +
            $"NumberOfAllMembers={s.NumberOfAllMembers} NumberOfHealthyMembers={s.NumberOfHealthyMembers} " +
            $"weighted=(healthy {s.WeightedHealthy}, wounded {s.WeightedWounded}) partySizeLimit={s.PartySizeLimit}");

        int bodySum = 0;
        int specialTypes = 0;
        int specialBodies = 0;
        var specialList = new StringBuilder();

        foreach (var slot in s.Slots)
        {
            bodySum += slot.Number;

            lines.Add(
                $"{Prefix}   slot: id={slot.TroopId} number={slot.Number} wounded={slot.WoundedNumber} " +
                $"weight={slot.Weight.ToString("0.0", inv)} special={slot.IsSpecialCurrency}");

            if (slot.IsSpecialCurrency && slot.Number > 0)
            {
                specialTypes++;
                specialBodies += slot.Number;
                if (specialList.Length > 0)
                    specialList.Append(", ");
                specialList.Append(slot.TroopId).Append(" x").Append(slot.Number.ToString(inv));
            }
        }

        // Stale-count detector: the roster's per-slot bodies must sum to the cached TotalManCount.
        // A gap means troops entered the slots without bumping the cached total (hypothesis: stale count).
        if (s.SlotCount > 0 && bodySum != s.TotalManCount)
        {
            lines.Add(
                $"{Prefix} MISMATCH: slot bodies sum={bodySum} but TotalManCount={s.TotalManCount} " +
                "(cached count is stale relative to the roster slots)");
        }

        if (specialTypes > 0)
        {
            lines.Add(
                $"{Prefix} special-currency troops in main party: types={specialTypes} bodies={specialBodies} " +
                $"[{specialList}]");
        }
        else
        {
            lines.Add($"{Prefix} special-currency troops in main party: NONE present");
        }

        return lines;
    }
}
