using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.CreatureBandits.Hooks;

/// <summary>
/// Route A's creation hook (#692), on Mission's private <c>CreateAgent(Monster, ...)</c> (v1.5.3 <c>Mission.cs:4077</c>),
/// which SpawnMonster reaches before it builds the agent (<c>Mission.cs:4450-4463</c>). The transpiler passes the
/// <c>monster.Flags</c> value handed to the native creation call (<c>:4082</c>) through
/// <see cref="CreatureWeaponStateScope.OnCreationFlags"/>, which adds CanWieldWeapon only while the spawner has armed the
/// scope, so the engine allocates the creature's weapon state. The postfix strips the flag from the new agent before
/// the build: left live it sends the creature into the unarmed melee lookup its action set cannot answer. Both hooks
/// sit on the one method, so they are applied together or not at all. If CreateAgent throws after the native call
/// the flag cannot be stripped (no managed agent); the finalizer logs it and closes the scope.
/// Not hot (once per agent created), so not on PatchShield's hot-method list.
/// </summary>
[HarmonyPatchCategory(CreatureBanditsConfig.PatchCategory)]
public static class Patch93_CreatureBanditWeaponState
{
    public static MethodBase TargetMethod() => AccessTools.Method(typeof(Mission), "CreateAgent",
        new[] { typeof(Monster), typeof(bool), typeof(int), typeof(Agent.CreationType), typeof(float), typeof(int),
                typeof(int), typeof(BasicCharacterObject) });

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var flags = AccessTools.PropertyGetter(typeof(Monster), nameof(Monster.Flags));
        var hook = AccessTools.Method(typeof(CreatureWeaponStateScope), nameof(CreatureWeaponStateScope.OnCreationFlags));
        int injected = 0;
        foreach (var instruction in instructions)
        {
            yield return instruction;
            if (!instruction.Calls(flags)) continue;
            yield return new CodeInstruction(OpCodes.Call, hook);
            injected++;
        }
        Diagnostics.CreatureBanditDiag.CreationHookSites = injected;
    }

    public static void Postfix(Agent __result)
    {
        if (!CreatureWeaponStateScope.TakeCreated() || __result == null) return;
        __result.SetAgentFlags(CreatureBanditRules.BuildFlags(__result.GetAgentFlags()));
    }

    public static Exception? Finalizer(Exception? __exception)
    {
        if (__exception != null && CreatureWeaponStateScope.TakeCreated())
            CreatureBanditLog.Logger?.LogError("[CreatureBandits] CreateAgent threw after a creature was " +
                $"created with CanWieldWeapon; the flag could not be stripped: {__exception.GetType().Name}: {__exception.Message}");
        return __exception;
    }
}
