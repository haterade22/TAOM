using System.Collections.Generic;

namespace TAOM.Features.MixedFormations.Models;

/// <summary>
/// Cached per-formation slot map. Records (row, file) for every assigned unit and the
/// next-index counters for "new agent appears mid-mission" assignments.
/// </summary>
public sealed class SlotAssignment
{
    public FormationLayoutType Layout { get; }
    public int FilesPerRow { get; }
    public Dictionary<int, (int row, int file)> ByAgentIndex { get; } = new();
    public int NextMeleeIndex { get; set; }
    public int NextRangedIndex { get; set; }

    public SlotAssignment(FormationLayoutType layout, int filesPerRow)
    {
        Layout = layout;
        FilesPerRow = filesPerRow;
    }
}
