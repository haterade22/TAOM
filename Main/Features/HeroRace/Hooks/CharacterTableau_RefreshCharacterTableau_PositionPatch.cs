using HarmonyLib;
using System;
using TAOM.Core.Logging;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade.View;
using TaleWorlds.MountAndBlade.View.Tableaus;

namespace TAOM.Features.HeroRace.Hooks;

/// <summary>
/// Applies per-race framing offsets to the 3D character tableau (inventory, party, encyclopedia).
///
/// <para>Bannerlord frames every character as if it were human, so a dwarf sits low in the panel
/// and a cave troll is cropped. The offsets live in <c>CharacterAvatarPatch.json</c>.</para>
///
/// <para>This replaces <c>CharacterTableauService</c>, a 221-line reimplementation of the whole of
/// <c>RefreshCharacterTableau</c> and <c>UpdateMount</c> driven by roughly thirty private-field
/// reflection bindings. It was registered in IoC and called by nothing, so the 3D offsets had never
/// actually applied in TAOM; only the 2D portrait ones did. A postfix that nudges two origins gets
/// the same result, keeps vanilla in charge of the refresh, and cuts the drift surface from about
/// thirty bindings to eight.</para>
///
/// <para><b>Why absolute origins, not deltas.</b> The offset is measured from the tableau own spawn
/// frames rather than added to whatever origin the entity currently holds. Vanilla resets both
/// origins on every refresh (the character through <c>AgentVisuals.Refresh</c>, the mount by being
/// recreated in <c>UpdateMount</c>), so a delta would usually be safe, but "usually" is doing real
/// work in that sentence and the failure mode is a character that walks out of shot after enough
/// equipment changes. Reading four extra frames buys structural idempotence.</para>
///
/// <para><b>Rows follow the ENTITY; only the origin follows the place.</b> The tableau can swap the
/// character and its mount between two fixed places. That moves the models, it does not turn the
/// dwarf into a horse, so the character always reads <c>&lt;race&gt;</c> and the mount always reads
/// <c>mount_&lt;race&gt;</c>. The deleted service selected the row by PLACE, which handed the horse
/// the rider offsets on a swap. Unobservable there (dead code), but wrong against shipped data:
/// <c>cave_troll</c> has a plain row and no mount row, so place-based selection would push a horse
/// 4 metres away and leave the troll unframed.</para>
///
/// <para>Rotation is preserved untouched. <c>AgentVisuals.Refresh</c> bakes the race scale into the
/// frame rotation, so replacing the whole frame would silently reset every non-human body scale.
/// Only <c>origin</c> is written.</para>
///
/// <para>Ordering: this runs after vanilla, and therefore after <c>AdjustCharacterForStanceIndex</c>,
/// which was checked on 1.4.8 and touches camera position, actions and skeletons but never an
/// entity origin. It composes with Patch2 (a prefix on the same method that fixes action-set
/// resolution) rather than competing with it.</para>
/// </summary>
[HarmonyPatch(typeof(CharacterTableau), "RefreshCharacterTableau")]
[HarmonyPatchCategory("Patch72_TableauRacePosition")]
public static class CharacterTableau_RefreshCharacterTableau_PositionPatch
{
    private static ITableauPositionService _service;
    private static IModLogger _logger;

    /// <summary>
    /// Captured once at wire-up rather than resolved per call. Matches the BannerColorPersistence
    /// and SettlementNameplateFade patches; a tableau refresh is not per-frame, but an IoC resolve
    /// inside a Harmony postfix is a needless failure surface.
    /// </summary>
    public static void Initialize(ITableauPositionService service, IModLogger logger)
    {
        _service = service;
        _logger = logger;
    }

    [HarmonyPostfix]
    public static void Postfix(
        CharacterTableau __instance,
        AgentVisuals ____agentVisuals,
        AgentVisuals ____mountVisuals,
        int ____race,
        bool ____isCharacterMountPlacesSwapped,
        MatrixFrame ____initialSpawnFrame,
        MatrixFrame ____characterMountPositionFrame,
        MatrixFrame ____mountSpawnPoint,
        MatrixFrame ____mountCharacterPositionFrame)
    {
        var service = _service;
        if (service == null)
            return;

        try
        {
            // Publish for the offset tuner before any early-out, so a race with no configured row
            // is still tunable. That is precisely the race someone needs to tune.
            LiveTableauRef.Set(__instance, ____race);

            var swapped = ____isCharacterMountPlacesSwapped;

            ApplyOffset(
                service,
                ____agentVisuals,
                swapped ? ____characterMountPositionFrame.origin : ____initialSpawnFrame.origin,
                ____race,
                TableauEntity.Character);

            ApplyOffset(
                service,
                ____mountVisuals,
                swapped ? ____mountCharacterPositionFrame.origin : ____mountSpawnPoint.origin,
                ____race,
                TableauEntity.Mount);
        }
        catch (Exception e)
        {
            // Never let a framing tweak take the preview down with it. Logged rather than swallowed:
            // a silent catch here would be indistinguishable from the patch never having applied.
            // Routed through IModLogger rather than TableauDiagnostics because the latter declares
            // itself temporary instrumentation and instructs its own removal, and shares one global
            // 600-line budget across the whole HeroRace diagnostic surface.
            _logger?.LogError($"Patch72 THREW (preview keeps vanilla framing): {e}");
        }
    }

    private static void ApplyOffset(
        ITableauPositionService service,
        AgentVisuals visuals,
        Vec3 baseOrigin,
        int race,
        TableauEntity entity)
    {
        if (visuals == null)
            return;

        GameEntity gameEntity = visuals.GetEntity();

        // Compared against (GameEntity)null, not plain null: GameEntity derives from NativeObject,
        // which overloads == so a managed reference wrapping a destroyed native object compares
        // equal to null. A bare `gameEntity == null` binds that same overload here, but writing the
        // cast makes the intent explicit and matches how vanilla writes the check.
        if (gameEntity == (GameEntity)null)
            return;

        if (!service.TryGetOrigin(baseOrigin, race, entity, out var origin))
            return;

        // Read-modify-write of the ROTATION only. The rotation carries the race scale that
        // AgentVisuals.Refresh applied plus any drag-rotation the player has done; the origin is
        // replaced outright with the absolute value computed above.
        MatrixFrame frame = gameEntity.GetFrame();
        frame.origin = origin;
        gameEntity.SetFrame(ref frame, true);
    }
}
