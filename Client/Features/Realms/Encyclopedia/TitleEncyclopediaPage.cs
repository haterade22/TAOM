using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encyclopedia;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;
using TAOM.Features.Realms;
using TAOM.Features.Realms.Domain.Titles;

namespace TAOM.Client.Features.Realms.Encyclopedia;

/// <summary>
/// A title of the catalog as an encyclopedia object: the entry of the Titles list and the object of its page
/// (<see cref="TitleEncyclopediaPageVM"/>). It is never registered with the object manager nor saved. One instance per
/// title id and campaign (<see cref="Of"/>): the page's previous and next buttons find it in the list by reference.
/// </summary>
public sealed class RealmsTitle : MBObjectBase
{
    private static readonly Dictionary<string, RealmsTitle> Cache = new();
    private static object? _cacheCampaign;

    private RealmsTitle(string id) : base(id) { }

    /// <summary>The title's definition in the current catalog; null once the title is gone.</summary>
    internal TitleDefinition? Definition => TitleEncyclopediaPage.Behavior()?.Service.Catalog.Get(StringId);

    /// <summary>The encyclopedia link of a title's page.</summary>
    internal static string LinkOf(string titleId) => $"{nameof(RealmsTitle)}-{titleId}";

    /// <summary>The one object for <paramref name="titleId"/> in this campaign, null for an unknown title.</summary>
    internal static RealmsTitle? Of(string? titleId)
    {
        var b = TitleEncyclopediaPage.Behavior();
        if (titleId == null || b?.Service.Catalog.Get(titleId) == null)
            return null;
        var campaign = TaleWorlds.CampaignSystem.Campaign.Current;
        if (!ReferenceEquals(campaign, _cacheCampaign))
        {
            Cache.Clear();
            _cacheCampaign = campaign;
        }
        if (!Cache.TryGetValue(titleId, out var title))
            Cache[titleId] = title = new RealmsTitle(titleId);
        return title;
    }

    public override TextObject GetName() =>
        Definition is { } title && TitleEncyclopediaPage.Behavior() is { } b
            ? new TextObject(TitleNames.Of(title, b))
            : new TextObject(StringId);

    // MBObjectBase hashes its MBGUID, which is zero for every unregistered object.
    public override int GetHashCode() => StringId.GetHashCode();

    public override string ToString() => StringId;
}

/// <summary>
/// The Titles category of the encyclopedia: every title of the catalog, filtered by rank, by the player's clan and realm and
/// by status, sorted by rank or claims. Every title, fief title or not, opens its own page (RealmsEncyclopediaTitlePage.xml):
/// holder, seat, liege and vassal titles, claims and the player's title actions.
/// </summary>
public sealed class TitleEncyclopediaPage : EncyclopediaPage
{
    private static bool _patched;

    public TitleEncyclopediaPage()
    {
        HomePageOrderIndex = 550;
    }

    public override string GetStringID() => "EncyclopediaRealmsTitle";

    public override TextObject GetName() => new("{=realms_ency_titles}Titles");

    public override TextObject GetDescriptionText() =>
        new("{=realms_ency_titles_desc}Every title of the realms, from the village lordships to the high crowns: who holds each by right and who claims it.");

    public override string GetViewFullyQualifiedName() => "RealmsEncyclopediaTitlePage";

    /// <summary>
    /// The encyclopedia home keeps only the pages relevant when the map screen builds it (GauntletMapEncyclopediaView,
    /// once per campaign session), and a new campaign gets its titles on the first hourly tick, after that. So the page
    /// is relevant while Realms runs, titles or not; its list reads the titles each time it opens.
    /// </summary>
    public override bool IsRelevant() => RealmsCampaign.Current is { IsDisabled: false };

    public override bool IsValidEncyclopediaItem(object o) => o is RealmsTitle { Definition: not null };

    public override MBObjectBase GetObject(string typeName, string stringID) => RealmsTitle.Of(stringID)!;

    // Re-run each time a list is shown, so it reflects the titles, holders and names of the moment.
    protected override IEnumerable<EncyclopediaListItem> InitializeListItems()
    {
        var b = Behavior();
        if (b == null)
            yield break;
        var ownType = GetIdentifier(typeof(RealmsTitle));
        foreach (var title in b.Service.Catalog.All)
        {
            if (RealmsTitle.Of(title.Id) is not { } item)
                continue;
            yield return new EncyclopediaListItem(item, TitleNames.Of(title, b),
                TitleText.HolderName(b, title), title.Id, ownType, playerCanSeeValues: true);
        }
    }

    // A list, not an iterator: the filter items keep IsActive between visits.
    protected override IEnumerable<EncyclopediaFilterGroup> InitializeFilterItems()
    {
        var rank = new List<EncyclopediaFilterItem>
        {
            Filter("{=realms_ency_f_high}Realms and high titles", t => t.Rank >= TitleRank.Kingdom),
            Filter("{=realms_ency_f_duchy}Duchies", t => t.Rank == TitleRank.Duchy),
            Filter("{=realms_ency_f_county}Counties (towns)", t => t.Rank == TitleRank.County),
            Filter("{=realms_ency_f_barony}Baronies (castles)", t => t.Rank == TitleRank.Barony),
            Filter("{=realms_ency_f_lordship}Lordships (villages)", t => t.Rank == TitleRank.Lordship),
        };
        var yours = new List<EncyclopediaFilterItem>
        {
            Filter("{=realms_ency_f_held}Held by your clan", HeldByPlayerClan),
            Filter("{=realms_ency_f_claimed}Claimed by your clan", ClaimedByPlayerClan),
            Filter("{=realms_ency_f_realm}In your realm", InPlayerRealm),
        };
        var status = new List<EncyclopediaFilterItem>
        {
            Filter("{=realms_ency_f_vacant}Vacant", t => WithBehavior(b => b.Service.Ledger.HolderOf(t.Id) == null)),
            Filter("{=realms_ency_f_has_claims}Claimed", t => WithBehavior(b => b.Service.Ledger.ClaimsOn(t.Id).Count > 0)),
        };
        return new List<EncyclopediaFilterGroup>
        {
            new(rank, new TextObject("{=realms_ency_g_rank}Rank")),
            new(yours, new TextObject("{=realms_ency_g_yours}Yours")),
            new(status, new TextObject("{=realms_ency_g_status}Status")),
        };
    }

    protected override IEnumerable<EncyclopediaSortController> InitializeSortControllers() => new List<EncyclopediaSortController>
    {
        new(new TextObject("{=realms_ency_s_rank}Rank"), new RankComparer()),
        new(new TextObject("{=realms_ency_s_claims}Claims"), new ClaimsComparer()),
    };

    /// <summary>
    /// Adds the page to the encyclopedia. The game finds its pages by an attribute this build keeps internal, so the page
    /// is added to the manager's pages after it builds them, and its target type (which the base constructor reads from
    /// that attribute) is given by a constructor prefix. Without either patch the encyclopedia has no Titles page.
    /// </summary>
    internal static void Patch()
    {
        if (_patched)
            return;
        _patched = true;
        try
        {
            var harmony = new Harmony("TAOM.Realms.TitlePage");
            _identifierTypes = AccessTools.Field(typeof(EncyclopediaPage), "_identifierTypes");
            _pages = AccessTools.Field(typeof(EncyclopediaManager), "_pages");
            var constructor = AccessTools.Constructor(typeof(EncyclopediaPage), Type.EmptyTypes);
            var create = AccessTools.Method(typeof(EncyclopediaManager), nameof(EncyclopediaManager.CreateEncyclopediaPages));
            if (_identifierTypes == null || _pages == null || constructor == null || create == null)
            {
                Debug.Print("[TAOM.Realms] the encyclopedia page internals were not found; the encyclopedia has no Titles page.");
                return;
            }
            harmony.Patch(constructor, prefix: new HarmonyMethod(typeof(TitleEncyclopediaPage), nameof(PagePrefix)));
            harmony.Patch(create, postfix: new HarmonyMethod(typeof(TitleEncyclopediaPage), nameof(CreatePagesPostfix)));
            _canAdd = true;
        }
        catch (Exception ex)
        {
            Debug.Print($"[TAOM.Realms] the encyclopedia Titles page is off: {ex}");
        }
    }

    private static System.Reflection.FieldInfo? _identifierTypes;
    private static System.Reflection.FieldInfo? _pages;
    private static bool _canAdd;

    // Runs before the base constructor's body, which keeps the value when it finds no attribute of its own.
    private static void PagePrefix(EncyclopediaPage __instance)
    {
        if (__instance is TitleEncyclopediaPage)
            _identifierTypes?.SetValue(__instance, new[] { typeof(RealmsTitle) });
    }

    private static void CreatePagesPostfix(EncyclopediaManager __instance)
    {
        if (!_canAdd)
            return;
        try
        {
            if (_pages?.GetValue(__instance) is Dictionary<Type, EncyclopediaPage> pages && !pages.ContainsKey(typeof(RealmsTitle)))
                pages.Add(typeof(RealmsTitle), new TitleEncyclopediaPage());
        }
        catch (Exception ex)
        {
            Debug.Print($"[TAOM.Realms] adding the encyclopedia Titles page threw: {ex}");
        }
    }

    private static bool HeldByPlayerClan(TitleDefinition title) => WithBehavior(b =>
    {
        var holder = b.Service.Ledger.HolderOf(title.Id);
        return holder != null && IsPlayerClan(holder);
    });

    private static bool ClaimedByPlayerClan(TitleDefinition title) =>
        WithBehavior(b => b.Service.Ledger.ClaimsOn(title.Id).Any(c => IsPlayerClan(c.HeroId)));

    /// <summary>The fief is the player's realm's, or (for a title without a fief) it or a liege title is the realm's title.</summary>
    private static bool InPlayerRealm(TitleDefinition title) => WithBehavior(b =>
    {
        var kingdom = Clan.PlayerClan?.Kingdom;
        if (kingdom == null)
            return false;
        if (title.SettlementId != null && Settlement.Find(title.SettlementId) is { } fief)
            return fief.MapFaction == kingdom;
        return title.KingdomId == kingdom.StringId
            || b.Service.Catalog.AncestorsOf(title.Id).Any(t => t.KingdomId == kingdom.StringId);
    });

    private static bool IsPlayerClan(string heroId)
    {
        var clan = Clan.PlayerClan;
        return clan != null && TaleWorlds.CampaignSystem.Hero.Find(heroId)?.Clan == clan;
    }

    private static EncyclopediaFilterItem Filter(string name, Func<TitleDefinition, bool> predicate) =>
        new(new TextObject(name), o => o is RealmsTitle { Definition: { } t } && predicate(t));

    private static bool WithBehavior(Func<RealmsCampaign, bool> test)
    {
        var b = Behavior();
        return b != null && test(b);
    }

    /// <summary>The running ledger with its titles; null before the first hourly tick, on a disabled one and off campaign.</summary>
    internal static RealmsCampaign? Behavior()
    {
        var b = RealmsCampaign.Current;
        return b != null && !b.IsDisabled && b.Service.IsInitialized ? b : null;
    }

    private static TitleDefinition? Of(EncyclopediaListItem item) => (item.Object as RealmsTitle)?.Definition;

    /// <summary>Highest rank first, then by name.</summary>
    private sealed class RankComparer : EncyclopediaListItemComparerBase
    {
        public override int Compare(EncyclopediaListItem x, EncyclopediaListItem y)
        {
            var order = (Of(x)?.Rank ?? 0).CompareTo(Of(y)?.Rank ?? 0) * (IsAscending ? 1 : -1);
            return order != 0 ? order : ResolveEquality(x, y);
        }

        public override string GetComparedValueText(EncyclopediaListItem item) =>
            Of(item) is { } title ? TitleText.Rank(title.Rank) : _missingValue.ToString();
    }

    /// <summary>Most claimants first, then by name.</summary>
    private sealed class ClaimsComparer : EncyclopediaListItemComparerBase
    {
        public override int Compare(EncyclopediaListItem x, EncyclopediaListItem y)
        {
            var order = Claims(x).CompareTo(Claims(y)) * (IsAscending ? 1 : -1);
            return order != 0 ? order : ResolveEquality(x, y);
        }

        public override string GetComparedValueText(EncyclopediaListItem item)
        {
            var claims = Claims(item);
            return claims > 0 ? claims.ToString() : _emptyValue.ToString();
        }

        private static int Claims(EncyclopediaListItem item) =>
            Of(item) is { } title && Behavior() is { } b ? b.Service.Ledger.ClaimsOn(title.Id).Count : 0;
    }
}
