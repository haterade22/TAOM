using System;
using System.Collections.Generic;
using TAOM.Features.LongDefeat;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Features.StoryMissions.Domain;

/// <summary>Which side of the walls the player takes. It decides what each way a run ends means to him.</summary>
internal enum StorySide
{
    Defending,
    Attacking,
}

/// <summary>
/// One valid row of <c>story_missions.json</c>: the scene, the two cultures, the garrison (the defenders; the player's in a Defend row) and the waves that attack it, with the
/// rule numbers and the recorded keep and muster points of that scene. <paramref name="Name"/> is raw <c>{=KEY}English</c> text.
/// <paramref name="Music"/> is the mission's own track, a path under the module's <c>ModuleSounds</c> folder; null keeps the campaign's music.
/// </summary>
internal sealed record StoryMissionDefinition(
    string Id,
    string Name,
    string Scene,
    StorySide Side,
    string DefenderCulture,
    string AttackerCulture,
    IReadOnlyList<GarrisonRow> Garrison,
    WavePlan Waves,
    RunRules Rules,
    KeepPoint? Keep,
    MusterPoints? Muster,
    string? Music = null)
{
    /// <summary>The heroes who stand free with the garrison from the first tick (#1366), at most four; none when the file lists none.</summary>
    public IReadOnlyList<HeroSpawn> Heroes { get; init; } = Array.Empty<HeroSpawn>();

    /// <summary>The start places of a mission on a field scene (#1105 slice 2), or null for a siege scene.</summary>
    public FieldPlaces? Field { get; init; }

    /// <summary>The id of the hero of <see cref="Heroes"/> who must live (<see cref="RunPlan.MustLive"/>), or null.</summary>
    public string? MustLive { get; init; }
}
