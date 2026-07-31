using System.Collections.Generic;

namespace TAOM.Features.CoopInterop;

/// <summary>
/// One <c>SaveableTypeDefiner</c> found in a loaded assembly, reduced to the three facts needed to
/// detect and attribute a save-id collision.
/// </summary>
public sealed record SaveDefinerRecord(string AssemblyName, string TypeName, int BaseId);

/// <summary>
/// A set of definers sharing one base save id. The engine registers save definitions into a plain
/// dictionary keyed by id, so an exact collision throws during <c>Module.Initialize</c> with a
/// message that names neither mod.
/// </summary>
public sealed class SaveDefinerCollision
{
    public SaveDefinerCollision(int baseId, IReadOnlyList<SaveDefinerRecord> records, bool isCrossAssembly)
    {
        BaseId = baseId;
        Records = records;
        IsCrossAssembly = isCrossAssembly;
    }

    public int BaseId { get; }

    public IReadOnlyList<SaveDefinerRecord> Records { get; }

    /// <summary>
    /// True when the colliding definers come from different assemblies — a mod conflict. False
    /// means two definers inside one assembly, which is that mod's own defect (for TAOM, a
    /// copy-pasted definer) and must be reported with a different message.
    /// </summary>
    public bool IsCrossAssembly { get; }
}
