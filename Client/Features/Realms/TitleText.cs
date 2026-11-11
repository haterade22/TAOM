using System.Collections.Generic;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;
using TAOM.Features.Realms;
using TAOM.Features.Realms.Domain.Laws;
using TAOM.Features.Realms.Domain.Ledger;
using TAOM.Features.Realms.Domain.Services;
using TAOM.Features.Realms.Domain.Titles;

namespace TAOM.Client.Features.Realms;

/// <summary>Builds the readable text shown in the titles window.</summary>
internal static class TitleText
{
    public static string Label(RealmsCampaign b, TitleDefinition title) =>
        $"{TitleNames.Of(title, b)}:  {HolderName(b, title)}";

    public static string Hint(RealmsCampaign b, TitleDefinition title) =>
        $"{Rank(title.Rank)}. {Laws(b.Service.EffectiveLaws(title.Id))}";

    public static string HolderName(string? heroId) => MarkYou(heroId, TitleNames.Hero(heroId));

    /// <summary>The title's holder by right, styled by the title ("Aran Gil-galad"), marked when it is the player.</summary>
    public static string HolderName(RealmsCampaign b, TitleDefinition title)
    {
        var heroId = b.Service.Ledger.HolderOf(title.Id);
        return MarkYou(heroId, TitleNames.Styled(title, heroId, b));
    }

    private static string MarkYou(string? heroId, string name) =>
        heroId != null && heroId == Hero.MainHero?.StringId
            ? name + " " + new TextObject("{=realms_ui_you}(you)")
            : name;

    public static string Details(RealmsCampaign b, TitleDefinition title)
    {
        var ledger = b.Service.Ledger;
        var holder = ledger.HolderOf(title.Id);
        var sb = new StringBuilder();
        sb.AppendLine(Rank(title.Rank));
        sb.AppendLine(Line("{=realms_ui_holder}Held by right", HolderName(b, title)));
        var liege = title.ParentId != null ? b.Service.Catalog.Get(title.ParentId) : null;
        if (liege != null)
            sb.AppendLine(Line("{=realms_ui_liege}Liege title", TitleNames.Of(liege, b)));

        var actual = ActualRuler(title);
        if (actual.Known)
        {
            var note = actual.HeroId == holder ? string.Empty : " " + new TextObject("{=realms_ui_without_title}(without the title)");
            sb.AppendLine(Line(title.SettlementId != null ? "{=realms_ui_owner}Fief owned by" : "{=realms_ui_ruler}Realm ruled by",
                HolderName(actual.HeroId) + note));
        }

        sb.AppendLine(Line("{=realms_ui_laws}Laws", Laws(b.Service.EffectiveLaws(title.Id))));

        var claims = ledger.ClaimsOn(title.Id);
        sb.AppendLine(Line("{=realms_ui_claims}Claims", claims.Count == 0
            ? new TextObject("{=realms_ui_none}none").ToString()
            : string.Join(", ", claims.Select(c => $"{HolderName(c.HeroId)} ({c.Kind.ToString().ToLowerInvariant()})"))));

        foreach (var pending in ledger.Pending.Where(p => p.TitleId == title.Id))
            sb.AppendLine(Line("{=realms_ui_waiting}Waiting for", pending.Kind == PendingKind.RealmRuler
                ? new TextObject("{=realms_ui_wait_ruler}the realm to choose a ruler").ToString()
                : new TextObject("{=realms_ui_wait_clan}the clan to choose a leader").ToString()));

        foreach (var (office, officeHolder) in Offices(b, title))
            sb.AppendLine(Line(office, officeHolder));

        var vassals = b.Service.Catalog.ChildrenOf(title.Id).Count;
        if (vassals > 0)
            sb.AppendLine(Line("{=realms_ui_vassals}Vassal titles", vassals.ToString()));

        var history = Recent(b, title.Id, ShownChronicle);
        if (history.Count > 0)
        {
            sb.AppendLine(new TextObject("{=realms_ui_chronicle}Chronicle").ToString() + ":");
            foreach (var entry in history)
                sb.AppendLine("  " + ChronicleLine(entry));
        }
        return sb.ToString().TrimEnd();
    }

    /// <summary>How many chronicle entries the encyclopedia and the titles window show.</summary>
    public const int ShownChronicle = 5;

    /// <summary>The offices of a realm's title with their holders ("Steward of Gondor", "Denethor"), empty for other titles.</summary>
    public static IReadOnlyList<(string Office, string Holder)> Offices(RealmsCampaign b, TitleDefinition title)
    {
        if (title.Rank != TitleRank.Kingdom || title.KingdomId == null)
            return new List<(string, string)>();
        return b.Service.OfficesOfRealm(title.KingdomId)
            .Select(o => (new TextObject(o.Name).ToString(), HolderName(b.Service.Ledger.OfficeHolderOf(o.Id))))
            .ToList();
    }

    /// <summary>The last changes of holder of a title or office, newest first.</summary>
    public static IReadOnlyList<ChronicleEntry> Recent(RealmsCampaign b, string id, int count) =>
        b.Service.Ledger.ChronicleOf(id).Reverse().Take(count).ToList();

    /// <summary>"12 Spring 1012: Denethor (inherited)".</summary>
    public static string ChronicleLine(ChronicleEntry entry) =>
        $"{Date(entry.CampaignDay)}: {HolderName(entry.HeroId)} ({Reason(entry.Reason)})";

    public static string Date(double campaignDay) => CampaignTime.Days((float)campaignDay).ToString();

    public static string Reason(TitleEventReason reason) => new TextObject(reason switch
    {
        TitleEventReason.Inheritance => "{=realms_why_inherited}inherited",
        TitleEventReason.ClanSuccession => "{=realms_why_clan}new clan leader",
        TitleEventReason.RealmSuccession => "{=realms_why_ruler}new ruler",
        TitleEventReason.Transfer => "{=realms_why_transfer}handed over",
        TitleEventReason.Possession => "{=realms_why_possession}took up",
        TitleEventReason.Vacated => "{=realms_why_vacated}fell vacant",
        TitleEventReason.Conquest => "{=realms_why_conquest}conquered",
        TitleEventReason.Grant => "{=realms_why_grant}granted",
        TitleEventReason.Usurpation => "{=realms_why_usurped}usurped",
        TitleEventReason.Claim => "{=realms_why_claim}by claim",
        TitleEventReason.Start => "{=realms_why_start}first holder",
        TitleEventReason.Formation => "{=realms_why_formed}formed",
        TitleEventReason.Restoration => "{=realms_why_restored}restored",
        TitleEventReason.Revocation => "{=realms_why_revoked}revoked",
        TitleEventReason.Appointment => "{=realms_why_appointed}appointed",
        TitleEventReason.Dismissal => "{=realms_why_dismissed}left office",
        _ => "{=realms_why_other}changed",
    }).ToString();

    public static string Laws(TitleLaws laws)
    {
        var succession = laws.Succession switch
        {
            SuccessionLaw.Primogeniture => "{=realms_law_primo}eldest child inherits",
            SuccessionLaw.Ultimogeniture => "{=realms_law_ultimo}youngest child inherits",
            SuccessionLaw.Seniority => "{=realms_law_seniority}eldest of the clan inherits",
            SuccessionLaw.ClanLeader => "{=realms_law_clan}passes to the clan leader",
            _ => "{=realms_law_elective}passes to the chosen ruler",
        };
        var gender = laws.Gender switch
        {
            GenderLaw.Agnatic => "{=realms_law_agnatic}men only",
            GenderLaw.AgnaticCognatic => "{=realms_law_agnatic_cognatic}men first",
            _ => "{=realms_law_absolute}men and women equally",
        };
        return $"{new TextObject(succession)}, {new TextObject(gender)}";
    }

    public static string Rank(TitleRank rank) => new TextObject(rank switch
    {
        TitleRank.Lordship => "{=realms_ui_rank_lordship}Lordship (village)",
        TitleRank.Barony => "{=realms_ui_rank_barony}Barony (castle)",
        TitleRank.County => "{=realms_ui_rank_county}County (town)",
        TitleRank.Duchy => "{=realms_ui_rank_duchy}Duchy",
        TitleRank.Kingdom => "{=realms_ui_rank_kingdom}Kingdom",
        TitleRank.Union => "{=realms_ui_rank_union}Union of realms",
        _ =>"{=realms_ui_rank_empire}High title",
    }).ToString();

    public static IReadOnlyList<TitleDefinition> Sorted(RealmsCampaign b, IEnumerable<TitleDefinition?> titles) =>
        titles.Where(t => t != null).Select(t => t!)
            .OrderByDescending(t => t.Rank)
            .ThenBy(t => TitleNames.Of(t, b), System.StringComparer.CurrentCulture)
            .ToList();

    private static string Line(string label, string value) => $"{new TextObject(label)}: {value}";

    /// <summary>Who actually has the land now: the fief's owner, or the realm's ruler.</summary>
    public static (bool Known, string? HeroId) ActualRuler(TitleDefinition title)
    {
        var objects = TaleWorlds.CampaignSystem.Campaign.Current?.CampaignObjectManager;
        if (objects == null)
            return (false, null);
        if (title.SettlementId != null)
        {
            var settlement = objects.Find<Settlement>(title.SettlementId);
            if (settlement == null)
                return (false, null);
            var landlord = settlement.IsVillage ? settlement.Village?.Bound : settlement;
            return (true, landlord?.Owner?.StringId);
        }
        if (title.Rank == TitleRank.Kingdom && title.KingdomId != null)
        {
            var kingdom = objects.Find<Kingdom>(title.KingdomId);
            return kingdom == null ? (false, null) : (true, kingdom.Leader?.StringId);
        }
        return (false, null);
    }
}
