using System.Collections.Generic;

namespace TAOM.Features.CoopInterop;

/// <summary>
/// Groups discovered <c>SaveableTypeDefiner</c>s by base save id and reports every id claimed more
/// than once. Pure — the assembly walk that produces the records lives in the entry point.
/// </summary>
public interface ISaveDefinerCollisionDetector
{
    IReadOnlyList<SaveDefinerCollision> Detect(IEnumerable<SaveDefinerRecord>? records);
}
