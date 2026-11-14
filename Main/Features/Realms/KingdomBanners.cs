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

    /// <summary>Sets the banner of each kingdom whose ruler's primary title changed. A few title lookups per kingdom.</summary>
    public void Update(RealmsService service)
    {
        if (!HouseBanners.IsActive)
            return; // without the split, a title banner would replace the ruling house's banner too
        foreach (var kingdom in Kingdom.All)
        {
            var id = kingdom?.StringId;
            if (id == null || kingdom!.IsEliminated)
                continue;
            var code = service.KingdomBannerOf(id, kingdom.Leader?.StringId);
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
            Debug.Print($"[TAOM.Realms] {id} flies the banner of its ruler's primary title.");
            Redraw(kingdom);
        }
    }

    private static void Redraw(Kingdom kingdom)
    {
        foreach (var clan in kingdom.Clans)
            HouseBanners.Redraw(clan);
    }
}
