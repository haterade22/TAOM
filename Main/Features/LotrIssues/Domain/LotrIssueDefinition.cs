using System.Collections.Generic;

namespace TAOM.Features.LotrIssues.Domain;

/// <summary>
/// The localized text keys a LOTR issue + its quest render. All keys are <c>{=key}default</c> form;
/// the template fills runtime variables (<c>{ISSUE_SETTLEMENT}</c>, <c>{COUNT}</c>, <c>{TARGET}</c>, …)
/// via <c>SetTextVariable</c>. Immutable; never null (empty string when absent).
/// </summary>
public sealed class LotrIssueText
{
    public string TitleKey { get; }
    public string DescriptionKey { get; }
    public string BriefKey { get; }
    public string AcceptKey { get; }
    public string ExplanationKey { get; }
    public string SolutionAcceptKey { get; }
    public string TaskKey { get; }
    public string SuccessKey { get; }
    public string FailKey { get; }

    public LotrIssueText(
        string titleKey, string descriptionKey, string briefKey, string acceptKey,
        string explanationKey, string solutionAcceptKey, string taskKey, string successKey, string failKey)
    {
        TitleKey = titleKey ?? "";
        DescriptionKey = descriptionKey ?? "";
        BriefKey = briefKey ?? "";
        AcceptKey = acceptKey ?? "";
        ExplanationKey = explanationKey ?? "";
        SolutionAcceptKey = solutionAcceptKey ?? "";
        TaskKey = taskKey ?? "";
        SuccessKey = successKey ?? "";
        FailKey = failKey ?? "";
    }
}

/// <summary>
/// One config-driven LOTR issue: a generic <see cref="Template"/> mechanic parameterized with the
/// content (giver gate, frequency, culture filter, target counts, reward, item/troop sourcing, text).
/// Immutable; authored in <c>lotr_issues/taom_lotr_issues.xml</c> and validated by
/// <c>LotrIssueConfigProvider</c>. TaleWorlds-free so the domain + service are unit-testable.
/// </summary>
public sealed class LotrIssueDefinition
{
    public string Id { get; }
    public LotrIssueTemplate Template { get; }
    public IssueGiverOccupation GiverOccupation { get; }
    public IssueFrequencyTier Frequency { get; }

    /// <summary>Runtime culture StringIds this issue may spawn for; empty = all cultures.</summary>
    public IReadOnlyList<string> Cultures { get; }

    /// <summary>Base objective count (items to deliver, bands to clear, etc.); always &gt; 0 for count templates.</summary>
    public int Count { get; }

    /// <summary>Additional count scaled by issue difficulty (0..1); added to <see cref="Count"/>.</summary>
    public float CountPerDifficulty { get; }

    /// <summary>Item sourcing scheme: <c>category:&lt;ItemCategory&gt;</c> or <c>item:&lt;DefaultItemsMember&gt;</c>; empty if none.</summary>
    public string ItemSource { get; }

    /// <summary>Troop sourcing scheme: <c>basic|elite|bandit|mount|prisoners</c>; empty if none.</summary>
    public string TroopSource { get; }

    public int RewardGoldBase { get; }
    public float RewardGoldPerDifficulty { get; }
    public int RewardRenown { get; }

    /// <summary>Optional bonus item id granted on completion; empty if none.</summary>
    public string RewardItem { get; }

    /// <summary>How many of <see cref="RewardItem"/> completion grants (1 to 99; the XML's reward_count, default 1).</summary>
    public int RewardItemCount { get; }

    /// <summary>
    /// Offered only when the player's culture is one of <see cref="Cultures"/> too, not the giver's alone (the XML's
    /// for_player_culture): for a reward only those cultures can use, such as a lord's material (#693).
    /// </summary>
    public bool ForPlayerCulture { get; }

    /// <summary>Template-specific mode string (e.g. Combat "DefeatRaids"/"CaptureLords"); "" if none.</summary>
    public string Variant { get; }

    /// <summary>Minimum player↔giver relation for the issue to offer (engine default band is -10).</summary>
    public int RelationMin { get; }

    public LotrIssueText Text { get; }

    public LotrIssueDefinition(
        string id,
        LotrIssueTemplate template,
        IssueGiverOccupation giverOccupation,
        IssueFrequencyTier frequency,
        IReadOnlyList<string> cultures,
        int count,
        float countPerDifficulty,
        string itemSource,
        string troopSource,
        int rewardGoldBase,
        float rewardGoldPerDifficulty,
        int rewardRenown,
        string rewardItem,
        string variant,
        int relationMin,
        LotrIssueText text,
        int rewardItemCount = 1,
        bool forPlayerCulture = false)
    {
        Id = id;
        Template = template;
        GiverOccupation = giverOccupation;
        Frequency = frequency;
        Cultures = cultures ?? new List<string>();
        Count = count;
        CountPerDifficulty = countPerDifficulty;
        ItemSource = itemSource ?? "";
        TroopSource = troopSource ?? "";
        RewardGoldBase = rewardGoldBase;
        RewardGoldPerDifficulty = rewardGoldPerDifficulty;
        RewardRenown = rewardRenown;
        RewardItem = rewardItem ?? "";
        RewardItemCount = rewardItemCount;
        ForPlayerCulture = forPlayerCulture;
        Variant = variant ?? "";
        RelationMin = relationMin;
        Text = text ?? new LotrIssueText("", "", "", "", "", "", "", "", "");
    }

    /// <summary>
    /// Whether the issue suits a player of this culture: always, unless <see cref="ForPlayerCulture"/>. Checked when it
    /// is offered and each day the offer waits to be taken.
    /// </summary>
    public bool OffersTo(string playerCultureStringId) => !ForPlayerCulture || AppliesToCulture(playerCultureStringId);

    /// <summary>True if this issue may spawn for the given runtime culture StringId.</summary>
    public bool AppliesToCulture(string cultureStringId)
    {
        if (Cultures.Count == 0) return true;
        if (string.IsNullOrEmpty(cultureStringId)) return false;
        foreach (var c in Cultures)
            if (string.Equals(c, cultureStringId, System.StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
}
