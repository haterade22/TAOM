using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TAOM.Features.Realms.Domain.Titles;

namespace TAOM.Features.Realms.Cheats;

/// <summary>
/// Developer console commands (Alt+~): taom.realms_titles, taom.realms_title, taom.realms_hero. They were realms.titles,
/// realms.title and realms.hero in the separate TAOM.Realms module.
/// </summary>
public static class RealmsConsole
{
    private const string Group = "taom";

    [CommandLineFunctionality.CommandLineArgumentFunction("realms_titles", Group)]
    public static string Titles(List<string> args) =>
        DevConsole.TaomConsole.RunInCampaign(args, "taom.realms_titles [kingdom_id]", TitlesBody);

    [CommandLineFunctionality.CommandLineArgumentFunction("realms_title", Group)]
    public static string Title(List<string> args) =>
        DevConsole.TaomConsole.RunInCampaign(args, "taom.realms_title <title_id | settlement_id>", TitleBody);

    [CommandLineFunctionality.CommandLineArgumentFunction("realms_hero", Group)]
    public static string HeroTitles(List<string> args) =>
        DevConsole.TaomConsole.RunInCampaign(args, "taom.realms_hero [hero_id]", HeroBody);

    private static string TitlesBody(List<string> args)
    {
        if (!TryGet(out var behavior, out var error))
            return error;
        var catalog = behavior.Service.Catalog;
        var kingdomId = args.FirstOrDefault();
        var roots = catalog.All
            .Where(t => kingdomId == null ? t.ParentId == null : t.Rank == TitleRank.Kingdom && t.KingdomId == kingdomId)
            .ToList();
        if (roots.Count == 0)
            return kingdomId == null ? "No titles." : $"No kingdom title for '{kingdomId}'. Usage: taom.realms_titles [kingdom_id]";

        var sb = new StringBuilder();
        foreach (var root in roots)
            AppendTree(sb, behavior, root, 0, kingdomId == null ? TitleRank.Kingdom : TitleRank.Barony);
        sb.Append($"{catalog.Count} titles in total.");
        return sb.ToString();
    }

    private static string TitleBody(List<string> args)
    {
        if (!TryGet(out var behavior, out var error))
            return error;
        if (args.Count == 0)
            return "Usage: taom.realms_title <title_id | settlement_id>";
        var catalog = behavior.Service.Catalog;
        var title = catalog.Get(args[0]) ?? catalog.ForSettlement(args[0]);
        if (title == null)
            return $"No title or settlement '{args[0]}'.";

        var ledger = behavior.Service.Ledger;
        var sb = new StringBuilder();
        sb.AppendLine($"{TitleNames.Of(title, behavior)} [{title.Id}] {title.Rank}");
        sb.AppendLine($"  holder: {TitleNames.Hero(ledger.HolderOf(title.Id))}");
        sb.AppendLine($"  laws: {behavior.Service.EffectiveLaws(title.Id)}");
        var liege = title.ParentId != null ? catalog.Get(title.ParentId) : null;
        sb.AppendLine($"  de jure liege: {(liege != null ? TitleNames.Of(liege, behavior) + " [" + liege.Id + "]" : "-")}");
        foreach (var claim in ledger.ClaimsOn(title.Id))
            sb.AppendLine($"  claim: {TitleNames.Hero(claim.HeroId)} ({claim.Kind}, day {claim.CampaignDay:0})");
        foreach (var pending in ledger.Pending.Where(p => p.TitleId == title.Id))
            sb.AppendLine($"  waiting for: {pending.Kind} of {pending.ReferenceId}");
        sb.Append($"  vassal titles: {catalog.ChildrenOf(title.Id).Count}");
        return sb.ToString();
    }

    private static string HeroBody(List<string> args)
    {
        if (!TryGet(out var behavior, out var error))
            return error;
        var heroId = args.Count > 0 ? args[0] : Hero.MainHero?.StringId;
        if (heroId == null)
            return "Usage: taom.realms_hero [hero_id]";

        var service = behavior.Service;
        var sb = new StringBuilder();
        sb.AppendLine($"{TitleNames.Hero(heroId)} [{heroId}]");
        var held = service.Ledger.TitlesHeldBy(heroId)
            .Select(id => service.Catalog.Get(id)).Where(t => t != null).Select(t => t!)
            .OrderByDescending(t => t.Rank).ToList();
        foreach (var t in held)
            sb.AppendLine($"  holds: {TitleNames.Of(t, behavior)} [{t.Id}]");
        foreach (var claim in service.Ledger.ClaimsOf(heroId))
            sb.AppendLine($"  claims: [{claim.TitleId}] ({claim.Kind})");
        sb.Append($"{held.Count} titles held.");
        return sb.ToString();
    }

    private static void AppendTree(StringBuilder sb, RealmsCampaign behavior, TitleDefinition title, int depth, TitleRank lowest)
    {
        var holder = behavior.Service.Ledger.HolderOf(title.Id);
        sb.Append(' ', depth * 2).AppendLine($"{TitleNames.Of(title, behavior)} [{title.Id}]: {TitleNames.Hero(holder)}");
        if (title.Rank <= lowest)
            return;
        foreach (var child in behavior.Service.Catalog.ChildrenOf(title.Id))
            AppendTree(sb, behavior, child, depth + 1, lowest);
    }

    private static bool TryGet(out RealmsCampaign behavior, out string error)
    {
        behavior = null!;
        error = string.Empty;
        var current = RealmsCampaign.Current;
        if (current == null)
        {
            error = "No campaign is running.";
            return false;
        }
        if (!current.EnsureInitialized())
        {
            error = "Titles are off: this save holds title data this version cannot read.";
            return false;
        }
        behavior = current;
        return true;
    }
}
