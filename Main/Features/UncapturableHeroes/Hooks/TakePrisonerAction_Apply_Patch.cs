using System;
using HarmonyLib;
using TAOM.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;

namespace TAOM.Features.UncapturableHeroes.Hooks;

/// <summary>
/// Prefix on <see cref="TakePrisonerAction"/>.<c>Apply</c>, closing the capture routes that never
/// consult <c>Hero.CanBecomePrisoner</c> at all.
///
/// <para>The one that matters in practice is
/// <c>PrisonerCaptureCampaignBehavior.HandleSettlementHeroes</c> (<c>:67</c>): a hero standing in a
/// settlement that changes hands, or whose host faction declares war, is captured with no gate.
/// Without this seam a Nazgûl who happened to be resting in a fief would still end up in a
/// dungeon.</para>
///
/// <para>Targets the PUBLIC <c>Apply(PartyBase, Hero)</c> rather than the private
/// <c>ApplyInternal</c>. The only caller <c>Apply</c> misses is
/// <c>ApplyByTakenFromPartyScreen</c>, and that is already unreachable for a hero:
/// <c>PlayerEncounter.DoCaptureHeroes</c> (<c>PlayerEncounter.cs:1611</c>) strips every hero out of
/// <c>RosterToReceiveLootPrisoners</c> before the loot screen sees it. A public target is also a
/// binding <c>HarmonyPatchBindingTests</c> can hold onto across an engine bump.</para>
/// </summary>
[HarmonyPatch(typeof(TakePrisonerAction), nameof(TakePrisonerAction.Apply), new[] { typeof(PartyBase), typeof(Hero) })]
[HarmonyPatchCategory("Patch76_UncapturableHeroes")]
public static class TakePrisonerAction_Apply_Patch
{
    private static IUncapturableHeroService? _service;
    private static IModLogger? _logger;

    /// <summary>Called once from UncapturableHeroesIoC at container build time.</summary>
    public static void Initialize(IUncapturableHeroService service, IModLogger logger)
    {
        _service = service;
        _logger = logger;
    }

    public static void ResetForUnload()
    {
        _service = null;
        _logger = null;
    }

    [HarmonyPrefix]
    public static bool Prefix(PartyBase capturerParty, Hero prisonerCharacter)
    {
        try
        {
            // The player's own captivity is a whole subsystem (PlayerCaptivity.StartCaptivity, the
            // prisoner wait menu, ransom). Five of this method's call sites exist only to run it.
            if (prisonerCharacter == null || prisonerCharacter == Hero.MainHero)
                return true;

            // Already held. MakeHeroFugitiveAction touches PartyBelongedTo, the settlement and the
            // hero state, but NEVER removes anyone from a captor's PrisonRoster, so converting here
            // would leave him Fugitive and still listed as somebody's prisoner. Deferring changes
            // nothing about a state this feature did not create.
            if (prisonerCharacter.IsPrisoner)
                return true;

            // A death-marked hero belongs to vanilla, and this guard MUST mirror the one in
            // Hero_CanBecomePrisoner_Patch or the two seams contradict each other on the same hero
            // in the same battle. A kill applied while the hero is in a map event does not kill: it
            // stages a mark and returns (KillCharacterAction.cs:46-49). MapEvent.cs:1977 then admits
            // every mark except DiedInBattle/DiedInLabor, so a Murdered hero reaches the capture
            // gate, the postfix deliberately defers to vanilla there, vanilla answers true, and
            // MapEvent.cs:1993 calls straight into this prefix. Without this guard we would veto a
            // capture the postfix had just decided not to veto.
            if (prisonerCharacter.DeathMark != KillCharacterAction.KillCharacterActionDetail.None)
                return true;

            var service = _service;
            if (service == null)
                return true;

            var prevented = service.TryPreventCapture(
                prisonerCharacter.StringId,
                prisonerCharacter.CharacterObject?.Race,
                prisonerCharacter.Name?.ToString(),
                IsPlayerCapturing(capturerParty));

            // Skip vanilla ONLY when the escape actually happened. TryPreventCapture returns false
            // if the hero could not be made a fugitive, and letting vanilla capture him then is
            // strictly better than a hero who is neither captured nor free.
            return !prevented;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"TakePrisonerAction_Apply_Patch: {ex.Message}");
            return true;
        }
    }

    /// <summary>
    /// True when the player's own faction is the one doing the capturing, which is the only case
    /// where an escape is something the player witnessed. <c>PartyBase.MapFaction</c> is internally
    /// null-guarded and returns null for an ownerless party, unlike <c>PartyBase.Culture</c>.
    /// </summary>
    private static bool IsPlayerCapturing(PartyBase? capturerParty)
    {
        var captorFaction = capturerParty?.MapFaction;
        var playerFaction = Hero.MainHero?.MapFaction;
        return captorFaction != null && playerFaction != null && captorFaction == playerFaction;
    }
}
