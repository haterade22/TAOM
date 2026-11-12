using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using TAOM.Features.Realms.Domain.Laws;
using TAOM.Features.Realms.Domain.Titles;

namespace TAOM.Features.Realms.Domain.Config;

public sealed class ConfigReadResult
{
    public ConfigReadResult(RealmsConfig config, IReadOnlyList<string> warnings)
    {
        Config = config;
        Warnings = warnings;
    }

    public RealmsConfig Config { get; }
    public IReadOnlyList<string> Warnings { get; }
}

/// <summary>
/// Reads ModuleData/realms_titles.xml. Lenient: a bad value is reported and its default kept,
/// so a typo in the file never stops a campaign from starting.
/// </summary>
public static class RealmsConfigXml
{
    public static ConfigReadResult Parse(XDocument document)
    {
        var config = RealmsConfig.Defaults();
        var warnings = new List<string>();
        var root = document.Root;
        if (root == null || root.Name.LocalName != "Realms")
        {
            warnings.Add("The root element is not <Realms>; defaults are used.");
            return new ConfigReadResult(config, warnings);
        }

        ReadGeneration(root.Element("Generation"), config, warnings);
        var defaults = root.Element("DefaultLaws");
        if (defaults != null)
            config.DefaultLaws = ReadLaws(defaults, config.DefaultLaws, warnings) ?? config.DefaultLaws;
        ReadRankNames(root.Element("RankNames"), config.RankNames, warnings);
        ReadClaims(root.Element("Claims"), config.Claims, warnings);
        foreach (var prefix in Children(root, "Prefixes", "Prefix"))
        {
            var rule = ReadPrefix(prefix, warnings);
            if (rule != null)
                config.RankNames.AddPrefix(rule);
        }

        foreach (var kingdom in Children(root, "Kingdoms", "Kingdom"))
        {
            var id = Required(kingdom, "id", warnings);
            var laws = ReadLaws(kingdom, config.DefaultLaws, warnings);
            if (id != null && laws != null)
                config.KingdomLaws[id] = laws;
            var capital = (string?)kingdom.Attribute("capital");
            if (id != null && !string.IsNullOrWhiteSpace(capital))
                config.KingdomCapitals[id] = capital!;
            var throne = (string?)kingdom.Attribute("throne");
            if (id != null && throne == "vacant")
                config.VacantThrones.Add(id);
            else if (throne != null)
                warnings.Add($"<Kingdom id=\"{id}\"> throne '{throne}' is not 'vacant'; ignored.");
        }

        ReadFeudal(root, config.Feudal, warnings);
        foreach (var union in Children(root, "Unions", "Union"))
        {
            var id = Required(union, "id", warnings);
            var members = union.Elements("Member").Select(m => Required(m, "title", warnings)).Where(t => t != null).Select(t => t!).ToList();
            if (id != null)
            {
                config.Unions.Add(new UnionConfig(id, (string?)union.Attribute("name") ?? id, members));
                ReadRealmName(union, "u_" + id, config);
            }
        }

        foreach (var realm in Children(root, "TitularRealms", "Realm"))
        {
            var id = Required(realm, "id", warnings);
            var lands = Required(realm, "lands", warnings);
            if (id == null || lands == null)
                continue;
            if (config.TitularRealms.Any(r => r.Id == id))
            {
                warnings.Add($"<Realm id=\"{id}\"> is defined twice; the second is ignored.");
                continue;
            }

            var titular = new TitularRealmConfig(id, (string?)realm.Attribute("name") ?? id, (string?)realm.Attribute("culture"),
                (string?)realm.Attribute("liege"), lands, Words(realm, "cultures"), ReadLaws(realm, config.DefaultLaws, warnings));
            config.TitularRealms.Add(titular);
            ReadRealmName(realm, titular.TitleId, config);
        }

        foreach (var claim in Children(root, "StartingClaims", "Claim"))
        {
            var hero = Required(claim, "hero", warnings);
            var title = Required(claim, "title", warnings);
            var kindText = (string?)claim.Attribute("kind") ?? nameof(Ledger.ClaimKind.Ancestral);
            if (!TryEnum<Ledger.ClaimKind>(kindText, out var kind))
            {
                warnings.Add($"<Claim hero=\"{hero}\"> kind '{kindText}' is not a known claim kind; ignored.");
                continue;
            }

            if (hero != null && title != null)
                config.StartingClaims.Add(new StartingClaimConfig(hero, title, kind));
        }

        foreach (var coronation in Children(root, "Coronations", "Coronation"))
        {
            var title = Required(coronation, "title", warnings);
            var hero = Required(coronation, "hero", warnings);
            var clan = Required(coronation, "clan", warnings);
            if (title == null || hero == null || clan == null)
                continue;
            if (config.Coronations.Any(c => c.TitleId == title && c.HeroId == hero))
            {
                warnings.Add($"<Coronation title=\"{title}\" hero=\"{hero}\"> is defined twice; the second is ignored.");
                continue;
            }

            config.Coronations.Add(new CoronationConfig(title, hero, clan, (string?)coronation.Attribute("informal"),
                (string?)coronation.Attribute("banner_of"), Words(coronation, "fiefs"), Words(coronation, "titles"),
                (string?)coronation.Attribute("office"), Int(coronation, "tier", 4, 0, warnings),
                Int(coronation, "troops", 33, 0, warnings), Int(coronation, "gold", 20000, 0, warnings),
                Int(coronation, "relation", 50, 0, warnings),
                coronation.Attribute("influence") != null ? Int(coronation, "influence", 0, 0, warnings) : (int?)null,
                (string?)coronation.Attribute("former_banner"),
                new CoronationEvent((string?)coronation.Attribute("heading"), (string?)coronation.Attribute("text"),
                    (string?)coronation.Attribute("image"), (string?)coronation.Attribute("button")),
                (string?)coronation.Attribute("banner")));
        }

        foreach (var office in Children(root, "Offices", "Office"))
        {
            var id = Required(office, "id", warnings);
            var kingdom = Required(office, "kingdom", warnings);
            var rankText = (string?)office.Attribute("rank") ?? nameof(TitleRank.Duchy);
            if (!TryEnum<TitleRank>(rankText, out var rank))
            {
                warnings.Add($"<Office id=\"{id}\"> rank '{rankText}' is not a known rank; ignored.");
                continue;
            }

            var follows = (string?)office.Attribute("follows");
            if (follows != null && follows != "ruler")
                warnings.Add($"<Office id=\"{id}\"> follows '{follows}' is not 'ruler'; ignored.");
            if (id == null || kingdom == null || config.Offices.Any(o => o.Id == id))
            {
                if (id != null && kingdom != null)
                    warnings.Add($"<Office id=\"{id}\"> is defined twice; the second is ignored.");
                continue;
            }

            config.Offices.Add(new OfficeConfig(id, kingdom, (string?)office.Attribute("name") ?? id,
                (string?)office.Attribute("holder"), (string?)office.Attribute("holder_female"), (string?)office.Attribute("of"),
                rank, follows == "ruler", Int(office, "influence", 0, 0, warnings), (string?)office.Attribute("initial")));
        }

        foreach (var high in Children(root, "HighTitles", "HighTitle"))
        {
            var id = Required(high, "id", warnings);
            var members = high.Elements("Member").Select(m => (string?)m.Attribute("kingdom")).Where(k => !string.IsNullOrEmpty(k)).Select(k => k!).ToList();
            if (id != null)
            {
                config.HighTitles.Add(new HighTitleConfig(id, (string?)high.Attribute("name") ?? id, members, ReadLaws(high, config.DefaultLaws, warnings)));
                ReadRealmName(high, "e_" + id, config);
            }
        }

        foreach (var duchy in Children(root, "Duchies", "Duchy"))
        {
            var id = Required(duchy, "id", warnings);
            var kingdom = Required(duchy, "kingdom", warnings);
            if (id != null && kingdom != null)
                config.Duchies.Add(new DuchyConfig(id, (string?)duchy.Attribute("name") ?? id, kingdom,
                    Settlements(duchy, "County", warnings), Settlements(duchy, "Barony", warnings)));
        }

        return new ConfigReadResult(config, warnings);
    }

    private static void ReadGeneration(XElement? generation, RealmsConfig config, List<string> warnings)
    {
        if (generation == null)
            return;
        var villages = (string?)generation.Attribute("include_villages");
        if (villages != null)
        {
            if (bool.TryParse(villages, out var include))
                config.IncludeVillages = include;
            else
                warnings.Add($"include_villages '{villages}' is not true or false; kept {config.IncludeVillages}.");
        }

        var size = (string?)generation.Attribute("counties_per_duchy");
        if (size == null)
            return;
        if (int.TryParse(size, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n >= 0)
            config.CountiesPerDuchy = n;
        else
            warnings.Add($"counties_per_duchy '{size}' is not a whole number of 0 or more; kept {config.CountiesPerDuchy}.");
    }

    /// <summary>
    /// &lt;Claims neighbors relation usurp_relation ai ai_chance ai_gold_per_point ai_reserve&gt; with
    /// &lt;Rank rank buy usurp/&gt; children. A rank listed without buy cannot be bought; without usurp, not usurped.
    /// </summary>
    private static void ReadClaims(XElement? claims, ClaimsConfig config, List<string> warnings)
    {
        if (claims == null)
            return;
        config.Neighbors = Int(claims, "neighbors", config.Neighbors, 1, warnings);
        config.BuyRelation = Int(claims, "relation", config.BuyRelation, -100, warnings);
        config.UsurpRelation = Int(claims, "usurp_relation", config.UsurpRelation, -100, warnings);
        config.AiGoldPerPoint = Int(claims, "ai_gold_per_point", config.AiGoldPerPoint, 0, warnings);
        config.AiChance = Float(claims, "ai_chance", config.AiChance, warnings);
        config.AiReserve = Float(claims, "ai_reserve", config.AiReserve, warnings);
        var ancestral = (string?)claims.Attribute("ancestral_min_rank");
        if (ancestral != null)
        {
            if (TryEnum<TitleRank>(ancestral, out var minRank))
                config.AncestralMinRank = minRank;
            else
                warnings.Add($"<Claims> ancestral_min_rank '{ancestral}' is not a known rank; kept {config.AncestralMinRank}.");
        }

        var ai = (string?)claims.Attribute("ai");
        if (ai != null)
        {
            if (bool.TryParse(ai, out var enabled))
                config.AiEnabled = enabled;
            else
                warnings.Add($"<Claims> ai '{ai}' is not true or false; kept {config.AiEnabled}.");
        }

        foreach (var rank in claims.Elements("Rank"))
        {
            var value = (string?)rank.Attribute("rank");
            if (value == null || !TryEnum<TitleRank>(value, out var r))
            {
                warnings.Add($"<Claims><Rank rank=\"{value}\"> is not a known rank; ignored.");
                continue;
            }

            config.BuyPrices.Remove(r);
            config.UsurpInfluence.Remove(r);
            var buy = Int(rank, "buy", -1, 1, warnings);
            var usurp = Int(rank, "usurp", -1, 0, warnings);
            if (buy > 0)
                config.BuyPrices[r] = buy;
            if (usurp >= 0)
                config.UsurpInfluence[r] = usurp;
        }
    }

    /// <summary>
    /// &lt;Grants relation revoke_relation&gt; with &lt;Rank rank revoke/&gt; children (a rank listed without revoke cannot
    /// be revoked); &lt;Chronicle entries/&gt;; &lt;Consequences resentment loyalty liege_relation liege_cap liege_rank
    /// army_discount&gt; with &lt;Rank rank tribute/&gt; children (a rank listed without tribute pays none).
    /// </summary>
    private static void ReadFeudal(XElement root, FeudalConfig config, List<string> warnings)
    {
        var grants = root.Element("Grants");
        if (grants != null)
        {
            config.GrantRelation = Int(grants, "relation", config.GrantRelation, -100, warnings);
            config.RevokeRelation = Int(grants, "revoke_relation", config.RevokeRelation, -100, warnings);
            foreach (var rank in grants.Elements("Rank"))
                if (TryRankAttribute(rank, warnings, out var r))
                {
                    config.RevokeInfluence.Remove(r);
                    var revoke = Int(rank, "revoke", -1, 0, warnings);
                    if (revoke >= 0)
                        config.RevokeInfluence[r] = revoke;
                }
        }

        var chronicle = root.Element("Chronicle");
        if (chronicle != null)
            config.ChronicleEntries = Int(chronicle, "entries", config.ChronicleEntries, 1, warnings);

        var consequences = root.Element("Consequences");
        if (consequences == null)
            return;
        config.ResentmentRelation = Int(consequences, "resentment", config.ResentmentRelation, -100, warnings);
        config.ResentmentLoyalty = Int(consequences, "loyalty", config.ResentmentLoyalty, -100, warnings);
        config.LiegeRelation = Int(consequences, "liege_relation", config.LiegeRelation, -100, warnings);
        config.LiegeRelationCap = Int(consequences, "liege_cap", config.LiegeRelationCap, -100, warnings);
        config.ArmyDiscount = Math.Min(1f, Float(consequences, "army_discount", config.ArmyDiscount, warnings));
        var liegeRank = (string?)consequences.Attribute("liege_rank");
        if (liegeRank != null)
        {
            if (TryEnum<TitleRank>(liegeRank, out var r))
                config.LiegeMinRank = r;
            else
                warnings.Add($"<Consequences> liege_rank '{liegeRank}' is not a known rank; kept {config.LiegeMinRank}.");
        }

        // Tribute is a share of fief income set in the mod options; only the rank it starts at comes from the file. The
        // flat <Rank tribute=""/> amounts of older files are ignored.
        var tributeRank = (string?)consequences.Attribute("tribute_rank");
        if (tributeRank != null)
        {
            if (TryEnum<TitleRank>(tributeRank, out var tr))
                config.TributeMinRank = tr;
            else
                warnings.Add($"<Consequences> tribute_rank '{tributeRank}' is not a known rank; kept {config.TributeMinRank}.");
        }
    }

    private static bool TryRankAttribute(XElement element, List<string> warnings, out TitleRank rank)
    {
        var value = (string?)element.Attribute("rank");
        if (value != null && TryEnum(value, out rank))
            return true;
        rank = default;
        warnings.Add($"<{element.Parent?.Name.LocalName}><Rank rank=\"{value}\"> is not a known rank; ignored.");
        return false;
    }

    private static int Int(XElement element, string attribute, int fallback, int min, List<string> warnings)
    {
        var text = (string?)element.Attribute(attribute);
        if (text == null)
            return fallback;
        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n >= min)
            return n;
        warnings.Add($"<{element.Name.LocalName}> {attribute} '{text}' is not a whole number of {min} or more; kept {fallback}.");
        return fallback;
    }

    private static float Float(XElement element, string attribute, float fallback, List<string> warnings)
    {
        var text = (string?)element.Attribute(attribute);
        if (text == null)
            return fallback;
        if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) && n >= 0 && !float.IsInfinity(n))
            return n;
        warnings.Add($"<{element.Name.LocalName}> {attribute} '{text}' is not a number of 0 or more; kept {fallback}.");
        return fallback;
    }

    private static void ReadRankNames(XElement? rankNames, RankNameTable table, List<string> warnings)
    {
        if (rankNames == null)
            return;
        foreach (var rank in rankNames.Elements("Rank"))
            if (TryRank(rank, warnings, out var r, out var name))
                table.SetDefault(r, name);
        foreach (var holder in rankNames.Elements("Holder"))
            if (TryHolder(holder, warnings, out var r, out var male, out var female))
                table.SetHolderDefault(r, male, female);
        foreach (var culture in rankNames.Elements("Culture"))
        {
            var id = Required(culture, "id", warnings);
            var holders = (string?)culture.Attribute("holders");
            if (id != null && holders == "plain")
                table.SetHoldersPlain(id);
            else if (holders != null)
                warnings.Add($"<Culture id=\"{id}\"> holders '{holders}' is not 'plain'; ignored.");
            foreach (var rank in culture.Elements("Rank"))
                if (id != null && TryRank(rank, warnings, out var r, out var name))
                    table.SetForCulture(id, r, name);
            foreach (var holder in culture.Elements("Holder"))
                if (id != null && TryHolder(holder, warnings, out var r, out var male, out var female))
                    table.SetHolderForCulture(id, r, male, female);
        }
    }

    /// <summary>
    /// A &lt;Holder rank="..." male="..." female="..."/&gt;: how the holder of a title of that rank is styled, {NAME} being
    /// the hero's name. female may be left out (female holders then have no style); rank and male are required.
    /// </summary>
    private static bool TryHolder(XElement element, List<string> warnings, out TitleRank rank, out string male, out string? female)
    {
        var maleText = (string?)element.Attribute("male");
        male = maleText ?? string.Empty;
        female = (string?)element.Attribute("female");
        var value = (string?)element.Attribute("rank");
        if (Enum.TryParse(value, out rank) && Enum.IsDefined(typeof(TitleRank), rank) && maleText != null)
            return true;
        warnings.Add($"<Holder rank=\"{value}\" male=\"{male}\"> needs a known rank and a male style (empty for none); ignored.");
        return false;
    }

    /// <summary>
    /// A &lt;Prefix name="..." priority="..." rank="..." title="..." settlement="..." kingdom="..." under="..." culture="..."
    /// holder="..." holder_female="..." known_over="..."/&gt;. holder and holder_female style the holders of the titles it
    /// names, as a &lt;Holder&gt; does (holder_female needs holder); known_over (a rank, needs holder) keeps that style over
    /// the holder's higher titles up to that rank. Null, with a warning, when it has no name, a bad priority or an
    /// unknown rank, or sets no condition (it would rename every title; use &lt;RankNames&gt; for that).
    /// </summary>
    private static TitlePrefixRule? ReadPrefix(XElement element, List<string> warnings)
    {
        var name = Required(element, "name", warnings);
        if (name == null)
            return null;

        var priority = 0;
        var priorityText = (string?)element.Attribute("priority");
        if (priorityText != null && !int.TryParse(priorityText, NumberStyles.Integer, CultureInfo.InvariantCulture, out priority))
        {
            warnings.Add($"<Prefix name=\"{name}\"> priority '{priorityText}' is not a whole number; ignored.");
            return null;
        }

        var ranks = new List<TitleRank>();
        foreach (var value in Words(element, "rank"))
        {
            if (!TryEnum<TitleRank>(value, out var rank))
            {
                warnings.Add($"<Prefix name=\"{name}\"> rank '{value}' is not a known rank; ignored.");
                return null;
            }
            ranks.Add(rank);
        }

        var holder = (string?)element.Attribute("holder");
        var holderFemale = (string?)element.Attribute("holder_female");
        if (holder == null && holderFemale != null)
            warnings.Add($"<Prefix name=\"{name}\"> sets holder_female without holder; holder_female ignored.");
        TitleRank? knownOver = null;
        var knownOverText = (string?)element.Attribute("known_over");
        if (knownOverText != null)
        {
            if (holder == null)
                warnings.Add($"<Prefix name=\"{name}\"> sets known_over without holder; known_over ignored.");
            else if (TryEnum<TitleRank>(knownOverText, out var over))
                knownOver = over;
            else
                warnings.Add($"<Prefix name=\"{name}\"> known_over '{knownOverText}' is not a known rank; known_over ignored.");
        }

        var rule = new TitlePrefixRule(name, priority, ranks,
            Words(element, "title"), Words(element, "settlement"), Words(element, "kingdom"),
            Words(element, "under"), Words(element, "culture"),
            holder, holder != null ? holderFemale : null, knownOver);
        if (!rule.IsUnconditional)
            return rule;
        warnings.Add($"<Prefix name=\"{name}\"> sets no condition and would rename every title; ignored. Use <RankNames> for that.");
        return null;
    }

    /// <summary>A realm_name attribute: the name a kingdom takes while its ruler's highest title is this one.</summary>
    private static void ReadRealmName(XElement element, string titleId, RealmsConfig config)
    {
        var name = (string?)element.Attribute("realm_name");
        if (!string.IsNullOrWhiteSpace(name))
            config.RealmNames[titleId] = name!;
    }

    /// <summary>The settlement ids of a duchy's &lt;County settlement="..."/&gt; or &lt;Barony settlement="..."/&gt; children.</summary>
    private static List<string> Settlements(XElement duchy, string item, List<string> warnings) =>
        duchy.Elements(item).Select(e => Required(e, "settlement", warnings)).Where(s => s != null).Select(s => s!).ToList();

    private static string[] Words(XElement element, string attribute) =>
        ((string?)element.Attribute(attribute) ?? string.Empty).Split(new[] { ' ', '\t', '\r', '\n', ',' }, StringSplitOptions.RemoveEmptyEntries);

    private static bool TryRank(XElement element, List<string> warnings, out TitleRank rank, out string name)
    {
        name = (string?)element.Attribute("name") ?? string.Empty;
        var value = (string?)element.Attribute("rank");
        if (Enum.TryParse(value, out rank) && Enum.IsDefined(typeof(TitleRank), rank) && name.Length > 0)
            return true;
        warnings.Add($"<Rank rank=\"{value}\" name=\"{name}\"> needs a known rank and a name; ignored.");
        return false;
    }

    /// <summary>Laws from succession/gender attributes. Null when the element sets neither.</summary>
    private static TitleLaws? ReadLaws(XElement element, TitleLaws fallback, List<string> warnings)
    {
        var succession = (string?)element.Attribute("succession");
        var gender = (string?)element.Attribute("gender");
        if (succession == null && gender == null)
            return null;
        var s = fallback.Succession;
        var g = fallback.Gender;
        if (succession != null && !TryEnum(succession, out s))
            warnings.Add($"Unknown succession law '{succession}'; kept {fallback.Succession}.");
        if (gender != null && !TryEnum(gender, out g))
            warnings.Add($"Unknown gender law '{gender}'; kept {fallback.Gender}.");
        return new TitleLaws(s, g);
    }

    private static bool TryEnum<T>(string value, out T result) where T : struct =>
        Enum.TryParse(value, ignoreCase: true, out result) && Enum.IsDefined(typeof(T), result);

    private static IEnumerable<XElement> Children(XElement root, string group, string item) =>
        root.Element(group)?.Elements(item) ?? Enumerable.Empty<XElement>();

    private static string? Required(XElement element, string attribute, List<string> warnings)
    {
        var value = (string?)element.Attribute(attribute);
        if (!string.IsNullOrWhiteSpace(value))
            return value;
        warnings.Add($"<{element.Name.LocalName}> without '{attribute}' ignored.");
        return null;
    }
}
