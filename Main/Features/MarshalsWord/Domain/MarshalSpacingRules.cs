using System;

namespace TAOM.Features.MarshalsWord.Domain;

/// <summary>The four spacing limits of a company: <c>MinimumDistance</c>, <c>MaximumDistance</c>, <c>MinimumInterval</c> and <c>MaximumInterval</c> (Formation.cs:587-595).</summary>
internal readonly record struct SpacingLimits(float MinDistance, float MaxDistance, float MinInterval, float MaxInterval);

/// <summary>The company the spacing proof read: its soldiers and riders, the four limits it answers, and the four it would answer as foot soldiers and as cavalry.</summary>
internal readonly record struct SpacingRead(int Company, int Units, int Riders, SpacingLimits Measured, SpacingLimits Infantry, SpacingLimits Cavalry);

internal enum SpacingReading
{
    Infantry,
    Cavalry,
    Mixed,
    NoTest,
}

internal static class MarshalSpacingRules
{
    /// <summary>Why the repair does not apply, or an empty string when it does. Co-op and a dedicated server keep it off so that two machines never compute different widths (build-spec 9.3).</summary>
    internal static string WhyNot(bool enabled, bool repair, bool coopActive, bool dedicatedServer)
    {
        if (!enabled)
            return "setting-off";
        if (!repair)
            return "repair-off";
        if (coopActive)
            return "coop";
        return dedicatedServer ? "dedicated-server" : "";
    }

    /// <summary>
    /// How a company's limits read. Only the limits on which infantry and cavalry differ can tell, and when none does the reading proves nothing.
    /// All of those at the infantry value is Infantry; all at the cavalry value is Cavalry; anything else, a number that is not finite included, is Mixed.
    /// </summary>
    internal static SpacingReading Read(SpacingLimits measured, SpacingLimits infantry, SpacingLimits cavalry)
    {
        var tested = 0;
        var asInfantry = 0;
        var asCavalry = 0;
        Tally(measured.MinDistance, infantry.MinDistance, cavalry.MinDistance, ref tested, ref asInfantry, ref asCavalry);
        Tally(measured.MaxDistance, infantry.MaxDistance, cavalry.MaxDistance, ref tested, ref asInfantry, ref asCavalry);
        Tally(measured.MinInterval, infantry.MinInterval, cavalry.MinInterval, ref tested, ref asInfantry, ref asCavalry);
        Tally(measured.MaxInterval, infantry.MaxInterval, cavalry.MaxInterval, ref tested, ref asInfantry, ref asCavalry);
        if (tested == 0)
            return SpacingReading.NoTest;
        if (asInfantry == tested)
            return SpacingReading.Infantry;
        return asCavalry == tested ? SpacingReading.Cavalry : SpacingReading.Mixed;
    }

    private static void Tally(float measured, float infantry, float cavalry, ref int tested, ref int asInfantry, ref int asCavalry)
    {
        if (!(Math.Abs(infantry - cavalry) > MarshalTuning.SpacingEpsilon))
            return;
        tested++;
        if (Math.Abs(measured - infantry) <= MarshalTuning.SpacingEpsilon)
            asInfantry++;
        else if (Math.Abs(measured - cavalry) <= MarshalTuning.SpacingEpsilon)
            asCavalry++;
    }
}
