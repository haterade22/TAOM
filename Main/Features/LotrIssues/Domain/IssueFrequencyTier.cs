namespace TAOM.Features.LotrIssues.Domain;

/// <summary>
/// TAOM-owned mirror of the engine's <c>IssueBase.IssueFrequency</c> (VeryCommon/Common/Rare), kept
/// here so the domain + service + config provider stay free of TaleWorlds types and unit-testable.
/// The template boundary maps this to the engine enum when constructing <c>PotentialIssueData</c>.
/// </summary>
public enum IssueFrequencyTier
{
    Rare,
    Common,
    VeryCommon
}
