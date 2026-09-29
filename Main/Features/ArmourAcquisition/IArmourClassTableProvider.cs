using System.Collections.Generic;
using TAOM.Features.ArmourAcquisition.Domain;

namespace TAOM.Features.ArmourAcquisition;

/// <summary>The generated armour_classes.xml, validated and cached for the process.</summary>
public interface IArmourClassTableProvider
{
    /// <summary>Rows keyed by item id (ordinal: the engine resolves ids case-sensitively).</summary>
    IReadOnlyDictionary<string, ArmourClassEntry> GetEntries();
}
