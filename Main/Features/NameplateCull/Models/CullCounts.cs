// Adapted from yotthani's VanillaTuning (MIT, (c) 2026 yotthani), nameplate-cull.
namespace TAOM.Features.NameplateCull.Models;

/// <summary>How many nameplates one culled update ran and how many it left alone.</summary>
public readonly struct CullCounts
{
    public CullCounts(int updated, int skipped)
    {
        Updated = updated;
        Skipped = skipped;
    }

    public int Updated { get; }

    public int Skipped { get; }
}
