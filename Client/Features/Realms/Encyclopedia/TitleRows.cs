using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Features.Realms;
using TAOM.Features.Realms.Domain.Titles;
using TAOM.Client.Features.Realms.Popups;

namespace TAOM.Client.Features.Realms.Encyclopedia;

/// <summary>
/// One line of a title section: a muted label (empty on the follow-on lines of a group) and a value that, when it is a link,
/// opens a popup or another encyclopedia page. The action runs on the next tick, outside the page's own click handling.
/// </summary>
internal sealed class TitleRowVM : ViewModel
{
    private readonly Action? _open;

    public TitleRowVM(string label, string value, Action? open)
    {
        LabelText = label;
        ValueText = value;
        _open = open;
    }

    [DataSourceProperty] public string LabelText { get; }
    [DataSourceProperty] public string ValueText { get; }
    [DataSourceProperty] public bool IsLink => _open != null;

    public void ExecuteClick() => UiQueue.Post(_open);
}

/// <summary>Builds the rows of the title sections. No laws are shown on the encyclopedia: they only drive succession.</summary>
internal static class TitleRows
{
    private const int ShownVassals = 8;
    private const int ShownHeld = 8;

    /// <summary>
    /// Rows for one title: who holds it by right when that is not the fief owner or ruler, liege, claims, the player's claim
    /// actions, vassals. <paramref name="refresh"/> rebuilds the section after an action.
    /// </summary>
    public static void ForTitle(MBBindingList<TitleRowVM> rows, RealmsCampaign b, TitleDefinition title, Action? refresh)
    {
        rows.Clear();
        var catalog = b.Service.Catalog;
        var holder = b.Service.Ledger.HolderOf(title.Id);
        var actual = TitleText.ActualRuler(title);
        if (actual.Known && actual.HeroId != holder)
            rows.Add(new TitleRowVM(Text("{=realms_ui_holder}Held by right"), TitleText.HolderName(b, title), HeroPage(holder)));

        var liege = catalog.Get(title.ParentId);
        if (liege != null)
            rows.Add(new TitleRowVM(Text("{=realms_ui_liege}Liege title"), TitleNames.Of(liege, b), Page(liege.Id)));

        var claims = b.Service.Ledger.ClaimsOn(title.Id).Count;
        rows.Add(claims == 0
            ? new TitleRowVM(Text("{=realms_ui_claims}Claims"), Text("{=realms_ui_none_cap}None"), null)
            : new TitleRowVM(Text("{=realms_ui_claims}Claims"), Count(claims == 1
                ? "{=realms_enc_claimant}1 claimant"
                : "{=realms_enc_claimants}{COUNT} claimants", claims), () => RealmsPopups.OpenClaimants(title.Id)));

        // "Buy a claim: Pay 80 Castar" as a link that asks first; a refused action shows its reason instead.
        foreach (var option in ClaimActions.For(b, title, refresh, null))
            rows.Add(new TitleRowVM(option.Label, ActionValue(option), option.Enabled ? option.Run : null));

        var vassals = TitleText.Sorted(b, catalog.ChildrenOf(title.Id));
        AddGroup(rows, Text("{=realms_ui_vassals}Vassal titles"),
            vassals.Take(ShownVassals).Select(v => (TitleNames.Of(v, b), (Action?)Page(v.Id))).ToList());
        if (vassals.Count > ShownVassals)
            rows.Add(new TitleRowVM(string.Empty, Count("{=realms_enc_more}and {COUNT} more", vassals.Count - ShownVassals), Tree(title.Id)));

        // The realm's officers ("Steward of Gondor: Denethor"), each opening the holder's page.
        if (title.Rank == TitleRank.Kingdom && title.KingdomId != null)
            foreach (var office in b.Service.OfficesOfRealm(title.KingdomId))
            {
                var officer = b.Service.Ledger.OfficeHolderOf(office.Id);
                rows.Add(new TitleRowVM(new TextObject(office.Name).ToString(), TitleText.HolderName(officer), HeroPage(officer)));
            }

        // The last holders, newest first.
        AddGroup(rows, Text("{=realms_ui_chronicle}Chronicle"),
            TitleText.Recent(b, title.Id, TitleText.ShownChronicle)
                .Select(e => (TitleText.ChronicleLine(e), HeroPage(e.HeroId)))
                .ToList());
    }

    /// <summary>Rows for a hero: the titles held by right, highest first, then the titles claimed. Each opens its title page.</summary>
    public static bool ForHero(MBBindingList<TitleRowVM> rows, RealmsCampaign b, Hero hero)
    {
        rows.Clear();
        var catalog = b.Service.Catalog;
        var ledger = b.Service.Ledger;
        var held = TitleText.Sorted(b, ledger.TitlesHeldBy(hero.StringId).Select(id => catalog.Get(id)));

        // The hero as known by their title or office, when it gives a style ("Lord Forlong of Lossarnach",
        // "Steward Denethor of Gondor").
        var primary = TitleNames.KnownBy(hero, b);
        var styled = TitleNames.KnownAs(hero, b);
        if (styled != null)
            rows.Add(new TitleRowVM(Text("{=realms_enc_style}Style"), styled.ToString(), primary != null ? Page(primary.Id) : null));

        var offices = b.Service.OfficesOf(hero.StringId);
        AddGroup(rows, Text("{=realms_enc_offices}Offices"),
            offices.Select(o => (new TextObject(o.Name).ToString(), RealmPage(b, o.KingdomId))).ToList());

        AddGroup(rows, Text("{=realms_enc_holds}Holds"),
            held.Take(ShownHeld).Select(t => (TitleNames.Of(t, b), (Action?)Page(t.Id))).ToList());
        if (held.Count > ShownHeld)
            rows.Add(new TitleRowVM(string.Empty, Count("{=realms_enc_more}and {COUNT} more", held.Count - ShownHeld), null));

        var claimed = ledger.ClaimsOf(hero.StringId)
            .Select(c => (Title: catalog.Get(c.TitleId), c.Kind))
            .Where(c => c.Title != null)
            .OrderByDescending(c => c.Title!.Rank)
            .Select(c => ($"{TitleNames.Of(c.Title!, b)} ({ClaimKindText(c.Kind)})", (Action?)Page(c.Title!.Id)))
            .ToList();
        AddGroup(rows, Text("{=realms_ui_claims}Claims"), claimed);
        return held.Count > 0 || claimed.Count > 0 || offices.Count > 0;
    }

    public static Action Tree(string titleId) => () => RealmsPopups.OpenTree(titleId);

    /// <summary>Opens the title's own encyclopedia page.</summary>
    public static Action Page(string titleId) => () => TitleVisuals.GoTo(RealmsTitle.LinkOf(titleId));

    /// <summary>Opens the page of the realm's title, when the realm has one.</summary>
    private static Action? RealmPage(RealmsCampaign b, string kingdomId)
    {
        var realm = b.Service.Catalog.All.FirstOrDefault(t => t.Rank == TitleRank.Kingdom && t.KingdomId == kingdomId);
        return realm != null ? Page(realm.Id) : null;
    }

    /// <summary>"Pay 80 Castar", "80 Castar: not near your lands", the bare reason, or nothing for a free action.</summary>
    internal static string ActionValue(ClaimOption option)
    {
        TextObject text;
        if (option.Enabled && option.Cost.Length == 0)
            return string.Empty;
        if (option.Enabled)
            text = new TextObject("{=realms_enc_pay}Pay {COST}");
        else if (option.Cost.Length > 0)
            text = new TextObject("{=realms_enc_cost_refused}{COST}: {REASON}");
        else
            return option.Reason;
        text.SetTextVariable("COST", option.Cost);
        text.SetTextVariable("REASON", option.Reason);
        return text.ToString();
    }

    private static void AddGroup(MBBindingList<TitleRowVM> rows, string label, IReadOnlyList<(string Value, Action? Open)> items)
    {
        for (var i = 0; i < items.Count; i++)
            rows.Add(new TitleRowVM(i == 0 ? label : string.Empty, items[i].Value, items[i].Open));
    }

    private static Action? HeroPage(string? heroId)
    {
        var link = heroId != null ? Hero.Find(heroId)?.EncyclopediaLink : null;
        return link != null ? () => TitleVisuals.GoTo(link) : null;
    }

    private static string ClaimKindText(global::TAOM.Features.Realms.Domain.Ledger.ClaimKind kind) => Text(kind switch
    {
        global::TAOM.Features.Realms.Domain.Ledger.ClaimKind.Conquest => "{=realms_claim_conquest_short}conquest",
        global::TAOM.Features.Realms.Domain.Ledger.ClaimKind.Fabricated => "{=realms_claim_fabricated_short}fabricated",
        _ => "{=realms_claim_ancestral_short}ancestral",
    });

    private static string Text(string id) => new TextObject(id).ToString();

    private static string Count(string template, int count)
    {
        var text = new TextObject(template);
        text.SetTextVariable("COUNT", count);
        return text.ToString();
    }
}
