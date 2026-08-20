using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TAOM.Core.Logging;
using TAOM.Features.FieldCommission.Domain;

namespace TAOM.Features.FieldCommission.Hooks;

/// <summary>
/// Guards vanilla's unguarded <c>Hero.ResetEquipments</c> (#486), which clones three equipments
/// straight off <c>Hero.Template</c> with no null checks. Its only caller anywhere in the installed
/// assemblies is <c>RemoveCompanionAction.ApplyInternal</c>, when firing a wanderer. A promoted
/// companion is a wanderer whose <c>Template</c> is the line TROOP, so the civilian clone
/// dereferences null for the 743 of 895 TAOM troop blocks declaring no civilian roster, and the
/// stealth clone does the same for the eight <c>is_bandit</c> cultures (vanilla's own shape). The
/// throw lands after the hero is de-clanned and de-partied, before <c>OnCompanionRemoved</c> fires.
///
/// Returns <c>true</c> whenever the template supplies all three slots, so a vanilla-shaped hero still
/// runs the engine's own reset; otherwise fills each slot in place from the template, falling back to
/// the troop's battle kit. Every member touched is public: no reflection, no private field writes.
/// Full mechanism and the traps: <c>docs/reference/harmony-patch-registry.md</c>.
/// </summary>
[HarmonyPatch(typeof(Hero), nameof(Hero.ResetEquipments))]
[HarmonyPatchCategory("Patch71_HeroResetEquipmentsGuard")]
public static class Patch71_HeroResetEquipmentsGuard
{
    private static IModLogger _logger;

    // Named once per hero: a guard that leaves no trace is how a fix becomes an invisible change.
    // Keyed by campaign AND hero, because StringIds restart per campaign and would otherwise collide.
    private static readonly HashSet<string> _reported = new HashSet<string>();

    /// <summary>Called from <c>SubModule.OnSubModuleUnloaded</c>: without it the cached logger
    /// points into a disposed IoC container after a reload-in-process (Codex review #46).</summary>
    public static void ResetForUnload()
    {
        _logger = null;
        _reported.Clear();
    }

    [HarmonyPrefix]
    public static bool Prefix(Hero __instance)
    {
        if (__instance == null)
            return true;

        try
        {
            var template = __instance.Template;
            var battle = template?.FirstBattleEquipment;
            var civilian = template?.FirstCivilianEquipment;
            var stealth = ResolveStealth(template);

            if (EquipmentResetPlan.CanDeferToEngine(battle != null, civilian != null, stealth != null))
                return true;

            Fill(__instance.BattleEquipment, battle, battle, Campaign.Current?.DeadBattleEquipment);
            Fill(__instance.CivilianEquipment, civilian, battle, Campaign.Current?.DeadCivilianEquipment);
            Fill(__instance.StealthEquipment, stealth, battle, Campaign.Current?.DefaultStealthEquipment);

            Report(__instance, battle == null, civilian == null, stealth == null);
            return false;
        }
        catch (Exception ex)
        {
            // Skip the original rather than defer. Vanilla is not a safe default HERE: it would
            // re-raise the NRE this patch exists to stop, on a hero ApplyInternal has already
            // de-clanned and de-partied (and made a fugitive, on the ordinary non-prisoner path),
            // stranding the campaign in the torn state #486 is about. Skipping leaves the hero in
            // the kit they wear. That makes the prefix total, so there is no Finalizer.
            Report(__instance, false, false, false, ex);
            return false;
        }
    }

    /// <summary>
    /// <c>FirstStealthEquipment</c> on a non-hero is unguarded
    /// (<c>Culture.DefaultStealthEquipmentRoster.AllEquipments.First()</c>), so culture and roster are
    /// tested first. An EMPTY roster needs nothing: <c>AllEquipments</c> substitutes a one-element list
    /// holding <c>EmptyEquipment</c>, so <c>First()</c> cannot throw. Pinned by a binding test.
    /// </summary>
    private static Equipment ResolveStealth(CharacterObject template)
    {
        if (template == null)
            return null;

        if (!template.IsHero && template.Culture?.DefaultStealthEquipmentRoster == null)
            return null;

        return template.FirstStealthEquipment;
    }

    /// <summary>
    /// Fills one slot in place: <c>FillFrom</c> copies the 12 slots into the existing object and only
    /// READS the source, so no clone is needed. <paramref name="target"/> is skipped when it IS
    /// <paramref name="sharedDefault"/>, because the <c>Hero</c> getters fall back to a campaign-wide
    /// singleton when the backing field is null and filling that re-equips the whole campaign.
    /// internal for TAOM.Tests: the guard worth pinning directly.
    /// </summary>
    internal static void Fill(Equipment target, Equipment slotSource, Equipment battleFallback, Equipment sharedDefault)
    {
        if (target == null || ReferenceEquals(target, sharedDefault))
            return;

        var choice = EquipmentResetPlan.ForSlot(slotSource != null, battleFallback != null);
        if (choice == EquipmentResetSource.None)
            return;

        var source = choice == EquipmentResetSource.Template ? slotSource : battleFallback;
        target.FillFrom(source, EquipmentResetPlan.KeepsSourceEquipmentType(choice));
    }

    /// <summary>Named once per hero; the dedup gate runs before any string is built.</summary>
    private static void Report(Hero hero, bool noBattle, bool noCivilian, bool noStealth, Exception ex = null)
    {
        try
        {
            var heroId = hero?.StringId ?? "(unresolved hero)";
            if (!_reported.Add($"{Campaign.Current?.UniqueGameId}/{heroId}")) return;

            var gaps = new List<string>(3);
            if (noBattle) gaps.Add("battle");
            if (noCivilian) gaps.Add("civilian");
            if (noStealth) gaps.Add("stealth");

            // Say what actually happened. With no battle kit there is nothing to stand in, so the
            // unfilled slots were left as they were rather than reset from it.
            var detail = ex != null
                ? $"guard faulted with {ex.GetType().Name} ({ex.Message}); the reset was skipped and the hero kept their kit"
                : $"template supplies no {string.Join("/", gaps)} equipment; " + (noBattle
                    ? "it has no battle kit to stand in either, so those slots were left as they were"
                    : "those slots were filled from its battle kit instead");

            var message = $"Patch71: '{heroId}' (template '{hero?.Template?.StringId ?? "(none)"}') {detail}.";
            _logger ??= TAOM.IoC.Resolve<IModLogger>();

            // A fault is a should-never-happen backstop, so it goes out at ERROR with the whole
            // dump: without a trace there is nothing to root-cause from if it ever fires.
            if (ex != null) _logger?.LogError($"{message} {ex}");
            else _logger?.LogWarning(message);
        }
        catch
        {
            // Logging must never be the thing that breaks firing a companion.
        }
    }
}
