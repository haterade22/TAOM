using System;
using TaleWorlds.Core;

namespace TAOM.Features.CreatureBandits;

/// <summary>
/// Route A's one-shot scope around the spawner's own <c>SpawnMonster</c> call (#692). The engine allocates an agent's
/// native weapon state only when CanWieldWeapon is in the flags handed to its native creation call, and a soldier's
/// target scorer reads that state without a null check, so a creature soldiers can target must be created with it.
/// While armed, the flags Mission.CreateAgent passes to the native call gain CanWieldWeapon, once; the postfix then
/// takes the Created state and strips the flag before the build (<see cref="CreatureBanditRules.BuildFlags"/>).
///
/// Thread-static: the spawner arms it on the main thread inside the Mission.SpawnTroop prefix and disarms it in a
/// finally, so an agent created on any other thread, or outside the scope, passes through untouched. Engine-free and
/// public, because the transpiled CreateAgent calls <see cref="OnCreationFlags"/> directly.
/// </summary>
public static class CreatureWeaponStateScope
{
    private enum Phase { Idle, Armed, Created, Done }

    [ThreadStatic] private static Phase _phase;
    [ThreadStatic] private static bool _weaponFlagAtCreate;

    /// <summary>Whether the last armed creation carried CanWieldWeapon into the native call (for the spawn line).</summary>
    public static bool WeaponFlagAtCreate => _weaponFlagAtCreate;

    /// <summary>
    /// Whether the armed spawn got as far as the native creation call. Done, not Idle, after <see cref="TakeCreated"/>
    /// keeps that answer until <see cref="Disarm"/>: a SpawnMonster that throws after it leaves a native agent behind.
    /// </summary>
    public static bool ReachedCreation => _phase is Phase.Created or Phase.Done;

    public static void Arm()
    {
        _phase = Phase.Armed;
        _weaponFlagAtCreate = false;
    }

    public static void Disarm() => _phase = Phase.Idle;

    /// <summary>Called from Mission.CreateAgent's IL with the Monster's flags, on their way into the native creation call.</summary>
    public static AgentFlag OnCreationFlags(AgentFlag flags)
    {
        if (_phase != Phase.Armed) return flags;
        _phase = Phase.Created;
        var creation = CreatureBanditRules.CreationFlags(flags);
        _weaponFlagAtCreate = (creation & AgentFlag.CanWieldWeapon) != 0;
        return creation;
    }

    /// <summary>True once, right after an armed creation: the caller must strip the flag from the new agent now.</summary>
    public static bool TakeCreated()
    {
        if (_phase != Phase.Created) return false;
        _phase = Phase.Done;
        return true;
    }
}
