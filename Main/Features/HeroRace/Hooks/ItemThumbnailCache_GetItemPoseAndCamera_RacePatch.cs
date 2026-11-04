using HarmonyLib;
using System;
using TAOM.Core.Logging;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade.View;
using TaleWorlds.MountAndBlade.View.Tableaus.Thumbnails;

namespace TAOM.Features.HeroRace.Hooks;

/// <summary>
/// Reframes the inventory icons of race-shaped armour (Patch132).
///
/// <para>Vanilla builds every armour icon in two steps. <c>ItemThumbnailCache.GetItemPoseAndCamera</c>
/// picks the pose: the item goes to a fixed scene spot (<c>helmet_frame</c>, <c>armor_frame</c>,
/// <c>boot_frame</c>, ...) and is shot with a fixed camera (<c>helmet_cam</c>, ...). <c>AddItem</c> then
/// builds the entity at that pose. Both spots assume a human-proportioned mesh. Dwarven armour is
/// modelled in dwarf body space, so a dwarf helmet sits at dwarf head height and the camera aims
/// above it. Same root cause as the portrait framing (Patch4/Patch72): the engine frames everything
/// as if it were human.</para>
///
/// <para>Helmets are first centred automatically: the centre of the helmet's mesh bounds is moved
/// onto the icon camera's line of sight. Then the postfix moves <c>itemFrame</c> by the offsets in
/// <c>ItemIconPatch.json</c>, row
/// <c>&lt;race&gt;_&lt;slot&gt;</c>, before <c>AddItem</c> reads it. Patching the pose rather than the
/// finished entity matters for gloves: vanilla renders them on a human skeleton and ticks that
/// skeleton with <c>itemFrame</c>, so a frame moved afterwards would leave the bones behind. The
/// axes are the icon camera's own: Horizontal is screen right, Vertical is screen up, Zoom moves the
/// item towards the camera. Only the origin is written; the rotation vanilla gave the item stays.</para>
///
/// <para>Runs once per icon: the thumbnail cache keeps the result until it is cleared (vanilla
/// clears it on every <c>MapScreen.OnInitialize</c>). The tuning console clears it for the icon
/// surface so an edit shows the next time an icon is requested.</para>
/// </summary>
[HarmonyPatch(typeof(ItemThumbnailCache), "GetItemPoseAndCamera")]
[HarmonyPatchCategory("Patch132_ItemIconRacePosition")]
[ClientOnly]
public static class ItemThumbnailCache_GetItemPoseAndCamera_RacePatch
{
    private static IRacePositionStore _store;
    private static IModLogger _logger;

    /// <summary>Captured once at wire-up, same as Patch72: no IoC resolve inside a Harmony postfix.</summary>
    public static void Initialize(IRacePositionStore store, IModLogger logger)
    {
        _store = store;
        _logger = logger;
    }

    [HarmonyPostfix]
    public static void Postfix(ItemObject item, ref Camera camera, ref MatrixFrame itemFrame)
    {
        var store = _store;
        if (store == null || item == null)
            return;

        try
        {
            var slot = SlotFor(item.ItemType);
            if (slot == null)
                return;

            var race = ItemIconRaceMatcher.MatchRace(item.StringId, item.MultiMeshName, item.Culture?.StringId);
            var row = store.ResolveIcon(ItemIconRaceMatcher.RowKey(race, slot));
            if (row == null)
                return;

            // Helmets are centred from their own mesh first, so no hand-tuned row has to guess where
            // a given dwarf helmet sits; the row is then only a fine adjustment on top.
            if (slot == "helmet")
                itemFrame.origin += CentreOnCameraAxis(item, camera, itemFrame);

            itemFrame.origin += Offset(camera, row.Horizontal, row.Vertical, row.Zoom);
        }
        catch (Exception e)
        {
            // Never let a framing tweak take the icon down with it; the icon keeps vanilla framing.
            _logger?.LogError($"Patch132 THREW for item '{item.StringId}' (icon keeps vanilla framing): {e}");
        }
    }

    /// <summary>The vanilla camera slot an item type renders in, or null for non-armour.</summary>
    internal static string SlotFor(ItemObject.ItemTypeEnum type)
        => type switch
        {
            ItemObject.ItemTypeEnum.HeadArmor => "helmet",
            ItemObject.ItemTypeEnum.BodyArmor => "armor",
            ItemObject.ItemTypeEnum.LegArmor => "boot",
            ItemObject.ItemTypeEnum.HandArmor => "glove",
            ItemObject.ItemTypeEnum.Cape => "cape",
            _ => null,
        };

    /// <summary>
    /// The shift that puts the centre of the item's mesh on the icon camera's line of sight, keeping
    /// its distance from the camera. The bounds are read the way vanilla's 3D item inspector reads
    /// them (<c>ItemObjectViewExtensions.GetScaledFrame</c>): the min/max of every submesh of the
    /// inventory mesh, here carried through <paramref name="itemFrame"/> into the scene.
    /// </summary>
    private static Vec3 CentreOnCameraAxis(ItemObject item, Camera camera, MatrixFrame itemFrame)
    {
        if (camera == (Camera)null)
            return Vec3.Zero;

        var mesh = new ItemRosterElement(item, 0, null).GetItemMeshForInventory();
        if (mesh == (MetaMesh)null || mesh.MeshCount == 0)
            return Vec3.Zero;

        var min = new Vec3(float.MaxValue, float.MaxValue, float.MaxValue);
        var max = new Vec3(float.MinValue, float.MinValue, float.MinValue);
        for (var i = 0; i < mesh.MeshCount; i++)
        {
            var part = mesh.GetMeshAtIndex(i);
            var lo = part.GetBoundingBoxMin();
            var hi = part.GetBoundingBoxMax();
            for (var corner = 0; corner < 8; corner++)
            {
                var local = new Vec3(
                    (corner & 1) == 0 ? lo.x : hi.x,
                    (corner & 2) == 0 ? lo.y : hi.y,
                    (corner & 4) == 0 ? lo.z : hi.z);
                var world = itemFrame.TransformToParent(local);
                min = Vec3.Vec3Min(min, world);
                max = Vec3.Vec3Max(max, world);
            }
        }

        var centre = (min + max) * 0.5f;
        var lookAlong = camera.Direction;

        // Written as !(x > eps) so an empty mesh, a NaN bound or a degenerate camera changes nothing.
        if (!(lookAlong.LengthSquared > 1e-6f) || !((max - min).LengthSquared > 1e-6f))
            return Vec3.Zero;

        lookAlong = lookAlong.NormalizedCopy();
        var fromCamera = centre - camera.Position;
        var offAxis = fromCamera - lookAlong * Vec3.DotProduct(fromCamera, lookAlong);
        return offAxis.IsValid ? -offAxis : Vec3.Zero;
    }

    private static Vec3 Offset(Camera camera, float horizontal, float vertical, float zoom)
    {
        if (camera == (Camera)null)
            return Vec3.Up * vertical;

        // The icon cameras do not all look level: the helmet camera looks down on the item, so world
        // up there is mostly towards the lens and moving along it reads as zoom. Use the camera's own
        // axes instead. A Bannerlord camera looks along -rotation.u (Camera.Direction), with screen
        // right rotation.s and screen up rotation.f.
        var rotation = camera.Frame.rotation;
        var right = rotation.s;
        var up = rotation.f;
        var toCamera = rotation.u;

        // Written as !(x > eps) so a degenerate or NaN camera frame falls back to world up.
        if (!(right.LengthSquared > 1e-6f) || !(up.LengthSquared > 1e-6f) || !(toCamera.LengthSquared > 1e-6f))
            return Vec3.Up * vertical;

        return right.NormalizedCopy() * horizontal
             + up.NormalizedCopy() * vertical
             + toCamera.NormalizedCopy() * zoom;
    }
}
