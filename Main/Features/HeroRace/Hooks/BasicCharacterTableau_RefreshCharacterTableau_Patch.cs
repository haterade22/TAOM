using HarmonyLib;
using TAOM.Features.HeroRace;
using TaleWorlds.MountAndBlade.View.Tableaus;

namespace TAOM.Features.HeroRace.Hooks;

/// <summary>
/// Prefix on the private <c>BasicCharacterTableau.RefreshCharacterTableau</c> — the Save/Load hero
/// preview tableau (instantiated only by <c>SaveLoadHeroTableauTextureProvider</c>).
///
/// That method builds the body via the agentless native
/// <c>MBAgentVisuals.FillEntityWithBodyMeshesWithoutAgentVisuals(entity, SkinGenerationParams{_race}, …)</c>
/// on the hardcoded human skeleton. For a TAOM custom-race save the native static-morph build
/// access-violates — the custom-race head meshes lack the per-face-component morph data vanilla heads
/// carry (same class as the Erebor-arena crash, issue #295). A native AV is a corrupted-state
/// exception, so it cannot be try/caught; the fix must be preventive.
///
/// The Prefix coerces the private <c>_race</c> field to a render-safe race (the human base) for races
/// whose heads lack the data, so the cold main-menu Load Game preview cannot CTD. The decision lives
/// in <see cref="IBasicTableauRaceGuard"/> (ADR-002/007). Scope is narrow by construction: only the
/// Save/Load preview uses BasicCharacterTableau — the in-game inventory / character-creation tableaus
/// use the AgentVisuals path (CharacterTableau) and already render custom races correctly, so they are
/// untouched. Tradeoff: an affected custom-race save shows a human-headed preview (with correct
/// equipment) until the head morph data is authored asset-side (issue #295) — acceptable vs. a crash.
///
/// CRITICAL timing (Codex C1, issue #299): this is its OWN category applied from
/// <c>SubModule.OnBeforeInitialModuleScreenSetAsRoot</c>, NOT the sibling CharacterTableau patches'
/// <c>Patch2_RefreshTableau</c>. Those are applied in <c>OnGameInitializationFinished</c> (campaign
/// init), which is too late: the Save/Load hero preview renders on the COLD main menu, before any game
/// starts. Applying here — after IoC.Configure has set the guard, before the initial module screen is
/// pushed — attaches the prefix before the save list can render. Process-static one-shot guard in
/// SubModule. Guard injected once via <see cref="Initialize"/> from HeroRaceIoC (mirrors
/// FaceGen_GetBaseMonsterFromRace_Patch) — no per-call IoC resolve.
/// </summary>
[HarmonyPatch(typeof(BasicCharacterTableau), "RefreshCharacterTableau")]
[HarmonyPatchCategory("Patch55_BasicTableauRaceGuard")]
public static class BasicCharacterTableau_RefreshCharacterTableau_Patch
{
    private static IBasicTableauRaceGuard _guard;

    public static void Initialize(IBasicTableauRaceGuard guard) => _guard = guard;

    // ____race injects the private instance field _race (Harmony: three underscores + the literal
    // field name "_race" = four underscores). ref so the coerced value is written back before the
    // native body-mesh build reads it.
    [HarmonyPrefix]
    public static void Prefix(ref int ____race)
    {
        var guard = _guard;
        if (guard == null)
            return;

        ____race = guard.ResolveSafeRace(____race);
    }
}
