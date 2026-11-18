namespace TAOM.Features.CampaignStartOptions;

/// <summary>
/// Every key and list-item id of the campaign start options. The screen keys are what the Advanced
/// Start Options screen writes into the campaign when the player changes a value; the resolved keys
/// are what TAOM pins at campaign start and what every reader reads. The two sets stay separate so the
/// screen can change later without migrating saves (docs/features/campaign-start-options.md).
/// </summary>
public static class CampaignStartOptionKeys
{
    // ---- screen keys (written by the engine, only when changed from the default and not hidden) ----

    /// <summary>Vanilla's scenario list. TAOM replaces it with War of the Ring or Sandbox.</summary>
    public const string ScenarioOption = "Scenario";
    public const string WarOfTheRingScenarioItem = "taom_war_of_the_ring";
    public const string SandboxScenarioItem = "none";

    public const string StartDateOption = "taom_wotr_start";
    public const string StartEarlyItem = "taom_wotr_start_early";
    public const string StartStandardItem = "taom_wotr_start_standard";
    public const string StartLateItem = "taom_wotr_start_late";
    public const string StartCustomItem = "taom_wotr_start_custom";

    public const string CustomDay1Option = "taom_wotr_custom_day1";
    public const string CustomDay2Option = "taom_wotr_custom_day2";

    public const string RulesCategory = "taom_rules";
    public const string TroopWeightOption = "taom_troop_weight";
    public const string WarCanBeWonOption = "taom_war_can_be_won";
    public const string EyeHuntersOption = "taom_eye_hunters";
    public const string LargeAiArmiesOption = "taom_large_ai_armies";

    /// <summary>The Alignment rules preset list and its three items (Standard follows MCM, pins nothing).</summary>
    public const string AlignmentPresetOption = "taom_alignment_preset";
    public const string AlignmentStandardItem = "taom_alignment_standard";
    public const string AlignmentStrictItem = "taom_alignment_strict";
    public const string AlignmentRelaxedItem = "taom_alignment_relaxed";

    // ---- resolved keys (pinned by TAOM at the start of a new campaign) ----

    public const string PinnedCategory = "taom_cso";
    public const string VersionKey = "taom_cso_version";
    public const string WarOfTheRingKey = "taom_cso_wotr";
    public const string Day1Key = "taom_cso_wotr_day1";
    public const string Day2Key = "taom_cso_wotr_day2";
    public const string TroopWeightKey = "taom_cso_troop_weight";
    public const string VictoryKey = "taom_cso_victory";
    public const string EyeOfSauronKey = "taom_cso_eye";
    public const string AiPartyScalingKey = "taom_cso_ai_scaling";

    /// <summary>The pinned preset id: one of the three <c>Alignment*Item</c> values (a string, so the console can show it).</summary>
    public const string AlignmentKey = "taom_cso_alignment";

    public const int CurrentVersion = 1;

    // ---- start-date presets (owner decision 18 Nov 2026: Early 10/20, Late 60/90, Standard = MCM sliders) ----

    public const int EarlyDay1 = 10;
    public const int EarlyDay2 = 20;
    public const int LateDay1 = 60;
    public const int LateDay2 = 90;

    /// <summary>The slider range of the custom days, and the range a pinned pair must sit in.</summary>
    public const int MinDay = 1;
    public const int MaxDay = 365;
}
