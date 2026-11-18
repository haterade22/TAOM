using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TAOM.Dependencies.Foundation;
using TAOM.Features.EngineBugGuards.Domain;

namespace TAOM.Features.EngineBugGuards.Hooks;

/// <summary>
/// Patch167 (#1444; ported from Silas He's v1.5.6 fix package): a dead hero's encyclopedia page wears vanilla
/// imperial gear. v1.5.7 <c>Hero.OnDeath</c> (:2018-2033) sets <c>_battleEquipment</c> and <c>_civilianEquipment</c>
/// to null, and the getters then return <c>Campaign.DeadBattleEquipment</c> / <c>DeadCivilianEquipment</c> (:221, :223),
/// the neutral roster, whose coif and mittens deform on the dwarf skeleton. This keeps the hero's own two kits through
/// <c>OnDeath</c>. The rest of what <c>OnDeath</c> clears (skills, perks, traits) stays cleared. Cost: each dead hero
/// now saves its two Equipment objects. Heroes who died before this fix have no kit left in the save and keep the
/// fallback. Not <c>ClientOnly</c>: the kits are saved hero state.
/// </summary>
[HarmonyPatch(typeof(Hero), "OnDeath")]
[HarmonyPatchCategory(Category)]
public static class Patch167_DeadHeroOutfit
{
    internal const string Category = "Patch167_DeadHeroOutfit";

    // Saveable auto-properties (Hero.cs :212-216): private, so a delegate bound once, not reflection per call.
    internal static readonly Func<Hero, Equipment?>? GetBattle = Getter("_battleEquipment");
    internal static readonly Action<Hero, Equipment>? SetBattle = Setter("_battleEquipment");
    internal static readonly Func<Hero, Equipment?>? GetCivilian = Getter("_civilianEquipment");
    internal static readonly Action<Hero, Equipment>? SetCivilian = Setter("_civilianEquipment");

    private static Func<Hero, Equipment?>? Getter(string property)
    {
        var method = AccessTools.PropertyGetter(typeof(Hero), property);
        return method == null ? null : AccessTools.MethodDelegate<Func<Hero, Equipment?>>(method);
    }

    private static Action<Hero, Equipment>? Setter(string property)
    {
        var method = AccessTools.PropertySetter(typeof(Hero), property);
        return method == null ? null : AccessTools.MethodDelegate<Action<Hero, Equipment>>(method);
    }

    /// <summary>
    /// A patch-time, process-constant decision: not with a co-op module loaded. Coop's own <c>OnDeath</c> prefix
    /// destroys the dying hero's kits and syncs both kit properties, so a restored kit would stay on the host only
    /// (#1444 review). Presence, not session, because the patch is applied once per process.
    /// </summary>
    [HarmonyPrepare]
    public static bool Prepare() => !CoopPresence.IsActive;

    [HarmonyPrefix]
    public static void Prefix(Hero __instance, out Equipment?[]? __state)
    {
        __state = GetBattle == null || GetCivilian == null ? null : new[] { GetBattle(__instance), GetCivilian(__instance) };
    }

    [HarmonyPostfix]
    public static void Postfix(Hero __instance, Equipment?[]? __state)
    {
        if (__state == null || SetBattle == null || SetCivilian == null)
            return;
        Restore(__instance, __state[0], GetBattle!, SetBattle);
        Restore(__instance, __state[1], GetCivilian!, SetCivilian);
    }

    private static void Restore(Hero hero, Equipment? beforeDeath, Func<Hero, Equipment?> get, Action<Hero, Equipment> set)
    {
        var current = get(hero);
        var kit = EngineDisplayRules.KitAfterDeath(beforeDeath, current);
        if (kit != null && !ReferenceEquals(kit, current))
            set(hero, kit);
    }
}
