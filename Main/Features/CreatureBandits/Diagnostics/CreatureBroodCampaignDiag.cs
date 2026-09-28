using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using static TAOM.Features.CreatureBandits.Diagnostics.CreatureDiagFormat;

namespace TAOM.Features.CreatureBandits.Diagnostics;

/// <summary>
/// Campaign-map diagnostics for the spider broods (#692): the brood spawner's session, census, spawn and stray lines,
/// and the map icon, the no-parley encounter and prisoner refusals, each counted for the whole session and logged
/// first-N. Every caller is main-thread campaign code (the map view, the encounter menu init, MapEvent's loot pass;
/// campaign ticks are sequential). Session-scoped: the brood spawner resets it at session launch. A line never breaks
/// the campaign tick it reports on (<see cref="Guard"/>). Temporary, strip after sign-off.
/// </summary>
internal static class CreatureBroodCampaignDiag
{
    private const int MapIconPartyCap = 30;
    private const int PrisonerLineCap = 10;

    private static readonly HashSet<string> MapIconParties = new();
    internal static long MapIconRiderSkips, NoParleyFired, PrisonersRefused;

    internal static double Day => Campaign.Current == null ? double.NaN : CampaignTime.Now.ToDays;

    /// <summary>The session line: the clan, its template's stacks, the anchors found on this map and the looter cap.</summary>
    internal static void Session(Clan? clan) => Guard("session", () =>
    {
        var template = clan?.DefaultPartyTemplate;
        string stacks = template?.Stacks == null ? "-" : string.Join(",", template.Stacks.Select(st =>
            (st.Character?.StringId ?? "?") + ":" + I(st.MinValue) + "-" + I(st.MaxValue)));
        var missing = CreatureBanditsConfig.BroodAnchorSettlementIds.Where(id => Settlement.Find(id) == null).ToList();
        CreatureBanditDiag.Logger?.LogInfo(CampaignLine("campaign-start", Day, "clan", B(clan != null), "template", template?.StringId ?? "-",
            "stacks", stacks, "anchors", I(CreatureBanditsConfig.BroodAnchorSettlementIds.Count - missing.Count),
            "missingAnchors", missing.Count == 0 ? "-" : string.Join(",", missing),
            "looterCap", clan == null ? "-" : I(Campaign.Current.Models.BanditDensityModel.GetMaxSupportedNumberOfLootersForClan(clan)),
            "broods", I(clan?.WarPartyComponents.Count ?? 0), "maxBroods", I(CreatureBanditsConfig.MaxBroods)));
    });

    /// <summary>The daily census: the counts, then one line per live brood.</summary>
    internal static void Census(Clan clan, int existing, int strays, bool willSpawn) => Guard("daily", () =>
    {
        var logger = CreatureBanditDiag.Logger;
        logger?.LogInfo(CampaignLine("daily", Day, "broods", I(existing), "maxBroods", I(CreatureBanditsConfig.MaxBroods),
            "willSpawn", B(willSpawn), "strays", I(strays), "mapIconSkips", I(MapIconRiderSkips),
            "noParley", I(NoParleyFired), "prisonersRefused", I(PrisonersRefused)));
        var main = MobileParty.MainParty;
        foreach (WarPartyComponent component in clan.WarPartyComponents)
        {
            var party = component.MobileParty;
            if (party == null) continue;
            var home = party.HomeSettlement;
            logger?.LogInfo(CampaignLine("brood", Day, "party", party.StringId, "home", home?.StringId ?? "-",
                "homeIsAnchor", B(home != null && CreatureBanditsConfig.BroodAnchorSettlementIds.Contains(home.StringId)),
                "fromHome", home == null ? "-" : F(party.Position.ToVec2().Distance(home.GatePosition.ToVec2())),
                "fromPlayer", main == null ? "-" : F(party.Position.ToVec2().Distance(main.Position.ToVec2())),
                "behavior", party.DefaultBehavior.ToString(), "shortTerm", party.ShortTermBehavior.ToString(),
                "troops", I(party.MemberRoster.TotalManCount), "gold", I(party.PartyTradeGold),
                "inBattle", B(party.MapEvent != null), "visible", B(party.IsVisible)));
        }
    });

    /// <summary>A new brood: where, what it carries, and how far from the player.</summary>
    internal static void Spawned(MobileParty brood, Settlement anchor, float radius) => Guard("spawn", () =>
    {
        var main = MobileParty.MainParty;
        string roster = string.Join(",", brood.MemberRoster.GetTroopRoster().Select(e => e.Character.StringId + ":" + I(e.Number)));
        CreatureBanditDiag.Logger?.LogInfo(CampaignLine("brood-spawn", Day, "party", brood.StringId, "anchor", anchor.StringId,
            "anchorName", Name(anchor.Name?.ToString()), "radius", F(radius),
            "pos", F(brood.Position.ToVec2().x) + "," + F(brood.Position.ToVec2().y), "roster", roster,
            "troops", I(brood.MemberRoster.TotalManCount), "aggressiveness", F(brood.Aggressiveness),
            "fromPlayer", main == null ? "-" : F(brood.Position.ToVec2().Distance(main.Position.ToVec2())),
            "playerSight", main == null ? "-" : F(main.SeeingRange), "behavior", brood.DefaultBehavior.ToString()));
    });

    /// <summary>A brood sent back to its patrol.</summary>
    internal static void Stray(MobileParty party, string was, Settlement home) => Guard("stray", () =>
        CreatureBanditDiag.Logger?.LogInfo(CampaignLine("brood-stray", Day, "party", party.StringId, "was", was,
            "home", home.StringId, "now", party.DefaultBehavior.ToString())));

    /// <summary>A diagnostic never breaks the campaign tick it reports on.</summary>
    internal static void Guard(string site, Action write)
    {
        try { write(); }
        catch (Exception e) { CreatureBanditDiag.Logger?.LogWarning($"[CreatureBandits][diag] {site} line failed: {e.GetType().Name}: {e.Message}"); }
    }

    internal static void ResetForSession()
    {
        MapIconParties.Clear();
        MapIconRiderSkips = NoParleyFired = PrisonersRefused = 0;
    }

    /// <summary>Patch94 map icon: the rider visual was skipped for a brood's leader.</summary>
    internal static void NoteMapIconRiderSkipped(PartyBase? party)
    {
        MapIconRiderSkips++;
        string id = party?.MobileParty?.StringId ?? "-";
        if (MapIconParties.Count >= MapIconPartyCap || !MapIconParties.Add(id)) return;
        CreatureBanditDiag.Logger?.LogInfo(CampaignLine("map-icon", Day, "party", id,
            "leader", PartyBaseHelperLeader(party), "skips", I(MapIconRiderSkips), "note", "rider visual skipped; spider only"));
    }

    /// <summary>Patch94 no parley: a brood encounter skipped the conversation.</summary>
    internal static void NoteNoParley(MobileParty? party)
    {
        NoParleyFired++;
        CreatureBanditDiag.Logger?.LogInfo(CampaignLine("no-parley", Day, "party", party?.StringId ?? "-",
            "troops", I(party?.MemberRoster?.TotalManCount ?? -1), "count", I(NoParleyFired)));
    }

    /// <summary>CreatureBanditAgents.RefusesPrisoner: a creature troop was refused as a prisoner (logged first-N, counted).</summary>
    internal static void NotePrisonerRefused(string troopId)
    {
        PrisonersRefused++;
        if (PrisonersRefused <= PrisonerLineCap)
            CreatureBanditDiag.Logger?.LogInfo(CampaignLine("prisoner-refused", Day, "troop", troopId,
                "count", I(PrisonersRefused)));
    }

    private static string PartyBaseHelperLeader(PartyBase? party)
    {
        try { return Helpers.PartyBaseHelper.GetVisualPartyLeader(party)?.StringId ?? "-"; }
        catch { return "?"; }
    }
}
