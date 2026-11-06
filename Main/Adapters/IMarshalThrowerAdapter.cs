using System.Collections.Generic;
using TAOM.Features.MarshalsWord.Domain;

namespace TAOM.Adapters;

/// <summary>
/// Every read and the one write that throwers on an advance make on the player's team (build-spec 9.4). A company is <c>Formation.Index</c>.
/// Main thread only, from <c>OnMissionTick</c>; no member writes an agent or a formation.
/// </summary>
internal interface IMarshalThrowerAdapter
{
    /// <summary>Mission time in seconds.</summary>
    double Now { get; }

    /// <summary>The player commands the team: <c>IsPlayerGeneral || IsPlayerSergeant</c>.</summary>
    bool PlayerCommands { get; }

    ThrowerCompany Company(int company);

    /// <summary>Walks the company's soldiers once.</summary>
    ThrowerCensus Census(int company);

    /// <summary>The throwing range of the company's armed soldiers from item data, or null when it has none; read at a run's start, with the speed multiplier of the weapon each soldier holds then.</summary>
    float? Range(int company);

    /// <summary>Every gate and place the stand rules read. The setting is true here and the range is null: the driver supplies both.</summary>
    ThrowerGates Gates(int company);

    /// <summary>Publishes the stand points to the patches with one reference write; an empty list writes null.</summary>
    void Publish(IReadOnlyList<ThrowerPoint> points);
}
