using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TAOM.Core.Logging;

namespace TAOM.Features.AlignmentDesertion.Hooks;

/// <summary>
/// Thin entry point (ADR-002): drives daily alignment desertion for mobile parties and settlement
/// garrisons. All decisions live in <see cref="IAlignmentDesertionService"/>; this behavior only does
/// the engine I/O — snapshot the roster into POCOs, hand them to the service, apply the returned
/// removals (mirrors <c>SpecialResourcesBehavior</c>'s read-snapshot / pure-decision / write-back
/// split). Stateless — desertion is recomputed each day from live rosters, so there is nothing to
/// persist in <see cref="SyncData"/>.
/// </summary>
public class AlignmentDesertionBehavior : CampaignBehaviorBase
{
    private readonly IAlignmentDesertionService _service;
    private readonly IModLogger _logger;

    public AlignmentDesertionBehavior(IAlignmentDesertionService service, IModLogger logger)
    {
        _service = service;
        _logger = logger;
    }

    public override void RegisterEvents()
    {
        CampaignEvents.DailyTickPartyEvent.AddNonSerializedListener(this, OnDailyTickParty);
        CampaignEvents.DailyTickSettlementEvent.AddNonSerializedListener(this, OnDailyTickSettlement);
    }

    public override void SyncData(IDataStore dataStore)
    {
        // Stateless: desertion is recomputed each day from live rosters; nothing TAOM-specific to round-trip.
    }

    private void OnDailyTickParty(MobileParty party)
    {
        if (!_service.IsEnabled || party == null)
            return;

        // Only lord field parties (incl. army members, which each tick individually) and the player's
        // main party shed troops here. Caravans (companion-led, so they have a clan + kingdom),
        // villagers, militia, bandits, and garrison parties are excluded — garrisons desert via
        // OnDailyTickSettlement under their own MCM gate. (Without this gate a player/AI caravan's
        // guards would desert — the "lords' field parties" promise the MCM hint makes. Codex #1.)
        if (!party.IsLordParty && !party.IsMainParty)
            return;

        var clan = party.LeaderHero?.Clan;
        var kingdom = clan?.Kingdom;
        if (kingdom == null)
            return;

        ApplyDesertion(party.MemberRoster, kingdom.StringId, clan == Clan.PlayerClan,
            isGarrison: false, showPlayerPopup: party == MobileParty.MainParty);
    }

    private void OnDailyTickSettlement(Settlement settlement)
    {
        if (!_service.IsEnabled || settlement == null)
            return;

        var garrison = settlement.Town?.GarrisonParty;
        if (garrison?.MemberRoster == null)
            return;

        var clan = settlement.OwnerClan;
        var kingdom = clan?.Kingdom;
        if (kingdom == null)
            return;

        // Garrison desertion is silent (no per-settlement popup spam); it's logged below.
        ApplyDesertion(garrison.MemberRoster, kingdom.StringId, clan == Clan.PlayerClan,
            isGarrison: true, showPlayerPopup: false);
    }

    private void ApplyDesertion(TroopRoster roster, string kingdomId, bool isPlayerOwned, bool isGarrison, bool showPlayerPopup)
    {
        if (roster == null)
            return;

        // Snapshot first — never mutate the roster while scanning it.
        var snapshot = new List<DesertionTroopInfo>(roster.Count);
        foreach (var element in roster.GetTroopRoster())
        {
            var character = element.Character;
            if (character == null)
                continue;
            snapshot.Add(new DesertionTroopInfo(character.StringId, character.Culture?.StringId, character.IsHero, element.Number));
        }

        if (snapshot.Count == 0)
            return;

        var desertions = _service.CalculateDesertion(kingdomId, isPlayerOwned, isGarrison, snapshot);
        if (desertions.Count == 0)
            return;

        var total = 0;
        foreach (var entry in desertions)
        {
            var character = CharacterObject.Find(entry.TroopId);
            if (character == null)
                continue;

            var index = roster.FindIndexOfTroop(character);
            if (index < 0)
                continue;

            var current = roster.GetElementNumber(index);
            var toRemove = Math.Min(entry.DesertCount, current);
            if (toRemove <= 0)
                continue;

            roster.AddToCounts(character, -toRemove);
            total += toRemove;
        }

        if (total <= 0)
            return;

        // Log-hygiene: only the player's own desertions are worth recording; AI-kingdom
        // desertions fire on every party/garrison daily and never surface to the player.
        if (isPlayerOwned)
            _logger.LogInfo($"[AlignDesert] {total} opposed-alignment troops deserted (kingdom={kingdomId}, garrison={isGarrison})");

        if (showPlayerPopup)
        {
            MBInformationManager.AddQuickInformation(
                new TextObject("{=taom_align_desertion}{COUNT} troops deserted — they refuse to serve a lord of the opposing alignment.")
                    .SetTextVariable("COUNT", total),
                extraTimeInMs: 3000);
        }
    }
}
