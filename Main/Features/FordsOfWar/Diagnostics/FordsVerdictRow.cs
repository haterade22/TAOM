using System.Collections.Generic;

namespace TAOM.Features.FordsOfWar.Diagnostics;

/// <summary>One named piece of a check, with its own result and the numbers behind it.</summary>
internal sealed record FordsPart(string Name, FordsResult Result, string Numbers);

internal sealed record FordsVerdictRow(int Check, string Title, FordsResult Result, IReadOnlyList<FordsPart> Parts, string Info,
    string Rule, string HumanOnly);
