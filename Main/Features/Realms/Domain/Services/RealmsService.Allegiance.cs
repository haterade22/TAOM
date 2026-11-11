using System;
using System.Collections.Generic;
using TAOM.Features.Realms.Domain.Titles;

namespace TAOM.Features.Realms.Domain.Services;

/// <summary>
/// Whose lands count as the hero's when they usurp or form a title above them. A lord does not keep every fief himself:
/// he spreads them over his family and his vassals, and what they hold still stands behind him.
/// </summary>
public sealed partial class RealmsService
{
    /// <summary>The hero's side, built once per quote or offer list (see <see cref="Allegiance"/>).</summary>
    private Allegiance AllegianceOf(string heroId, IClaimWorld world) => new(this, heroId, world);

    /// <summary>
    /// The hero, their clan, and their vassals at any depth: lords of the same realm who hold a title directly below one
    /// held by someone already counted. A ruler counts every lord serving the realm. A town, castle or village title also
    /// counts while one of them owns its fief, so a fief given to a vassal clan counts before its title is pressed.
    /// </summary>
    private sealed class Allegiance
    {
        private readonly RealmsService _service;
        private readonly IClaimWorld _world;
        private readonly string _heroId;
        private readonly string? _clanId;
        private readonly string? _realm;
        private readonly bool _rules;
        private HashSet<string>? _vassals;

        public Allegiance(RealmsService service, string heroId, IClaimWorld world)
        {
            _service = service;
            _world = world;
            _heroId = heroId;
            _clanId = world.ClanOf(heroId);
            _realm = world.KingdomOfHero(heroId);
            _rules = _realm != null && world.RulerOf(_realm) == heroId;
        }

        /// <summary>Whether the title counts for the hero: its holder is on their side, or (a fief title) its fief's owner is.</summary>
        public bool Counts(TitleDefinition title) =>
            Includes(_service.Ledger.HolderOf(title.Id))
            || (title.SettlementId != null && Includes(_world.OwnerOf(title.SettlementId)));

        private bool Includes(string? heroId)
        {
            if (heroId == null)
                return false;
            if (Near(heroId))
                return true;
            _vassals ??= Vassals();
            return _vassals.Contains(heroId);
        }

        /// <summary>The hero, their clan and, for a ruler, the whole realm: no title lookups.</summary>
        private bool Near(string heroId) =>
            heroId == _heroId
            || (_clanId != null && _world.ClanOf(heroId) == _clanId)
            || (_rules && _world.KingdomOfHero(heroId) == _realm);

        /// <summary>Lords of the realm below the hero's side in the title tree, followed down until nobody new is found.</summary>
        private HashSet<string> Vassals()
        {
            var vassals = new HashSet<string>(StringComparer.Ordinal);
            if (_realm == null || _rules)
                return vassals;
            bool added;
            do
            {
                added = false;
                foreach (var title in _service.Catalog.All)
                {
                    if (title.ParentId == null)
                        continue;
                    var holder = _service.Ledger.HolderOf(title.Id);
                    if (holder == null || vassals.Contains(holder) || Near(holder))
                        continue;
                    var liege = _service.Ledger.HolderOf(title.ParentId);
                    if (liege == null || !(vassals.Contains(liege) || Near(liege)) || _world.KingdomOfHero(holder) != _realm)
                        continue;
                    vassals.Add(holder);
                    added = true;
                }
            }
            while (added);

            return vassals;
        }
    }
}
