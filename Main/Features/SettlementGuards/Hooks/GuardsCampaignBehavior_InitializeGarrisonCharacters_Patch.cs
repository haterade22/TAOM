using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SandBox.CampaignBehaviors;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TAOM.Core.Logging;

namespace TAOM.Features.SettlementGuards.Hooks;

// #346 — cave trolls must never spawn as visible settlement guards. Vanilla fills the private
// _garrisonTroops list from the garrison roster (filtered only on Occupation == Soldier) and then
// draws every guard type (stand/patrol/spear/castle/unarmed) from it WEIGHTED BY LEVEL — so the
// L51 troll dominates the pick. This postfix scrubs excluded-race entries out of that list right
// after vanilla builds it: one choke point covers all five guard types. If the scrub empties the
// list, vanilla falls back to culture.Guard. Garrison membership (MemberRoster) is never touched —
// trolls still fight in siege defense.
public static class GuardsCampaignBehavior_InitializeGarrisonCharacters_Patch
{
    private static ISettlementGuardService _service;
    // Cached AccessTools.Field deliberately instead of Harmony ___-field injection: an engine
    // rename of _garrisonTroops degrades to the one-shot warning below + vanilla behavior,
    // whereas a stale injected parameter is a hard crash at patch-application time.
    private static FieldInfo _garrisonTroopsField;
    // One-shot logging guard (sibling convention, Phase 9b #157) — surface an API drift ONCE,
    // never per settlement entry.
    private static bool _exceptionLogged;

    public static void Initialize(ISettlementGuardService service)
    {
        _service = service;
        _garrisonTroopsField = AccessTools.Field(typeof(GuardsCampaignBehavior), "_garrisonTroops");
    }

    public static MethodBase TargetMethod()
    {
        return AccessTools.Method(
            typeof(GuardsCampaignBehavior),
            "InitializeGarrisonCharacters",
            new[] { typeof(Settlement) });
    }

    [HarmonyPostfix]
    public static void Postfix(GuardsCampaignBehavior __instance)
    {
        if (_service == null || _garrisonTroopsField == null) return;

        try
        {
            // Mutate in place: the field is readonly and vanilla holds this same instance.
            var garrisonTroops = (List<(CharacterObject, int)>)_garrisonTroopsField.GetValue(__instance);
            if (garrisonTroops == null || garrisonTroops.Count == 0) return;

            int removed = 0;
            for (int i = garrisonTroops.Count - 1; i >= 0; i--)
            {
                var character = garrisonTroops[i].Item1;
                if (character != null && _service.IsRaceExcludedFromGuardDuty(character.Race))
                {
                    garrisonTroops.RemoveAt(i);
                    removed++;
                }
            }

            if (removed > 0)
            {
                IoC.Resolve<IModLogger>()?.LogDebug(
                    $"[SettlementGuards] Scrubbed {removed} excluded-race garrison entr" +
                    $"{(removed == 1 ? "y" : "ies")} from the guard candidate pool (#346).");
            }
        }
        catch (Exception ex)
        {
            // Verified via ilspycmd on the installed v1.4.7 SandBox.dll:
            // `private readonly List<(CharacterObject, int)> _garrisonTroops`. Any exception here
            // is unexpected (likely engine drift in the field's declared type) — log once per
            // process and degrade to vanilla's unscrubbed list.
            if (!_exceptionLogged)
            {
                _exceptionLogged = true;
                try
                {
                    IoC.Resolve<IModLogger>()?.LogError(
                        $"[SettlementGuards] _garrisonTroops scrub failed: " +
                        $"{ex.GetType().Name}: {ex.Message}. Excluded-race guards may spawn. " +
                        $"This log fires once per process — verify the v1.4.7 field signature " +
                        $"via ilspycmd on SandBox.dll if behavior is unexpected.");
                }
                catch { /* logger resolution failure must not surface to vanilla path */ }
            }
        }
    }
}
