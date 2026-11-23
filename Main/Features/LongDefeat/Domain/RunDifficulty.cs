using System;

namespace TAOM.Features.LongDefeat.Domain;

/// <summary>How hard a run is. The values are written as one byte in the co-op payload, so never reorder them.</summary>
internal enum RunDifficulty
{
    Normal = 0,
    Hard = 1,
}

internal static class RunDifficulties
{
    /// <summary>The word the console, the autorun plan, the report and the prompt use.</summary>
    public static string Id(RunDifficulty difficulty) => difficulty == RunDifficulty.Hard ? "hard" : "normal";

    public static bool TryParse(string? word, out RunDifficulty difficulty)
    {
        difficulty = RunDifficulty.Normal;
        if (string.Equals(word, "normal", StringComparison.OrdinalIgnoreCase))
            return true;
        if (!string.Equals(word, "hard", StringComparison.OrdinalIgnoreCase))
            return false;

        difficulty = RunDifficulty.Hard;
        return true;
    }
}

/// <summary>What hard changes (waves.json <c>rules.hard</c>): the wave count and the health and damage growth are multiplied by <paramref name="Factor"/>, and each affix stage starts <paramref name="AffixWavesEarlier"/> waves sooner.</summary>
internal sealed record HardRules(float Factor, int AffixWavesEarlier)
{
    public static HardRules Default { get; } = new(1.25f, 2);
}
