using System;
using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.CoopInterop;

/// <inheritdoc cref="ISaveDefinerCollisionDetector"/>
public sealed class SaveDefinerCollisionDetector : ISaveDefinerCollisionDetector
{
    // TAOM's own base ids are deliberately NOT restated here. A hardcoded copy is a drift site that
    // asserts nothing: a fifth definer added by copy-paste would collide while the list stayed
    // green. `PresetSaveableTypeDefinerTests.BaseId_UniqueAcrossDiscoverableDefinersInTaomAssembly`
    // already reflects over the TAOM assembly's real SaveableTypeDefiner subclasses, which is the
    // check that actually holds. For the record, the shipped ids are 726900501 / 726900601 /
    // 726900701 / 726900801 and the next free one by the +100 convention is 726900901.

    public IReadOnlyList<SaveDefinerCollision> Detect(IEnumerable<SaveDefinerRecord>? records)
    {
        if (records == null) return Array.Empty<SaveDefinerCollision>();

        // De-duplicate identical (assembly, type) pairs first: assembly enumeration can surface the
        // same definer twice (a type reached through two load contexts), and one definer is not in
        // conflict with itself.
        var distinct = records
            .Where(r => r != null)
            .GroupBy(r => (r.AssemblyName, r.TypeName), TupleComparer)
            .Select(g => g.First())
            .ToList();

        return distinct
            .GroupBy(r => r.BaseId)
            .Where(g => g.Count() > 1)
            .Select(g => new SaveDefinerCollision(
                g.Key,
                g.ToList(),
                isCrossAssembly: g.Select(r => r.AssemblyName)
                                  .Distinct(StringComparer.OrdinalIgnoreCase)
                                  .Count() > 1))
            .OrderBy(c => c.BaseId)
            .ToList();
    }

    private static readonly IEqualityComparer<(string AssemblyName, string TypeName)> TupleComparer =
        new AssemblyTypeComparer();

    private sealed class AssemblyTypeComparer : IEqualityComparer<(string AssemblyName, string TypeName)>
    {
        public bool Equals((string AssemblyName, string TypeName) x, (string AssemblyName, string TypeName) y) =>
            string.Equals(x.AssemblyName, y.AssemblyName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.TypeName, y.TypeName, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string AssemblyName, string TypeName) obj)
        {
            unchecked
            {
                var h = StringComparer.OrdinalIgnoreCase.GetHashCode(obj.AssemblyName ?? string.Empty);
                return (h * 397) ^ StringComparer.OrdinalIgnoreCase.GetHashCode(obj.TypeName ?? string.Empty);
            }
        }
    }
}
