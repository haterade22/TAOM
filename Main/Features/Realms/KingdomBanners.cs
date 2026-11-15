using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TAOM.Features.Realms.Domain.Persistence;
using TAOM.Features.Realms.Domain.Services;
using TAOM.Features.Realms.Hooks;

namespace TAOM.Features.Realms;

/// <summary>
/// Gives a kingdom the banner of its ruler's primary title (<see cref="RealmsService.KingdomBannerOf"/>, a title's
/// <c>banner</c> in realms_titles.xml), and its own banner back when the ruler no longer has such a title. The ruling
/// house keeps its own banner on its parties and in the encyclopedia (<see cref="HouseBanners"/>). The kingdom's own banner is kept in the save
/// until then. Banners are set again every session, after BannerInjection has set each realm to its banner_key.
/// </summary>
internal sealed class KingdomBanners
{
    private Dictionary<string, string> _originals = new(StringComparer.Ordinal);

    /// <summary>The banner code each kingdom was given this session; not saved.</summary>
    private readonly Dictionary<string, string> _applied = new(StringComparer.Ordinal);

    public string Save() => KingdomBannerRecords.Write(_originals);

    public void Load(string? text)
    {
        _originals = KingdomBannerRecords.Read(text);
        _applied.Clear();
    }

    /// <summary>
    /// Sets the banner of each kingdom whose banner changed: its ruler's primary title changed, or <paramref name="choice"/>
    /// (the MCM option for its ruler) did. A few title lookups per kingdom.
    /// </summary>
    public void Update(RealmsService service, Func<Kingdom, RealmBanner>? choice = null)
    {
        if (!HouseBanners.IsActive)
            return; // without the split, a title banner would replace the ruling house's banner too
        foreach (var kingdom in Kingdom.All)
        {
            var id = kingdom?.StringId;
            if (id == null || kingdom!.IsEliminated)
                continue;
            var wanted = choice?.Invoke(kingdom) ?? RealmBanner.Title;
            var code = BannerOf(kingdom, service, wanted);
            _applied.TryGetValue(id, out var current);
            if (code == current && (code != null || !_originals.ContainsKey(id)))
                continue;

            if (code == null)
            {
                kingdom.Banner = new Banner(_originals[id]);
                _originals.Remove(id);
                _applied.Remove(id);
                Debug.Print($"[TAOM.Realms] {id} takes its own banner back.");
                Redraw(kingdom);
                continue;
            }

            if (!_originals.ContainsKey(id))
            {
                var own = kingdom.Banner?.Serialize();
                if (string.IsNullOrEmpty(own))
                    continue; // nothing to give back later
                _originals[id] = own!;
            }

            if (kingdom.Banner?.Serialize() != code)
                kingdom.Banner = new Banner(code);
            _applied[id] = code;
            Debug.Print(wanted == RealmBanner.House
                ? $"[TAOM.Realms] {id} flies its ruling house's banner."
                : $"[TAOM.Realms] {id} flies the banner of its ruler's primary title.");
            Redraw(kingdom);
        }
    }

    /// <summary>The banner code the kingdom should fly, or null for its own.</summary>
    private static string? BannerOf(Kingdom kingdom, RealmsService service, RealmBanner choice) => choice switch
    {
        RealmBanner.Title => service.KingdomBannerOf(kingdom.StringId, kingdom.Leader?.StringId),
        RealmBanner.House => NullIfEmpty(kingdom.RulingClan?.ClanOriginalBanner?.Serialize()),
        _ => null,
    };

    private static string? NullIfEmpty(string? code) => string.IsNullOrEmpty(code) ? null : code;

    /// <summary>
    /// Gives each kingdom a banner object of its own where it still shares its ruling clan's (a kingdom founded before
    /// this version), so editing the house banner no longer edits the realm's. Looks the same; safe to run on every peer.
    /// </summary>
    public static void Decouple()
    {
        if (!HouseBanners.IsActive)
            return;
        foreach (var kingdom in Kingdom.All)
        {
            var banner = kingdom?.Banner;
            if (banner != null && ReferenceEquals(banner, kingdom!.RulingClan?.ClanOriginalBanner))
                kingdom.Banner = new Banner(banner);
        }
    }

    /// <summary>Marks the visuals of everything that shows a banner of the kingdom's clans for a redraw.</summary>
    private static void Redraw(Kingdom kingdom)
    {
        foreach (var clan in kingdom.Clans)
        {
            foreach (var party in clan.WarPartyComponents)
                party?.MobileParty?.Party?.SetVisualAsDirty();
            foreach (var settlement in clan.Settlements)
                settlement?.Party?.SetVisualAsDirty();
        }
    }
}
