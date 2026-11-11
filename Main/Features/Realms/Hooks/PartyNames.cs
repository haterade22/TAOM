using System;
using System.Collections.Generic;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Features.Realms.Domain.Services;

namespace TAOM.Features.Realms.Hooks;

/// <summary>
/// Lord parties carry their owner's style and highest title: "Lord Forlong of Lossarnach's Party"; an owner without a
/// title keeps the game's name ("Forlong's Party"). The engine builds the name in
/// the private <c>LordPartyComponent.GetPartyName</c> (<c>str_lord_party_name</c>, "{TROOP.NAME}{.o} Party", with the
/// owner as TROOP) and caches it until the owner or leader changes. A postfix puts the styled name into TROOP.NAME, and
/// <see cref="Refresh"/> clears the cache when titles are made, loaded or change hands.
/// </summary>
internal static class PartyNames
{
    private static bool _patched;
    private static bool _faultReported;

    public static void Patch()
    {
        if (_patched)
            return;
        _patched = true;
        try
        {
            var original = AccessTools.Method(typeof(LordPartyComponent), "GetPartyName");
            if (original == null)
            {
                Debug.Print("[TAOM.Realms] LordPartyComponent.GetPartyName not found; party names keep the plain hero name.");
                return;
            }
            new Harmony("TAOM.Realms.PartyNames").Patch(original, postfix: new HarmonyMethod(typeof(PartyNames), nameof(Postfix)));
        }
        catch (Exception ex)
        {
            Debug.Print($"[TAOM.Realms] party name styles are off: {ex}");
        }
    }

    private static void Postfix(LordPartyComponent __instance, TextObject __result)
    {
        try
        {
            var owner = __instance.Owner;
            var behavior = RealmsCampaign.Current;
            if (__result == null || owner?.CharacterObject == null || behavior == null || !behavior.Settings.TitleStyles)
                return;
            var styled = TitleNames.KnownAs(owner, behavior);
            if (styled != null)
                StringHelpers.SetCharacterProperties("TROOP", owner.CharacterObject, __result).SetTextVariable("NAME", styled);
        }
        catch (Exception ex)
        {
            if (_faultReported)
                return;
            _faultReported = true;
            Debug.Print($"[TAOM.Realms] party name style failed; the plain name is kept: {ex}");
        }
    }

    /// <summary>Clears the cached names of all lord parties, so each is built again with the current titles.</summary>
    public static void Refresh()
    {
        var parties = TaleWorlds.CampaignSystem.Campaign.Current != null ? MobileParty.AllLordParties : null;
        if (parties == null)
            return;
        foreach (var party in parties)
            party?.PartyComponent?.ClearCachedName();
    }

    /// <summary>
    /// Clears the cached names of the lord parties whose owner gained or lost a title or office in
    /// <paramref name="events"/>: a party's style follows its owner's own titles and offices alone.
    /// </summary>
    public static void Refresh(IEnumerable<TitleEvent> events)
    {
        var owners = OwnersAffectedBy(events);
        var parties = owners.Count > 0 && TaleWorlds.CampaignSystem.Campaign.Current != null ? MobileParty.AllLordParties : null;
        if (parties == null)
            return;
        foreach (var party in parties)
            if (party?.PartyComponent is LordPartyComponent lord && lord.Owner?.StringId is { } owner && owners.Contains(owner))
                lord.ClearCachedName();
    }

    /// <summary>The heroes whose style the events may change: new and previous holders of titles and offices. A claim changes no style.</summary>
    internal static HashSet<string> OwnersAffectedBy(IEnumerable<TitleEvent> events)
    {
        var owners = new HashSet<string>(StringComparer.Ordinal);
        foreach (var e in events)
        {
            if (e.Kind == TitleEventKind.ClaimGained)
                continue;
            if (e.HeroId != null)
                owners.Add(e.HeroId);
            if (e.PreviousHolderId != null)
                owners.Add(e.PreviousHolderId);
        }

        return owners;
    }
}
