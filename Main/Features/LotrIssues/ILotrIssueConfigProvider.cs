using System.Collections.Generic;
using TAOM.Features.LotrIssues.Domain;

namespace TAOM.Features.LotrIssues;

/// <summary>Loads + validates the LOTR issue definitions authored in <c>lotr_issues/taom_lotr_issues.xml</c>.</summary>
public interface ILotrIssueConfigProvider
{
    /// <summary>All valid issue definitions (cached for the process lifetime). Empty if the file is missing.</summary>
    IReadOnlyList<LotrIssueDefinition> LoadIssues();
}
