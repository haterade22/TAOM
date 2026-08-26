using System;
using HarmonyLib;
using TAOM.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace TAOM.Features.UncapturableHeroes.Hooks;

/// <summary>
/// Postfix on <see cref="Hero"/>.<c>CanBecomePrisoner</c>, the gate
/// <c>MapEvent.CaptureDefeatedPartyMembers</c> consults at <c>MapEvent.cs:1983</c> before taking a
/// defeated lord prisoner.
///
/// <para>THE ESCAPE IS FREE. Denying capture here is the whole feature: when the gate fails, the
/// hero is still in the defeated member roster, so vanilla's own fall-through at
/// <c>MapEvent.cs:2004-2008</c> fires <c>MakeHeroFugitiveAction.Apply</c>. This patch writes the
/// veto; the engine writes the escape. That fall-through is the load-bearing premise of the whole
/// design, and it is pinned by an IL test rather than by hope, because if it ever moves the hero
/// would be neither captured nor escaped and nothing would say so in game.</para>
///
/// <para>The event <c>CampaignEvents.CanHeroBecomePrisonerEvent</c> cannot be used instead:
/// <c>Hero.cs:2010-2012</c> returns true unconditionally for every hero that is not
/// <c>MainHero</c>, before the dispatcher is ever reached, so the event never fires for an AI
/// lord.</para>
/// </summary>
[HarmonyPatch(typeof(Hero), nameof(Hero.CanBecomePrisoner))]
[HarmonyPatchCategory("Patch76_UncapturableHeroes")]
public static class Hero_CanBecomePrisoner_Patch
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

    /// <remarks>
    /// <c>Priority.Last</c> so TAOM's denial is the final word. Postfixes run highest-priority
    /// first, so running last means another mod's postfix cannot re-grant capture after we deny it.
    /// The reverse direction is deliberately left open: the guard below never flips <c>false</c> to
    /// <c>true</c>, so a third-party mod that wants a hero to be LESS capturable still wins.
    /// </remarks>
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    public static void Postfix(Hero __instance, ref bool __result)
    {
        try
        {
            // Never flip false to true. Vanilla returns false (with a failed assert) for anything
            // that is not a lord, companion or special hero, and that verdict is not ours to undo.
            if (!__result)
                return;

            // The player is handled by vanilla on both call sites: MapEvent skips MainHero outright
            // at :1974, and the other caller is the voluntary-surrender menu, which must keep
            // working.
            if (__instance == null || __instance == Hero.MainHero)
                return;

            var service = _service;
            if (service == null)
                return;

            // A hero carrying a death mark other than DiedInBattle/DiedInLabor passes the gate at
            // MapEvent.cs:1977 but then FAILS the DeathMark == None condition on the fugitive
            // fall-through. Denying his capture would leave him neither captured nor escaped,
            // stranded in a defeated roster. Let vanilla have him; the announcement stays honest.
            if (__instance.DeathMark != KillCharacterAction.KillCharacterActionDetail.None)
                return;

            if (!service.ShouldDenyCapture(__instance.StringId, __instance.CharacterObject?.Race))
                return;

            __result = false;
            service.OnBattleCaptureDenied(__instance.Name?.ToString(), IsInPlayerBattle(__instance));
        }
        catch (Exception ex)
        {
            // Leave __result alone and let vanilla decide. Swallowing here is mandatory rather than
            // defensive: PatchShield.ShieldFinalizerWithResult swallows Missing*/TypeLoad
            // exceptions and the patched method then returns default(bool) = false, which would
            // make EVERY hero in the game uncapturable. Failing open is the only safe direction.
            _logger?.LogError($"Hero_CanBecomePrisoner_Patch: {ex.Message}");
        }
    }

    /// <summary>
    /// Whether the player can actually see this escape happen.
    ///
    /// <c>IsPlayerMapEvent</c>, not <c>MapEvent.PlayerMapEvent != null</c>: the latter is
    /// <c>MobileParty.MainParty?.MapEvent</c>, which only means "the player is in some battle" and
    /// would fire on any concurrent AI fight elsewhere on the map. <c>RemovePartyLeader()</c> at
    /// <c>MapEvent.cs:1979-1981</c> only nulls the leader, so the hero's own party reference is
    /// still the defeated party when this gate runs.
    /// </summary>
    private static bool IsInPlayerBattle(Hero hero)
        => hero.PartyBelongedTo?.Party?.MapEvent?.IsPlayerMapEvent == true;
}
