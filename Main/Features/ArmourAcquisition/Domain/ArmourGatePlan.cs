using System.Collections.Generic;

namespace TAOM.Features.ArmourAcquisition.Domain;

/// <summary>What <c>ArmourGatePlanner</c> decided for one game's loaded items.</summary>
public sealed class ArmourGatePlan
{
    public ArmourGatePlan(IReadOnlyDictionary<string, ArmourClass> classById, IReadOnlyList<string> toFlip,
        int fromTable, int fromEngineTier, IReadOnlyList<string> staleTableIds, IReadOnlyList<string> missingNamedWeapons)
    {
        ClassById = classById;
        ToFlip = toFlip;
        FromTable = fromTable;
        FromEngineTier = fromEngineTier;
        StaleTableIds = staleTableIds;
        MissingNamedWeapons = missingNamedWeapons;
    }

    /// <summary>Every governed loaded item: character armour and the named weapons.</summary>
    public IReadOnlyDictionary<string, ArmourClass> ClassById { get; }

    /// <summary>Gated pieces that are merchandise now and must stop being so (empty when gating is off).</summary>
    public IReadOnlyList<string> ToFlip { get; }

    public int FromTable { get; }

    /// <summary>Character armour the table does not list, classed by its engine tier.</summary>
    public int FromEngineTier { get; }

    /// <summary>Table rows naming no loaded item: the table predates an art drop.</summary>
    public IReadOnlyList<string> StaleTableIds { get; }

    public IReadOnlyList<string> MissingNamedWeapons { get; }
}
