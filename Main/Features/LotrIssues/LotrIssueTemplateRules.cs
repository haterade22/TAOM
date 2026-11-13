using TaleWorlds.CampaignSystem.Issues;
using TAOM.Features.LotrIssues.Domain;

namespace TAOM.Features.LotrIssues;

/// <summary>
/// Rules the three issue templates (Combat, DeliverGoods, DeliverPersonnel) share, and the frequency map
/// <see cref="LotrIssuesCampaignBehavior"/> uses when it offers an issue. A static helper, never a base
/// class: the save system keys each saved field by the class level of its declaring type, so a class
/// between IssueBase and a template would change the id of the template's saved _defId. The engine's
/// IssueBase.PreconditionFlags is protected, so each template still builds its own flags.
/// </summary>
internal static class LotrIssueTemplateRules
{
    /// <summary>The relation floor when a template has no definition.</summary>
    internal const int DefaultRelationMin = -10;

    /// <summary>VeryCommon and Rare map to themselves; every other value maps to Common.</summary>
    internal static IssueBase.IssueFrequency MapFrequency(IssueFrequencyTier tier)
    {
        switch (tier)
        {
            case IssueFrequencyTier.VeryCommon: return IssueBase.IssueFrequency.VeryCommon;
            case IssueFrequencyTier.Rare: return IssueBase.IssueFrequency.Rare;
            default: return IssueBase.IssueFrequency.Common;
        }
    }

    /// <summary>A template's frequency: Common when its definition did not resolve.</summary>
    internal static IssueBase.IssueFrequency FrequencyOf(LotrIssueDefinition? def)
        => def == null ? IssueBase.IssueFrequency.Common : MapFrequency(def.Frequency);

    /// <summary>
    /// True when the relation is strictly below the definition's floor, or below -10 with no definition. The
    /// relation is a float because Hero.GetRelationWithPlayer returns one.
    /// </summary>
    internal static bool IsBelowRelationMin(float relationWithPlayer, LotrIssueDefinition? def)
        => relationWithPlayer < (def?.RelationMin ?? DefaultRelationMin);
}
