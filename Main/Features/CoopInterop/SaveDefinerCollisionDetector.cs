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
    // 726900701 / 726900801 / 726900901 and the next free one by the +100 convention is 726901001.
    // (That "next free" line went stale the moment FiefGranting claimed 726900901, which is the
    // drift this comment's own first sentence warns about. Trust the reflection test, not this.)

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
            // Only groups a player can ACT on. Base-id equality is a heuristic, not proof: the real
            // save id is `_saveBaseId + saveId` (SaveableTypeDefiner.AddClassDefinition, v1.4.7), so
            // two definers can share a base id and never collide if their offsets differ — and
            // vanilla does exactly that. SaveableCoreTypeDefiner (TaleWorlds.Core) and
            // SaveableObjectSystemTypeDefiner (TaleWorlds.ObjectSystem) both use 10000 in a game
            // that starts fine, and because they are in different assemblies the old code took the
            // cross-assembly branch and told players to "disable one of them" — naming two vanilla
            // engine types, at the top of every user log we collected.
            //
            // A group of purely game-shipped assemblies is never actionable and, as proven above,
            // not necessarily a fault at all. Requiring one non-engine member keeps every real
            // mod-vs-mod and mod-vs-vanilla case while removing the false positive.
            .Where(g => g.Any(r => !IsEngineAssembly(r.AssemblyName)))
            .Select(g => new SaveDefinerCollision(
                g.Key,
                g.ToList(),
                isCrossAssembly: g.Select(r => r.AssemblyName)
                                  .Distinct(StringComparer.OrdinalIgnoreCase)
                                  .Count() > 1))
            .OrderBy(c => c.BaseId)
            .ToList();
    }

    /// <summary>
    /// Assemblies that ship with the game. A player cannot disable any of these, so a collision
    /// confined to them is noise no matter what it means.
    /// </summary>
    private static readonly string[] EngineAssemblyNames =
    {
        "Native", "SandBox", "SandBoxCore", "StoryMode", "CustomBattle", "Multiplayer",
    };

    internal static bool IsEngineAssembly(string? assemblyName)
    {
        if (string.IsNullOrWhiteSpace(assemblyName)) return false;

        return assemblyName!.StartsWith("TaleWorlds.", StringComparison.OrdinalIgnoreCase)
               || EngineAssemblyNames.Any(n => string.Equals(n, assemblyName, StringComparison.OrdinalIgnoreCase));
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
