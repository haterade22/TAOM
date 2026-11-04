using System.Collections.Generic;

namespace TAOM.Features.MarshalsWord.Domain;

/// <summary>One queued order for one or more companies, each a <c>Formation.Index</c>, with a payload that the domain does not read.</summary>
internal sealed class QueueStep
{
    internal readonly long Id;
    internal readonly List<int> Companies;
    internal readonly StepEnd End;
    internal readonly object? Payload;
    internal bool NeedsWaitTest;

    /// <summary>The last member of the Move ahead of this step arrived while this step had not started, so this step waited for that walk.</summary>
    internal bool Behind;

    internal bool Started;
    internal double StartedAt;
    internal double Patience;

    internal QueueStep(long id, List<int> companies, StepEnd end, object? payload, bool needsWaitTest)
    {
        Id = id;
        Companies = companies;
        End = end;
        Payload = payload;
        NeedsWaitTest = needsWaitTest;
    }
}
