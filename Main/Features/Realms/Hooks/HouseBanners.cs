using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace TAOM.Features.Realms.Hooks;

/// <summary>
/// A ruling house keeps its own banner, apart from its realm's (docs/features/realms.md, "House and realm banners").
/// The engine stores every clan's own banner but <c>Clan.Banner</c> hands a ruling clan its kingdom's banner instead.
/// That stays the rule everywhere except in a few scopes, where a prefix on the getter returns the clan's own
/// (<c>ClanOriginalBanner</c>): the house's parties on the map (party nameplate, the banner a lord hangs at a town or
/// siege camp, the party markers on a settlement's nameplate), the encyclopedia (clan page and faction cards) and the
/// banner editor. Settlements, the clan screen and battles read the getter outside a scope and show the realm's.
/// Runtime only: nothing is saved, and without the patch the game falls back to the shared banner.
/// <para>
/// <c>KingdomManager.CreateKingdom</c> gives a new kingdom its founder's banner object itself, so editing one would
/// edit both; <c>Kingdom.InitializeKingdom</c> gets a copy instead, and <see cref="Decouple"/> splits the kingdoms of
/// older saves on load.
/// </para>
/// Off while Realms is (a save it could not read).
/// </summary>
internal static class HouseBanners
{
    internal static RealmsCampaign? Behavior;
    private static bool _patched;

    // Depth of the scopes that show the house banner. Per thread: a scope on one thread never leaks into another.
    [ThreadStatic]
    private static int _shown;

    public static void Patch()
    {
        if (_patched)
            return;
        _patched = true;
        try
        {
            var harmony = new Harmony("TAOM.Realms.HouseBanners");
            var getter = AccessTools.PropertyGetter(typeof(Clan), nameof(Clan.Banner));
            if (getter == null)
            {
                Debug.Print("[TAOM.Realms] Clan.Banner not found; ruling houses fly their realm's banner.");
                return;
            }

            harmony.Patch(getter, prefix: new HarmonyMethod(typeof(HouseBanners), nameof(BannerPrefix)));

            var initialize = AccessTools.Method(typeof(Kingdom), nameof(Kingdom.InitializeKingdom));
            if (initialize != null)
                harmony.Patch(initialize, prefix: new HarmonyMethod(typeof(HouseBanners), nameof(InitializePrefix)));
            else
                Debug.Print("[TAOM.Realms] Kingdom.InitializeKingdom not found; a new kingdom shares its founder's banner.");

            // Parties on the map. A lord's or caravan's party banner comes from its component.
            ShowWithin(harmony, AccessTools.Method(typeof(WarPartyComponent), nameof(WarPartyComponent.GetDefaultComponentBanner)),
                "war party banner");
            ShowWithin(harmony, AccessTools.Method(typeof(CaravanPartyComponent), nameof(CaravanPartyComponent.GetDefaultComponentBanner)),
                "caravan banner");

            // UI types are absent on a dedicated server; they are looked up by name.
            var partyVisual = AccessTools.TypeByName("SandBox.View.Map.Visuals.MobilePartyVisual");
            ShowWithin(harmony, partyVisual == null ? null : AccessTools.Method(partyVisual, "RefreshPartyIcon"),
                "banner of a party at a town");
            ShowWithin(harmony, partyVisual == null ? null : AccessTools.Method(partyVisual, "AddTentEntityForParty"),
                "siege camp banner");
            var marker = AccessTools.TypeByName("SandBox.ViewModelCollection.Nameplate.SettlementNameplatePartyMarkerItemVM");
            ShowWithin(harmony, marker == null ? null : AccessTools.Constructor(marker, new[] { typeof(MobileParty) }),
                "settlement party marker");

            // Encyclopedia and banner editor.
            var clanPage = AccessTools.TypeByName("TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Pages.EncyclopediaClanPageVM");
            ShowWithin(harmony, clanPage == null ? null : AccessTools.Method(clanPage, "Refresh"), "encyclopedia clan page");
            var factionCard = AccessTools.TypeByName("TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Items.EncyclopediaFactionVM");
            ShowWithin(harmony, factionCard == null ? null : AccessTools.Constructor(factionCard, new[] { typeof(IFaction) }),
                "encyclopedia faction card");
            var editor = AccessTools.TypeByName("SandBox.GauntletUI.BannerEditor.GauntletBannerEditorScreen");
            ShowWithin(harmony, editor == null ? null : AccessTools.FirstConstructor(editor, c => c.GetParameters().Length == 1),
                "banner editor");
        }
        catch (Exception ex)
        {
            Debug.Print($"[TAOM.Realms] house banners are off: {ex}");
        }
    }

    /// <summary>The house banner and realm banner are apart: the patch is in and Realms is on.</summary>
    public static bool IsActive => _patched && Behavior is { IsDisabled: false };

    /// <summary>
    /// The banner the clan's house flies: its own, even when it rules a realm (where <c>Clan.Banner</c> gives the
    /// realm's). For code that shows the house, such as the TAOM encyclopedia list and title pages.
    /// </summary>
    public static Banner? HouseBannerOf(Clan? clan)
    {
        if (clan == null)
            return null;
        _shown++;
        try
        {
            return clan.Banner;
        }
        finally
        {
            _shown--;
        }
    }

    /// <summary>
    /// Gives each kingdom a banner object of its own where it still shares its ruling clan's (a kingdom founded before
    /// this version), so editing the house banner no longer edits the realm's. Looks the same; safe to run on every peer.
    /// </summary>
    public static void Decouple()
    {
        if (!IsActive)
            return;
        foreach (var kingdom in Kingdom.All)
        {
            var banner = kingdom?.Banner;
            if (banner != null && ReferenceEquals(banner, kingdom!.RulingClan?.ClanOriginalBanner))
                kingdom.Banner = new Banner(banner);
        }
    }

    /// <summary>Marks the visuals of everything that shows the clan's banner for a redraw.</summary>
    public static void Redraw(Clan? clan)
    {
        if (clan == null)
            return;
        foreach (var party in clan.WarPartyComponents)
            party?.MobileParty?.Party?.SetVisualAsDirty();
        foreach (var settlement in clan.Settlements)
            settlement?.Party?.SetVisualAsDirty();
    }

    private static void ShowWithin(Harmony harmony, MethodBase? method, string what)
    {
        if (method == null)
        {
            Debug.Print($"[TAOM.Realms] {what} not found; it shows a ruling house's realm banner.");
            return;
        }
        harmony.Patch(method, prefix: new HarmonyMethod(typeof(HouseBanners), nameof(Enter)),
            finalizer: new HarmonyMethod(typeof(HouseBanners), nameof(Leave)));
    }

    private static void Enter() => _shown++;

    // Observes only: a void finalizer lets an exception from the scoped method go on unchanged.
    private static void Leave()
    {
        if (_shown > 0)
            _shown--;
    }

    private static bool BannerPrefix(Clan __instance, ref Banner __result)
    {
        if (_shown == 0 || !IsActive)
            return true;
        var own = __instance.ClanOriginalBanner;
        if (own == null)
            return true;
        __result = own;
        return false;
    }

    private static void InitializePrefix(ref Banner banner)
    {
        if (banner != null && IsActive)
            banner = new Banner(banner);
    }
}
