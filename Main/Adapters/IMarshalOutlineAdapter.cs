using System.Collections.Generic;

namespace TAOM.Adapters;

/// <summary>The engine side of the outlines, generic over the handle a unit is keyed by.</summary>
internal interface IMarshalOutlineAdapter<TKey>
    where TKey : notnull
{
    /// <summary>Adds the colour of each unit that should wear an outline now: the selected companies' units, and the units of the company each one marks or charges, and the mount of each unit that rides, as a unit of its own.</summary>
    void ReadWanted(Dictionary<TKey, uint> into);

    /// <summary>Paints the unit, or clears it with a null colour. Only a unit that was read as wanted is ever painted, and only a painted one is cleared. A unit that is no longer the slot's occupant is skipped.</summary>
    void Paint(TKey unit, uint? colour);
}
