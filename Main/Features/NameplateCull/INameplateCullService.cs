// Adapted from yotthani's VanillaTuning (MIT, (c) 2026 yotthani), nameplate-cull.
namespace TAOM.Features.NameplateCull;

/// <summary>
/// What Patch104's prefix asks once per campaign-map frame (docs/features/nameplate-cull.md). Neither member throws.
/// </summary>
public interface INameplateCullService
{
    /// <summary>
    /// Runs the vanilla nameplate update with the hidden plates left out and returns true, so the prefix skips vanilla.
    /// Returns false, having changed nothing the vanilla update does not redo, when the toggle is off, the cull is
    /// unavailable, it switched itself off after an error, or this call failed: vanilla then runs.
    /// </summary>
    /// <param name="nameplates">The <c>SettlementNameplatesVM</c> whose <c>Update()</c> is being replaced.</param>
    bool TryUpdate(object nameplates);

    /// <summary>Binds the engine members and writes the install line. Called once, from the module's GameInit step.</summary>
    void Install();
}
