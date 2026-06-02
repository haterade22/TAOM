namespace TAOM.Features.CareerSystem.UI;

// Lifecycle wrapper for the in-battle CareerAbilityHUD Gauntlet layer (Issue #102 extract from
// CareerPerkMissionBehavior). Boundary class -- touches sealed TaleWorlds GauntletLayer +
// ScreenManager statics, so no dedicated unit tests; verify via in-battle smoke.
public interface IAbilityHudController
{
    // Attempt to create the layer + bind it to the current top screen. Idempotent and safe to
    // call every frame -- no-ops once attached and no-ops when no top screen is available yet.
    void TryInitialize();

    // Per-frame VM refresh. Caches the ability name + sprite path by heroId so the per-frame
    // string interpolation and TextObject allocation only happens when the active hero changes.
    void Refresh(string heroStringId);

    // Release the layer + finalize the VM. Called from OnEndMission.
    void Cleanup();
}
