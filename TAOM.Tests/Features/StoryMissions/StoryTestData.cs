using System;
using TAOM.Features.LongDefeat;
using TAOM.Features.LongDefeat.Domain;
using TAOM.Features.StoryMissions.Domain;

namespace TAOM.Tests.Features.StoryMissions;

internal static class StoryTestData
{
    internal static WavePlan Waves(int count = 1) =>
        new(Enumerable(count), new WaveScaling(10, 300));

    private static WaveSpec[] Enumerable(int count)
    {
        var waves = new WaveSpec[count];
        for (var i = 0; i < count; i++)
            waves[i] = new WaveSpec(110 + i, new[] { new WaveTroop("a1", FormationSlot.Infantry, 3), new WaveTroop("a2", FormationSlot.Ranged, 1) });
        return waves;
    }

    internal static StoryMissionDefinition Definition(StorySide side = StorySide.Defending, int waves = 1, KeepPoint? keep = null, MusterPoints? muster = null,
        RunRules? rules = null) =>
        new("m1", "{=!}Mission one", "scene_a", side, "vlandia", "isengard",
            new[] { new GarrisonRow("g1", FormationSlot.Infantry, 100), new GarrisonRow("g2", FormationSlot.Ranged, 50) },
            Waves(waves), rules ?? RunRules.Default, keep, muster);
}
