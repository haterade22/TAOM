using System;
using System.Collections.Generic;
using TAOM.Core.Logging;
using TAOM.Features.Refuge.Components;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using Helpers;

namespace TAOM.Features.Refuge;

/// <summary>
/// Warden lifecycle (port of the Refuge module's SoldierPromotion + the behavior's companion
/// enumeration). Two deliberate departures from the source, both contract-mandated:
///
/// <para>Candidates are CLAN COMPANIONS in the main party, not any hero riding along; a visiting
/// noble or quest hero must never be strandable in a refuge. Promotable soldiers follow, and only
/// while the clan has a companion slot free, because resolving one mints a real companion.</para>
///
/// <para>Release NEVER kills. The source "depromoted" a promoted warden with
/// KillCharacterAction.ApplyByRemove and refunded the troop; here the promoted warden simply
/// remains a clan companion (he became somebody), and the soldier is not refunded.</para>
///
/// <para>Campaign statics sit behind protected virtuals (the CampService/SupplyOrderService
/// pattern) so the candidate ordering, the promote sequencing and the release matrix are all
/// unit-testable; the virtual bodies are the honest untested boundary sliver.</para>
/// </summary>
public class WardenService : IWardenService
{
    /// <summary>Random spread on a minted companion's age above coming-of-age (source value).</summary>
    private const int PromotedAgeSpreadYears = 14;
    private const int PromotedAgeBaseOffsetYears = 4;

    private readonly IModLogger _logger;

    public WardenService(IModLogger logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<WardenCandidate> Candidates()
    {
        var result = new List<WardenCandidate>();
        foreach (var companion in CompanionsInMainParty())
        {
            if (companion != null)
                result.Add(companion);
        }
        // Promotions gate on a free clan companion slot: resolving one calls AddCompanionAction,
        // which past the limit would either throw or silently overfill the clan roster.
        if (HasCompanionSlotFree())
        {
            foreach (var troop in PromotableTroopsInMainParty())
            {
                if (troop != null)
                    result.Add(troop);
            }
        }
        return result;
    }

    public bool AnyAvailable() => Candidates().Count > 0;

    public string ResolveWarden(WardenCandidate candidate, out bool promoted, out string promotedFromTroopId)
    {
        promoted = false;
        promotedFromTroopId = null;
        if (candidate == null || string.IsNullOrEmpty(candidate.Id))
            return null;

        if (candidate.IsCompanion)
            return candidate.Id;

        // Soldier path: re-check the gates at resolve time, not just at listing time; the picker
        // can sit open while the party fights a battle that empties the stack.
        if (!HasCompanionSlotFree())
            return null;
        if (TroopCountInMainParty(candidate.Id) < 1)
            return null;

        string heroId = MintCompanionFromTroop(candidate.Id);
        if (heroId == null)
            return null;

        // Exactly one soldier leaves the ranks; he is the person the new hero used to be.
        RemoveOneTroopFromMainParty(candidate.Id);
        promoted = true;
        promotedFromTroopId = candidate.Id;
        return heroId;
    }

    public void ReleaseWarden(string wardenHeroId, bool promoted)
    {
        if (string.IsNullOrEmpty(wardenHeroId))
            return;
        // NO-KILL policy (the contract): a promoted warden stays a clan companion. No
        // KillCharacterAction, no troop refund; the dismantle's roster merge carries him back
        // with the rest of the garrison.
        if (promoted)
            return;
        // A companion who is not with the refuge (captured, hospitalised) is left where fate put
        // him; the dismantle proceeds without touching him.
        if (!IsHeroWithRefugeParty(wardenHeroId))
            return;
        MoveHeroToMainParty(wardenHeroId);
    }

    // --- campaign-static seams (the untested boundary sliver; overridden in tests) ---

    protected virtual IReadOnlyList<WardenCandidate> CompanionsInMainParty()
    {
        var result = new List<WardenCandidate>();
        var roster = MobileParty.MainParty?.MemberRoster;
        var clan = Clan.PlayerClan;
        if (roster == null || clan == null)
            return result;
        for (int i = 0; i < roster.Count; i++)
        {
            var character = roster.GetCharacterAtIndex(i);
            var hero = character?.HeroObject;
            if (hero == null || hero == Hero.MainHero)
                continue;
            if (hero.CompanionOf != clan)
                continue;
            result.Add(new WardenCandidate
            {
                Id = hero.StringId,
                DisplayName = hero.Name?.ToString(),
                IsCompanion = true,
            });
        }
        return result;
    }

    protected virtual bool HasCompanionSlotFree()
    {
        var clan = Clan.PlayerClan;
        if (clan == null)
            return false;
        return (clan.Companions?.Count ?? 0) < clan.CompanionLimit;
    }

    protected virtual IReadOnlyList<WardenCandidate> PromotableTroopsInMainParty()
    {
        var result = new List<WardenCandidate>();
        var roster = MobileParty.MainParty?.MemberRoster;
        if (roster == null)
            return result;
        for (int i = 0; i < roster.Count; i++)
        {
            var element = roster.GetElementCopyAtIndex(i);
            var character = element.Character;
            if (character == null || character.IsHero || element.Number <= 0)
                continue;
            result.Add(new WardenCandidate
            {
                Id = character.StringId,
                DisplayName = character.Name?.ToString(),
                IsCompanion = false,
            });
        }
        return result;
    }

    protected virtual int TroopCountInMainParty(string troopId)
    {
        var roster = MobileParty.MainParty?.MemberRoster;
        var troop = FindTroop(troopId);
        if (roster == null || troop == null)
            return 0;
        return roster.GetTroopCount(troop);
    }

    /// <summary>
    /// Mints a companion hero from a troop: culture-matched companion template,
    /// HeroCreator.CreateSpecialHero into the player clan, renamed to the troop so "a Rohan
    /// Spearman became Captain-of-sorts" reads on screen, activated, AddCompanionAction, and
    /// placed in the main party so the founding flow can then move him into the refuge.
    /// Returns the hero StringId, or null when any engine step refuses.
    /// </summary>
    protected virtual string MintCompanionFromTroop(string troopId)
    {
        var troop = FindTroop(troopId);
        var clan = Clan.PlayerClan;
        var mainParty = MobileParty.MainParty;
        if (troop == null || troop.IsHero || clan == null || mainParty == null)
            return null;

        var culture = troop.Culture ?? Hero.MainHero?.Culture;
        var template = CharacterHelper.GetRandomCompanionTemplateWithPredicate(
                c => culture == null || c.Culture == culture)
            ?? CharacterHelper.GetRandomCompanionTemplateWithPredicate();
        if (template == null)
            return null;

        int comesOfAge = Campaign.Current?.Models?.AgeModel?.HeroComesOfAge ?? 18;
        int age = comesOfAge + PromotedAgeBaseOffsetYears + MBRandom.RandomInt(PromotedAgeSpreadYears);
        var hero = HeroCreator.CreateSpecialHero(template, bornSettlement: null, faction: clan, supporterOfClan: null, age: age);
        if (hero == null)
            return null;

        try
        {
            // The rename is cosmetic; a template-named hero is still a working warden, so a
            // localization hiccup here must not abort the promotion (source behaviour).
            hero.SetName(troop.Name, troop.Name);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"[Refuge] promoted-warden rename failed: {ex.Message}");
        }
        hero.ChangeState(Hero.CharacterStates.Active);
        AddCompanionAction.Apply(clan, hero);
        AddHeroToPartyAction.Apply(hero, mainParty, showNotification: false);
        return hero.StringId;
    }

    protected virtual bool RemoveOneTroopFromMainParty(string troopId)
    {
        var roster = MobileParty.MainParty?.MemberRoster;
        var troop = FindTroop(troopId);
        if (roster == null || troop == null)
            return false;
        roster.AddToCounts(troop, -1);
        return true;
    }

    protected virtual bool IsHeroWithRefugeParty(string heroId)
    {
        var hero = FindHero(heroId);
        return hero?.PartyBelongedTo?.PartyComponent is RefugePartyComponent;
    }

    protected virtual void MoveHeroToMainParty(string heroId)
    {
        var hero = FindHero(heroId);
        var mainParty = MobileParty.MainParty;
        if (hero == null || mainParty == null)
            return;
        AddHeroToPartyAction.Apply(hero, mainParty, showNotification: false);
    }

    private static Hero FindHero(string heroId) =>
        string.IsNullOrEmpty(heroId) ? null : Campaign.Current?.CampaignObjectManager?.Find<Hero>(heroId);

    private static CharacterObject FindTroop(string troopId) =>
        string.IsNullOrEmpty(troopId) ? null : MBObjectManager.Instance?.GetObject<CharacterObject>(troopId);
}
