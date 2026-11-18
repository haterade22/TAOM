using System;

namespace TAOM.Features.CampaignStartOptions;

/// <summary>What a new campaign gets for a setting the player left alone (MCM, else JSON).</summary>
public sealed class StartDefaults
{
    public StartDefaults(
        bool warOfTheRing, int phase1Day, int phase2Day, bool troopWeight,
        bool momentumVictory = false, bool eyeOfSauron = true, bool aiPartyScaling = true, AlignmentRules? alignment = null)
    {
        WarOfTheRing = warOfTheRing;
        Phase1Day = phase1Day;
        Phase2Day = phase2Day;
        TroopWeight = troopWeight;
        MomentumVictory = momentumVictory;
        EyeOfSauron = eyeOfSauron;
        AiPartyScaling = aiPartyScaling;
        Alignment = alignment ?? AlignmentRules.Strict;
    }

    public bool WarOfTheRing { get; }
    public int Phase1Day { get; }
    public int Phase2Day { get; }
    public bool TroopWeight { get; }

    /// <summary>"War can be won": MCM, else JSON, for a new campaign.</summary>
    public bool MomentumVictory { get; }

    /// <summary>"Eye of Sauron hunters".</summary>
    public bool EyeOfSauron { get; }

    /// <summary>"Large AI armies".</summary>
    public bool AiPartyScaling { get; }

    /// <summary>The four alignment switches as MCM (else JSON) has them now; Standard follows these.</summary>
    public AlignmentRules Alignment { get; }
}

/// <summary>
/// The values the screen wrote into the campaign. A null member means the key is absent: the player
/// left that option alone, or it was hidden (<c>GetChangedOptions</c> keeps neither).
/// </summary>
public sealed class ScreenValues
{
    public string? Scenario { get; set; }
    public string? StartDate { get; set; }
    public int? CustomDay1 { get; set; }
    public int? CustomDay2 { get; set; }
    public bool? TroopWeight { get; set; }
    public bool? WarCanBeWon { get; set; }
    public bool? EyeHunters { get; set; }
    public bool? LargeAiArmies { get; set; }
    public string? AlignmentPreset { get; set; }
}

public sealed class ResolvedStartOptions
{
    public ResolvedStartOptions(
        bool warOfTheRing, int phase1Day, int phase2Day, bool troopWeight,
        bool momentumVictory, bool eyeOfSauron, bool aiPartyScaling, string alignmentPreset, string? warning)
    {
        MomentumVictory = momentumVictory;
        EyeOfSauron = eyeOfSauron;
        AiPartyScaling = aiPartyScaling;
        AlignmentPreset = alignmentPreset;
        WarOfTheRing = warOfTheRing;
        Phase1Day = phase1Day;
        Phase2Day = phase2Day;
        TroopWeight = troopWeight;
        Warning = warning;
    }

    public bool WarOfTheRing { get; }
    public int Phase1Day { get; }
    public int Phase2Day { get; }
    public bool TroopWeight { get; }
    public bool MomentumVictory { get; }
    public bool EyeOfSauron { get; }
    public bool AiPartyScaling { get; }

    /// <summary>One of the three <c>Alignment*Item</c> ids; Standard pins no rules.</summary>
    public string AlignmentPreset { get; }

    /// <summary>Set when a screen value was unknown and a default was used instead; null otherwise.</summary>
    public string? Warning { get; }
}

/// <summary>
/// Pure: screen values plus defaults give the resolved values a campaign pins. The fact it rests on: an
/// option the player leaves alone is absent from the campaign's data, so absent means "the default".
/// </summary>
public static class CampaignStartOptionsResolver
{
    public static ResolvedStartOptions Resolve(ScreenValues screen, StartDefaults defaults)
    {
        string? warning = null;

        // Absent scenario: the screen showed the default, which is the War of the Ring when the MCM/JSON
        // switch was on. Any scenario id but ours (Sandbox, or another mod's) means no scripted war.
        bool wotr = screen.Scenario == null
            ? defaults.WarOfTheRing
            : screen.Scenario == CampaignStartOptionKeys.WarOfTheRingScenarioItem;

        int day1 = defaults.Phase1Day;
        int day2 = defaults.Phase2Day;
        switch (screen.StartDate)
        {
            case null:
            case CampaignStartOptionKeys.StartStandardItem:
                break;
            case CampaignStartOptionKeys.StartEarlyItem:
                day1 = CampaignStartOptionKeys.EarlyDay1;
                day2 = CampaignStartOptionKeys.EarlyDay2;
                break;
            case CampaignStartOptionKeys.StartLateItem:
                day1 = CampaignStartOptionKeys.LateDay1;
                day2 = CampaignStartOptionKeys.LateDay2;
                break;
            case CampaignStartOptionKeys.StartCustomItem:
                day1 = screen.CustomDay1 ?? defaults.Phase1Day;
                day2 = screen.CustomDay2 ?? defaults.Phase2Day;
                break;
            default:
                warning = $"unknown start-date preset '{screen.StartDate}', using the standard days";
                break;
        }

        (day1, day2) = ClampPair(day1, day2);

        // An absent preset is Standard. A preset this build does not know is Standard too, with a warning.
        string alignment = CampaignStartOptionKeys.AlignmentStandardItem;
        if (screen.AlignmentPreset != null)
        {
            if (AlignmentRules.IsKnownPreset(screen.AlignmentPreset))
                alignment = screen.AlignmentPreset;
            else
                warning = (warning == null ? "" : warning + "; ")
                    + $"unknown alignment preset '{screen.AlignmentPreset}', using Standard";
        }

        return new ResolvedStartOptions(
            wotr,
            day1,
            day2,
            screen.TroopWeight ?? defaults.TroopWeight,
            screen.WarCanBeWon ?? defaults.MomentumVictory,
            screen.EyeHunters ?? defaults.EyeOfSauron,
            screen.LargeAiArmies ?? defaults.AiPartyScaling,
            alignment,
            warning);
    }

    /// <summary>Day 1 in [1, 364], day 2 in [day1 + 1, 365]: phase 2 lands strictly after phase 1.</summary>
    public static (int Day1, int Day2) ClampPair(int day1, int day2)
    {
        day1 = Math.Min(Math.Max(day1, CampaignStartOptionKeys.MinDay), CampaignStartOptionKeys.MaxDay - 1);
        day2 = Math.Min(Math.Max(day2, day1 + 1), CampaignStartOptionKeys.MaxDay);
        return (day1, day2);
    }

    /// <summary>True when the pair is one a pinned campaign may carry (both in range, day 2 after day 1).</summary>
    public static bool IsValidPair(int day1, int day2) =>
        day1 >= CampaignStartOptionKeys.MinDay && day2 <= CampaignStartOptionKeys.MaxDay && day2 > day1;
}
