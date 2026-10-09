// Adapted from yotthani's VanillaTuning (MIT, (c) 2026 yotthani), nameplate-cull.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using SandBox.ViewModelCollection.Nameplate;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TAOM.Features.NameplateCull;
using TAOM.Features.NameplateCull.Hooks;
using TAOM.Features.NameplateCull.Models;

namespace TAOM.Adapters;

/// <summary>
/// The engine side of the nameplate cull (docs/features/nameplate-cull.md): vanilla <c>SettlementNameplatesVM.Update()</c>
/// with the plates that are hidden and stay hidden left out. The three private members are reached through
/// <see cref="AccessTools.FieldRef{T,F}"/> delegates built once in <see cref="Initialize"/>, never reflection per frame; the
/// list of plates to update is reused, and nothing in the per-frame path allocates (no LINQ, no closure, one delegate built
/// at install). Main thread; the parallel loop reads only the reused list and the camera position, both written before it
/// starts, as vanilla's loop reads its own list and its camera position.
///
/// <para>Order and effects follow vanilla's <c>Update</c> (v1.5.4): read the camera, <c>UpdateNameplateMT</c> over the plates
/// in parallel with the default grain size, then <c>RefreshBindValues</c> over the same plates in list order. Vanilla also
/// stores the camera in <c>_cachedCameraPosition</c>, but only <c>UpdateNameplateAuxMT</c> reads it and only vanilla's own
/// <c>Update</c> reaches that method (a binding test reads the IL), so the cull keeps the camera in its own field instead.
/// The skip decision is <see cref="NameplateCullRule.MaySkip"/>.</para>
/// </summary>
public sealed class NameplateCullAdapter : INameplateCullAdapter
{
    // RefreshPosition parks a hidden plate at (-1000, -1000) in v1.5.4; a binding test reads both operands from the IL.
    internal const float ParkedCoordinate = -1000f;

    private AccessTools.FieldRef<SettlementNameplatesVM, Camera>? _mapCamera;
    private AccessTools.FieldRef<SettlementNameplateVM, bool>? _bindIsVisibleOnMap;
    private AccessTools.FieldRef<SettlementNameplateVM, Vec3>? _worldPos;
    private List<SettlementNameplateVM>? _active;
    private TWParallel.ParallelForAuxPredicate? _updateRange;
    private Vec3 _cameraPosition;

    public string? Initialize()
    {
        try
        {
            _mapCamera = AccessTools.FieldRefAccess<SettlementNameplatesVM, Camera>("_mapCamera");
            _bindIsVisibleOnMap = AccessTools.FieldRefAccess<SettlementNameplateVM, bool>("_bindIsVisibleOnMap");
            _worldPos = AccessTools.FieldRefAccess<SettlementNameplateVM, Vec3>("_worldPos");
            _active = new List<SettlementNameplateVM>(1024);
            _updateRange = UpdateRange;

            // Compile the three methods now, so a member this game build lacks fails here and the cull reports itself
            // unavailable. Unprepared, UpdateCulled and MaySkip would fail at the first map frame inside TryUpdate's catch;
            // UpdateRange runs on engine worker threads, where no TAOM catch is on the stack.
            foreach (var name in new[] { nameof(UpdateCulled), nameof(MaySkip), nameof(UpdateRange) })
                RuntimeHelpers.PrepareMethod(typeof(NameplateCullAdapter)
                    .GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.MethodHandle);

            // The module runner ignores whether the patch category attached, so the install line would say ON for a cull
            // that never runs.
            if (IsPatch104Attached()) return null;
            _updateRange = null;
            return "Patch104 is not attached to SettlementNameplatesVM.Update";
        }
        catch (Exception ex)
        {
            _updateRange = null;
            return ex.GetType().Name + ": " + ex.Message;
        }
    }

    private static bool IsPatch104Attached()
    {
        var info = Harmony.GetPatchInfo(AccessTools.Method(typeof(SettlementNameplatesVM), nameof(SettlementNameplatesVM.Update), Type.EmptyTypes));
        return info != null && info.Prefixes.Any(p => p.PatchMethod?.DeclaringType == typeof(SettlementNameplatesVM_Update_NameplateCull_Patch));
    }

    public CullCounts UpdateCulled(object nameplates)
    {
        var vm = (SettlementNameplatesVM)nameplates;
        var plates = vm.AllNameplates;
        var active = _active!;
        var camera = _mapCamera!(vm).Position;
        var tracker = Campaign.Current.VisualTrackerManager;

        active.Clear();
        var count = plates.Count;
        try
        {
            for (var i = 0; i < count; i++)
            {
                var plate = plates[i];
                if (!MaySkip(plate, in camera, tracker)) active.Add(plate);
            }

            var updated = active.Count;
            _cameraPosition = camera;
            TWParallel.For(0, updated, _updateRange!);
            for (var i = 0; i < updated; i++)
                active[i].RefreshBindValues();
            return new CullCounts(updated, count - updated);
        }
        finally
        {
            active.Clear();
        }
    }

    private bool MaySkip(SettlementNameplateVM plate, in Vec3 camera, VisualTrackerManager tracker)
    {
        var settlement = plate.Settlement;
        var position = plate.Position;
        var facts = new NameplateFacts
        {
            IsVisibleOnMap = plate.IsVisibleOnMap,
            BindIsVisibleOnMap = _bindIsVisibleOnMap!(plate),
            ParkedOffScreen = position.x == ParkedCoordinate && position.y == ParkedCoordinate,
            IsTracked = plate.IsTracked,
            IsInRange = plate.IsInRange,
            IsTargetedByTutorial = plate.IsTargetedByTutorial,
            CameraZ = camera.z,
            DistanceToCamera = _worldPos!(plate).Distance(camera),
            IsTown = settlement.IsTown,
            IsFortification = settlement.IsFortification,
            SettlementInRange = settlement.IsHideout ? settlement.IsVisible : settlement.IsInspected,
            PartyVisualDirty = settlement.Party != null && settlement.Party.IsVisualDirty,
            VisuallyTracked = tracker.CheckTracked(settlement),
        };
        return NameplateCullRule.MaySkip(in facts);
    }

    private void UpdateRange(int startInclusive, int endExclusive)
    {
        var active = _active!;
        var camera = _cameraPosition;
        for (var i = startInclusive; i < endExclusive; i++)
            active[i].UpdateNameplateMT(camera);
    }
}
