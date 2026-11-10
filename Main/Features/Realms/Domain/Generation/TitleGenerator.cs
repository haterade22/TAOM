using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Features.Realms.Domain.Config;
using TAOM.Features.Realms.Domain.Ledger;
using TAOM.Features.Realms.Domain.Titles;
using TAOM.Features.Realms.Domain.World;

namespace TAOM.Features.Realms.Domain.Generation;

public sealed class GenerationResult
{
    public GenerationResult(TitleCatalog catalog, TitleLedger ledger, IReadOnlyList<string> warnings)
    {
        Catalog = catalog;
        Ledger = ledger;
        Warnings = warnings;
    }

    public TitleCatalog Catalog { get; }
    public TitleLedger Ledger { get; }
    public IReadOnlyList<string> Warnings { get; }
}

/// <summary>
/// Builds the de jure map from the campaign as it stands at the start: one Kingdom title per kingdom,
/// a County per town, a Barony per castle (under the nearest county of its configured duchy, else of its kingdom), a Lordship
/// per village (under its town or castle), Duchies from the config and automatic grouping, and the
/// configured high titles. Initial de jure holders are the current owners.
/// </summary>
public static class TitleGenerator
{
    public const string KingdomPrefix = "k_", EmpirePrefix = "e_", DuchyPrefix = "d_",
        CountyPrefix = "c_", BaronyPrefix = "b_", LordshipPrefix = "l_", UnionPrefix = "u_";

    public static GenerationResult Generate(WorldSnapshot world, RealmsConfig config)
    {
        var warnings = new List<string>();
        var definitions = new List<TitleDefinition>();
        var ledger = new TitleLedger { DefaultLaws = config.DefaultLaws };
        var kingdoms = world.Kingdoms.ToDictionary(k => k.Id);
        var empireOf = AddHighTitles(config, kingdoms, definitions, ledger, warnings);

        foreach (var kingdom in world.Kingdoms)
        {
            var id = KingdomPrefix + kingdom.Id;
            empireOf.TryGetValue(kingdom.Id, out var empireId);
            definitions.Add(new TitleDefinition(id, TitleRank.Kingdom, kingdom.Name, empireId, null, kingdom.Id, kingdom.CultureId));
            // A vacant throne (Gondor's, while the Stewards rule) starts without a holder; its ruler governs untitled.
            ledger.SetHolder(id, config.VacantThrones.Contains(kingdom.Id) ? null : kingdom.RulerHeroId);
            if (config.KingdomLaws.TryGetValue(kingdom.Id, out var laws))
                ledger.SetLaws(id, laws);
        }

        var fiefs = world.Fiefs;
        var towns = fiefs.Where(f => f.Kind == FiefKind.Town).ToList();
        var castles = fiefs.Where(f => f.Kind == FiefKind.Castle).ToList();
        var duchyOf = AddDuchies(config, towns, castles, kingdoms, definitions, ledger, warnings);

        foreach (var town in towns)
        {
            duchyOf.TryGetValue(town.Id, out var duchyId);
            AddFiefTitle(definitions, ledger, CountyPrefix, TitleRank.County, town, duchyId ?? KingdomTitleOf(town, kingdoms));
        }

        foreach (var castle in castles)
        {
            // A castle a duchy lists joins the nearest county of that duchy, or the duchy itself when it has no
            // county; any other castle joins the nearest county of its kingdom.
            string? parent;
            if (duchyOf.TryGetValue(castle.Id, out var duchyId))
            {
                var county = Nearest(towns.Where(t => duchyOf.TryGetValue(t.Id, out var d) && d == duchyId), castle);
                parent = county != null ? CountyPrefix + county.Id : duchyId;
            }
            else
            {
                var county = Nearest(towns.Where(t => t.KingdomId != null && t.KingdomId == castle.KingdomId), castle);
                parent = county != null ? CountyPrefix + county.Id : KingdomTitleOf(castle, kingdoms);
            }

            AddFiefTitle(definitions, ledger, BaronyPrefix, TitleRank.Barony, castle, parent);
        }

        if (config.IncludeVillages)
            AddLordships(fiefs, definitions, ledger, warnings);

        var catalog = new TitleCatalog(WithUnions(definitions, ledger, config, warnings));
        warnings.AddRange(catalog.Problems);
        return new GenerationResult(catalog, ledger, warnings);
    }

    /// <summary>
    /// The titles with the configured unions added that are not there yet (id <see cref="UnionPrefix"/> + id): each sits
    /// above its member titles that exist and have no liege title, and is held by their holder when one hero holds them
    /// all. A union with fewer than two such members is skipped. Titles already present are kept as they are.
    /// </summary>
    public static List<TitleDefinition> WithUnions(IEnumerable<TitleDefinition> titles, TitleLedger ledger, RealmsConfig config, List<string> warnings)
    {
        var definitions = titles.ToList();
        var index = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < definitions.Count; i++)
            index[definitions[i].Id] = i;
        foreach (var union in config.Unions)
        {
            var id = UnionPrefix + union.Id;
            if (index.ContainsKey(id))
                continue;
            var members = union.MemberTitleIds.Distinct()
                .Where(m => index.TryGetValue(m, out var i) && definitions[i].ParentId == null && definitions[i].Rank >= TitleRank.Kingdom)
                .ToList();
            if (members.Count < 2)
            {
                warnings.Add($"Union '{union.Id}' has fewer than two member titles without a liege title in this campaign; skipped.");
                continue;
            }

            if (members.Count < union.MemberTitleIds.Distinct().Count())
                warnings.Add($"Union '{union.Id}': some member titles are missing, below another title or below kingdom rank; left out.");
            definitions.Add(new TitleDefinition(id, TitleRank.Union, union.Name, null, null, null, definitions[index[members[0]]].CultureId));
            index[id] = definitions.Count - 1;
            foreach (var member in members)
                definitions[index[member]] = definitions[index[member]].WithParent(id);
            var holders = members.Select(ledger.HolderOf).Distinct().ToList();
            ledger.SetHolder(id, holders.Count == 1 ? holders[0] : null);
        }

        return definitions;
    }

    private static Dictionary<string, string> AddHighTitles(
        RealmsConfig config, IReadOnlyDictionary<string, KingdomInfo> kingdoms,
        List<TitleDefinition> definitions, TitleLedger ledger, List<string> warnings)
    {
        var empireOf = new Dictionary<string, string>();
        foreach (var high in config.HighTitles)
        {
            var members = high.MemberKingdomIds.Where(kingdoms.ContainsKey).Where(k => !empireOf.ContainsKey(k)).ToList();
            if (members.Count == 0)
            {
                warnings.Add($"High title '{high.Id}' has no member kingdom in this campaign; skipped.");
                continue;
            }

            var id = EmpirePrefix + high.Id;
            definitions.Add(new TitleDefinition(id, TitleRank.Empire, high.Name, null, null, null, kingdoms[members[0]].CultureId));
            foreach (var member in members)
                empireOf[member] = id;
            var rulers = members.Select(m => kingdoms[m].RulerHeroId).Distinct().ToList();
            ledger.SetHolder(id, rulers.Count == 1 ? rulers[0] : null);
            ledger.SetLaws(id, high.Laws);
        }

        return empireOf;
    }

    /// <summary>
    /// Makes the configured and automatic duchies. Returns the duchy title id of each town and of each castle a
    /// configured duchy lists. A settlement is only taken when it belongs to the duchy's kingdom and no earlier
    /// duchy has it; a configured duchy left with no town and no castle is skipped.
    /// </summary>
    private static Dictionary<string, string> AddDuchies(
        RealmsConfig config, IReadOnlyList<FiefInfo> towns, IReadOnlyList<FiefInfo> castles,
        IReadOnlyDictionary<string, KingdomInfo> kingdoms,
        List<TitleDefinition> definitions, TitleLedger ledger, List<string> warnings)
    {
        var duchyOf = new Dictionary<string, string>();
        var townsById = towns.ToDictionary(t => t.Id);
        var castlesById = castles.ToDictionary(c => c.Id);
        List<FiefInfo> Take(IEnumerable<string> ids, IReadOnlyDictionary<string, FiefInfo> byId, string kingdomId) =>
            ids.Distinct()
                .Where(id => byId.TryGetValue(id, out var f) && f.KingdomId == kingdomId && !duchyOf.ContainsKey(id))
                .Select(id => byId[id]).ToList();

        foreach (var duchy in config.Duchies)
        {
            var counties = Take(duchy.CountySettlementIds, townsById, duchy.KingdomId);
            var baronies = Take(duchy.BaronySettlementIds, castlesById, duchy.KingdomId);
            if ((counties.Count == 0 && baronies.Count == 0) || !kingdoms.ContainsKey(duchy.KingdomId))
            {
                warnings.Add($"Duchy '{duchy.Id}' has no town or castle of kingdom '{duchy.KingdomId}' in this campaign; skipped.");
                continue;
            }

            var left = duchy.CountySettlementIds.Distinct().Count() + duchy.BaronySettlementIds.Distinct().Count()
                - counties.Count - baronies.Count;
            if (left > 0)
                warnings.Add($"Duchy '{duchy.Id}': {left} listed settlement(s) are missing, of another kingdom or already in a duchy; left out.");
            AddDuchy(definitions, ledger, DuchyPrefix + duchy.Id, duchy.Name, kingdoms[duchy.KingdomId], counties, baronies, duchyOf);
        }

        foreach (var realm in towns.Where(t => t.KingdomId != null && kingdoms.ContainsKey(t.KingdomId)).GroupBy(t => t.KingdomId!))
        {
            var free = realm.Where(t => !duchyOf.ContainsKey(t.Id));
            foreach (var group in DuchyGrouping.Group(free, config.CountiesPerDuchy))
                AddDuchy(definitions, ledger, DuchyPrefix + group[0].Id, group[0].Name, kingdoms[realm.Key], group, new FiefInfo[0], duchyOf);
        }

        return duchyOf;
    }

    private static void AddDuchy(
        List<TitleDefinition> definitions, TitleLedger ledger, string id, string name, KingdomInfo kingdom,
        IReadOnlyList<FiefInfo> counties, IReadOnlyList<FiefInfo> baronies, Dictionary<string, string> duchyOf)
    {
        definitions.Add(new TitleDefinition(id, TitleRank.Duchy, name, KingdomPrefix + kingdom.Id, null, kingdom.Id, kingdom.CultureId));
        foreach (var member in counties.Concat(baronies))
            duchyOf[member.Id] = id;

        // The duke is whoever owns the most of the duchy's towns (its castles when it has no town);
        // on a tie, the owner listed first.
        var members = counties.Count > 0 ? counties : baronies;
        var holder = members.Where(m => m.OwnerHeroId != null)
            .GroupBy(m => m.OwnerHeroId!)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => members.TakeWhile(m => m.OwnerHeroId != g.Key).Count())
            .Select(g => g.Key).FirstOrDefault();
        ledger.SetHolder(id, holder);
    }

    private static FiefInfo? Nearest(IEnumerable<FiefInfo> candidates, FiefInfo fief) =>
        candidates.OrderBy(t => DuchyGrouping.Distance(t, fief)).ThenBy(t => t.Id, StringComparer.Ordinal).FirstOrDefault();

    private static void AddLordships(IReadOnlyList<FiefInfo> fiefs, List<TitleDefinition> definitions, TitleLedger ledger, List<string> warnings)
    {
        var fortifications = fiefs.Where(f => f.Kind != FiefKind.Village).ToDictionary(f => f.Id);
        foreach (var village in fiefs.Where(f => f.Kind == FiefKind.Village))
        {
            if (village.BoundFiefId == null || !fortifications.TryGetValue(village.BoundFiefId, out var bound))
            {
                warnings.Add($"Village '{village.Id}' has no known town or castle; its lordship has no parent.");
                AddFiefTitle(definitions, ledger, LordshipPrefix, TitleRank.Lordship, village, null);
                continue;
            }

            var prefix = bound.Kind == FiefKind.Town ? CountyPrefix : BaronyPrefix;
            AddFiefTitle(definitions, ledger, LordshipPrefix, TitleRank.Lordship, village, prefix + bound.Id);
        }
    }

    private static void AddFiefTitle(
        List<TitleDefinition> definitions, TitleLedger ledger, string prefix, TitleRank rank, FiefInfo fief, string? parentId)
    {
        var id = prefix + fief.Id;
        definitions.Add(new TitleDefinition(id, rank, fief.Name, parentId, fief.Id, fief.KingdomId, fief.CultureId));
        ledger.SetHolder(id, fief.OwnerHeroId);
    }

    private static string? KingdomTitleOf(FiefInfo fief, IReadOnlyDictionary<string, KingdomInfo> kingdoms) =>
        fief.KingdomId != null && kingdoms.ContainsKey(fief.KingdomId) ? KingdomPrefix + fief.KingdomId : null;
}
