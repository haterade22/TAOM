using TAOM.Adapters;
using TAOM.Features.MixedFormations.Models;

namespace TAOM.Features.MixedFormations;

public interface ILayoutPositioner
{
    /// <summary>
    /// Build a fresh slot assignment for the given formation under the requested layout.
    /// Pure function — same inputs produce the same outputs.
    /// </summary>
    SlotAssignment BuildInitialAssignment(IFormationAdapter formation, FormationLayoutType layout);

    /// <summary>
    /// Append-assign a new unit to an existing assignment (e.g., a soldier joining mid-battle).
    /// Mutates <paramref name="assignment"/> in place.
    /// </summary>
    (int row, int file) AssignNextSlot(SlotAssignment assignment, FormationUnit unit);
}
