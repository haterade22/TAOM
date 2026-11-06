using System.Collections.Generic;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Features.LongDefeat;

/// <summary>A recorded keep point: the centre of the zone attackers must hold, and its radius in metres.</summary>
internal sealed record KeepPoint(float X, float Y, float Z, float Radius);

/// <summary>
/// One valid row of fortresses.json with its wave roster and shop already resolved, so a fortress that exists always has both.
/// <paramref name="Name"/> is the raw <c>{=KEY}English</c> text; the client resolves it. <paramref name="Keep"/> is null until a
/// point is recorded, and the director then falls back to the scene's reinforcement frame. <paramref name="Sally"/> is the optional place a mounted class rides out to at each wave's start
/// (the muster shape); null until Mike records one, and a mounted class then rides from the start with no sally.
/// </summary>
internal sealed record FortressEntry(
    string Id,
    string Name,
    string Scene,
    string DefenderCulture,
    string AttackerCulture,
    string PlayerTroop,
    IReadOnlyList<GarrisonRow> Garrison,
    WavePlan Waves,
    ShopPlan Shop,
    KeepPoint? Keep,
    MusterPoints? Muster,
    MusterFrame? Sally = null);
