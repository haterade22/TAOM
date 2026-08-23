using HarmonyLib;
using TAOM.Adapters;
using TAOM.Features.Enlistment;
using TaleWorlds.CampaignSystem.Encounters;

namespace TAOM.Features.Refuge.Hooks;

/// <summary>
/// Prefix on the static <see cref="PlayerEncounter"/>.<c>DoMeeting</c>: meeting your own ready
/// refuge opens the refuge menu instead of vanilla's meeting flow (which would try to strike a
/// conversation with the warden as if he were a stranger's party).
///
/// <para>ENLISTMENT OWNS ENCOUNTER POLICY (INV-D1): while the player is enlisted, this patch
/// stands down entirely and vanilla (plus Enlistment's own interception) runs, so an enlisted
/// player marched over a refuge cannot be teleported into its menu mid-service.</para>
///
/// <para>Reads <c>PlayerEncounter.EncounteredMobileParty</c> directly rather than through
/// <see cref="IEncounterAdapter"/>: the adapter exposes the read (EncounteredPartyId), but the
/// feature's IoC hands this patch exactly (service, enlistment, menus), and adding a fourth
/// dependency for one static property read on a thin patch buys nothing (simplicity criterion).
/// The menu OPEN does go through <see cref="IGameMenuAdapter.EnsureMenuOpen"/> because raw
/// GameMenu switching silently no-ops without a menu context - the adapter verifies.</para>
/// </summary>
[HarmonyPatch(typeof(PlayerEncounter), "DoMeeting")]
[HarmonyPatchCategory("Patch75_Refuge")]
public static class RefugeEncounterPatch
{
    private static IRefugeService? _refuges;
    private static IEnlistmentStateQuery? _enlistment;
    private static IGameMenuAdapter? _menus;

    /// <summary>Called once from RefugeIoC at container build time.</summary>
    public static void Initialize(
        IRefugeService refuges, IEnlistmentStateQuery enlistment, IGameMenuAdapter menus)
    {
        _refuges = refuges;
        _enlistment = enlistment;
        _menus = menus;
    }

    [HarmonyPrefix]
    public static bool Prefix()
    {
        try
        {
            var refuges = _refuges;
            var menus = _menus;
            if (refuges == null || menus == null)
                return true;

            if (_enlistment?.IsEnlisted == true)
                return true;

            var encountered = PlayerEncounter.EncounteredMobileParty;
            var partyId = encountered?.StringId;
            if (string.IsNullOrEmpty(partyId) || !IsEnterableRefuge(refuges, partyId))
                return true;

            // A refuge inside a live map event is a battle participant: vanilla's encounter flow
            // (join/observe the fight) must run, not the manage menu, whose screens would mutate
            // the event's rosters behind the engine's back.
            if (encountered.MapEvent != null)
                return true;

            // Skip vanilla only when the refuge menu verifiably opened; on a failed open the
            // vanilla meeting is a strange conversation, but a dead encounter would be worse.
            return !menus.EnsureMenuOpen(RefugeCampaignBehavior.MenuId);
        }
        catch
        {
            return true;
        }
    }

    /// <summary>Ready refuges open the menu; orphan-adopted rows do too, because the menu's
    /// dismantle option is their only exit (everything else greys out for them).</summary>
    private static bool IsEnterableRefuge(IRefugeService refuges, string partyId)
    {
        foreach (var refuge in refuges.AllRefuges)
        {
            if ((refuge.IsReady || refuge.IsOrphanAdopted)
                && string.Equals(refuge.PartyId, partyId, System.StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }
}
