using System;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Features.StoryMissions.Domain;

/// <summary>
/// A story mission as a Long Defeat run plan: the definition's garrison, waves and rules, and nothing else of the mode. No shop and no gold, so no
/// pack, no fortification and no engineer's yard; no affixes, no boons and no class, so a wave carries nothing the definition did not write. The run
/// stops as <see cref="RunEndReason.TargetReached"/> after the authored waves, so the endless growth past the list never applies.
/// </summary>
internal static class StoryPlanBuilder
{
    public static RunPlan Build(StoryMissionDefinition definition, RunOptions options) =>
        new(definition.Id, definition.Garrison, definition.Waves, new ShopPlan(0, Array.Empty<ShopOffer>()), definition.Rules,
            options with { StopAfterWaves = definition.Waves.Authored.Count, ClassId = null })
        {
            Keep = definition.Keep,
            Muster = definition.Muster,
            Offered = FortOffer.None,
            Locations = LocationOffer.None,
        };
}
