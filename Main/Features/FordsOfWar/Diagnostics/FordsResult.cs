using System.Collections.Generic;

namespace TAOM.Features.FordsOfWar.Diagnostics;

internal enum FordsResult
{
    Pass,
    Fail,
    Partial,
    NotSeen,
}

internal static class FordsResults
{
    /// <summary>Fail if any part fails, Pass if every part passes, NotSeen if every part is NotSeen, otherwise Partial.</summary>
    internal static FordsResult Combine(IReadOnlyList<FordsPart> parts)
    {
        var pass = 0;
        var notSeen = 0;
        for (var i = 0; i < parts.Count; i++)
        {
            switch (parts[i].Result)
            {
                case FordsResult.Fail:
                    return FordsResult.Fail;
                case FordsResult.Pass:
                    pass++;
                    break;
                case FordsResult.NotSeen:
                    notSeen++;
                    break;
            }
        }

        if (parts.Count == 0 || notSeen == parts.Count)
            return FordsResult.NotSeen;
        return pass == parts.Count ? FordsResult.Pass : FordsResult.Partial;
    }

    internal static string Word(FordsResult r) => r switch
    {
        FordsResult.Pass => "PASS",
        FordsResult.Fail => "FAIL",
        FordsResult.Partial => "PARTIAL",
        _ => "NOT_SEEN",
    };

    internal static char Letter(FordsResult r) => r switch
    {
        FordsResult.Pass => 'P',
        FordsResult.Fail => 'F',
        FordsResult.Partial => 'R',
        _ => 'N',
    };
}
