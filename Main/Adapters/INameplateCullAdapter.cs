// Adapted from yotthani's VanillaTuning (MIT, (c) 2026 yotthani), nameplate-cull.
using TAOM.Features.NameplateCull.Models;

namespace TAOM.Adapters;

/// <summary>
/// The engine side of the nameplate cull: the private members of <c>SettlementNameplatesVM</c> and
/// <c>SettlementNameplateVM</c>, bound once, and the culled update itself. The update runs on the main thread.
/// </summary>
public interface INameplateCullAdapter
{
    /// <summary>Binds every engine member the update reads or calls. Null on success, otherwise why the cull cannot run on this game build.</summary>
    string? Initialize();

    /// <summary>
    /// <c>SettlementNameplatesVM.Update()</c> with the hidden plates left out: the camera is read once and handed to the
    /// loop (vanilla's private camera cache is not written: only vanilla's own loop reads it), the plates that may not be
    /// skipped update in parallel, then push their values, in the engine's order.
    /// </summary>
    /// <param name="nameplates">The <c>SettlementNameplatesVM</c>; anything else throws.</param>
    CullCounts UpdateCulled(object nameplates);
}
