namespace TAOM.Features.LotrIssues.Domain;

/// <summary>
/// The generic mechanic a <see cref="LotrIssueDefinition"/> instantiates. Each value maps to exactly
/// one <c>IssueBase</c>/<c>QuestBase</c> template pair; the XML config supplies the per-issue content
/// (counts, reward, culture filter, text keys). One enum value = one template branch = one test cell.
/// See <c>docs/features/lotr-issues.md</c> for the per-issue mapping.
/// </summary>
public enum LotrIssueTemplate
{
    /// <summary>Accumulate N of a culture/category-derived item and deliver to the giver.</summary>
    DeliverGoods,

    /// <summary>Deliver N bandit prisoners to the giver (gang recruits / forced mine labor).</summary>
    DeliverPersonnel,

    /// <summary>Combat objective: defeat N raids (won battles) or capture N enemy lords. The specific
    /// objective is the definition's <c>Variant</c> ("DefeatRaids" | "CaptureLords"). Covers the vanilla
    /// clear-hideout + defeat/capture-target archetypes.</summary>
    Combat,

    /// <summary>Protect a moving party (caravan/herd) to a destination; ambush variant.</summary>
    Escort,

    /// <summary>Market/price intervention, collection, fencing, or breaking a smuggling run.</summary>
    EconomicGather,

    /// <summary>Besiege / raid / scout fortifications / reinforce a garrison.</summary>
    ConquestMilitary,

    /// <summary>Low-mechanic social errands: go-to, shelter, find, tutor, tournament-dedicate.</summary>
    SocialMisc,

    /// <summary>Forge a crafted (smithing) item and deliver it.</summary>
    CraftItem
}
