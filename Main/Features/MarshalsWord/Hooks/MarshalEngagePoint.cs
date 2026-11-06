using System;
using System.Threading;
using HarmonyLib;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.MarshalsWord.Hooks;

/// <summary>
/// Vanilla's sticky Advance point (build-spec 9.4): <c>MovementOrder.GetPositionAux</c> keeps <c>_engageTargetPositionCache</c> (MovementOrder.cs:75)
/// and replaces it only within <c>speed * speed * 9 * depth</c> of the company (1184-1189), which a stand is outside. <see cref="MarshalThrowerPatch"/>
/// forgets it after each answer it moves, so the first answer it leaves alone seeds the point from where the enemy is then (1180-1183). Bound by name on
/// the main thread before a mission's first table (<c>MarshalThrowerAdapter.Publish</c>); a failed bind latches its exception as the thrower fault, and an unbound <see cref="Forget"/> throws.
/// </summary>
internal static class MarshalEngagePoint
{
    private const string FieldName = "_engageTargetPositionCache";

    private static AccessTools.StructFieldRef<MovementOrder, WorldPosition>? _cache;

    /// <summary>Main thread. Binds the field once per process. A failure latches the real exception, so the postfix moves no answer and the next pass reports it and fails the live runs.</summary>
    internal static void Bind()
    {
        if (Volatile.Read(ref _cache) != null)
            return;
        try
        {
            Volatile.Write(ref _cache, AccessTools.StructFieldRefAccess<MovementOrder, WorldPosition>(FieldName));
        }
        catch (Exception ex)
        {
            MarshalHooks.LatchThrowerFault(ex);
        }
    }

    /// <summary>The postfix's thread, inside vanilla's MovementOrderPositionLock (MovementOrder.cs:534). Sets the point invalid, as every constructor does.</summary>
    internal static void Forget(ref MovementOrder order)
    {
        var cache = Volatile.Read(ref _cache) ?? throw new MissingFieldException(nameof(MovementOrder), FieldName);
        cache(ref order) = WorldPosition.Invalid;
    }
}
