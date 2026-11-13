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
/// One valid row of <c>story_missions.json</c>: the scene, the two cultures, the player's garrison and the waves that attack it, with the
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
    string? Music = null);
