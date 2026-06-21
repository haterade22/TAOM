namespace TAOM.Features.LotrIssues.Domain;

/// <summary>
/// Which kind of notable/hero offers an issue. The behavior's <c>OnCheckForIssue</c> eligibility gate
/// maps the polled hero's vanilla occupation to one of these; an issue only offers to a matching giver.
/// </summary>
public enum IssueGiverOccupation
{
    /// <summary>Village headman.</summary>
    Headman,

    /// <summary>Urban gang leader (criminal underworld).</summary>
    GangLeader,

    /// <summary>Town merchant.</summary>
    Merchant,

    /// <summary>Town artisan.</summary>
    Artisan,

    /// <summary>Rural notable (village landlord/elder, not the headman).</summary>
    RuralNotable,

    /// <summary>A landed lord (clan member with a fief / army).</summary>
    Lord
}
