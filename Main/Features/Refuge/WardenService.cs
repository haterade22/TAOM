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

/// <summary>One hero in the main party's member roster, read at the campaign boundary. Pure data:
/// the warden eligibility filter in <see cref="WardenService"/> reads only these fields.</summary>
public sealed class PartyHeroInfo
{
    public string HeroId;
    public string DisplayName;

    /// <summary>True for <c>Hero.MainHero</c>.</summary>
    public bool IsMainHero;

    /// <summary>True when the hero's <c>CompanionOf</c> is the player clan; false when there is
    /// no player clan.</summary>
    public bool IsPlayerClanCompanion;
}

/// <summary>What a promotion needs to know about the troop and the player, read at the campaign
/// boundary. The seam returns null instead when the troop, the player clan or the main party is
/// missing.</summary>
public sealed class PromotionSource
{
    public bool TroopIsHero;

    /// <summary>The troop's culture StringId, or null.</summary>
    public string TroopCultureId;

    /// <summary>The main hero's culture StringId, or null.</summary>
    public string PlayerCultureId;
}

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
/// remains a clan companion (he became somebody), and the soldier is not refunded. The ONE place
/// ApplyByRemove survives is UnwindPromotion: a founding that failed after the promotion, before
/// the minted hero ever attached to a refuge - a transactional rollback, not a release.</para>
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

    /// <summary>Coming-of-age when the campaign has no AgeModel (source value).</summary>
    private const int DefaultComesOfAgeYears = 18;

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
        // NO-KILL policy (the contract): a promoted warden stays a clan companion - no
        // KillCharacterAction, no troop refund. He rides the SAME AddHeroToPartyAction move as a
        // companion warden: an earlier build let the roster merge "carry him back", but a raw
        // roster copy+clear nulls a hero's PartyBelongedTo (the engine's OnHeroRemoved fires on
        // the clear), so every hero must move by action.
        // A warden who is not with the refuge (captured, hospitalised) is left where fate put
        // him; the dismantle proceeds without touching him.
        if (!IsHeroWithRefugeParty(wardenHeroId))
            return;
        MoveHeroToMainParty(wardenHeroId);
    }

    public void UnwindPromotion(string wardenHeroId, string promotedFromTroopId)
    {
        if (string.IsNullOrEmpty(wardenHeroId) || string.IsNullOrEmpty(promotedFromTroopId))
            return;
        // The never-attached window: the hero was minted seconds ago, sits in the main party, and
        // never led anything. Removing him here is the transactional rollback of ResolveWarden,
        // not a violation of the no-kill release policy (which governs wardens who served).
        if (!RemoveMintedCompanion(wardenHeroId))
            return;
        AddOneTroopToMainParty(promotedFromTroopId);
        _logger.LogInfo(
            $"[Refuge] founding failed after promotion; unwound minted warden '{wardenHeroId}' and refunded one '{promotedFromTroopId}'.");
    }

    /// <summary>The player clan's companions riding in the main party, in roster order: every
    /// hero except the main hero whose CompanionOf is the player clan. A visiting noble or quest
    /// hero is never a candidate. internal for TAOM.Tests (InternalsVisibleTo).</summary>
    internal IReadOnlyList<WardenCandidate> CompanionsInMainParty()
    {
        var result = new List<WardenCandidate>();
        foreach (var hero in HeroesInMainParty())
        {
            if (hero == null || hero.IsMainHero)
                continue;
            if (!hero.IsPlayerClanCompanion)
                continue;
            result.Add(new WardenCandidate
            {
                Id = hero.HeroId,
                DisplayName = hero.DisplayName,
                IsCompanion = true,
            });
        }
        return result;
    }

    /// <summary>
    /// Mints a companion hero from a troop: culture-matched companion template,
    /// HeroCreator.CreateSpecialHero into the player clan, renamed to the troop so "a Rohan
    /// Spearman became Captain-of-sorts" reads on screen, activated, AddCompanionAction, and
    /// placed in the main party so the founding flow can then move him into the refuge.
    /// Returns the hero StringId, or null when any engine step refuses. The step order is the
    /// source's (template draw, then the age draw), so the campaign RNG is consumed as before.
    /// internal for TAOM.Tests (InternalsVisibleTo).
    /// </summary>
    internal string MintCompanionFromTroop(string troopId)
    {
        var source = ReadPromotionSource(troopId);
        if (source == null || source.TroopIsHero)
            return null;

        // Culture-matched template first; any companion template when the culture has none.
        string cultureId = source.TroopCultureId ?? source.PlayerCultureId;
        string templateId = RandomCompanionTemplateId(cultureId) ?? RandomCompanionTemplateId(null);
        if (templateId == null)
            return null;

        int age = (HeroComesOfAge() ?? DefaultComesOfAgeYears)
            + PromotedAgeBaseOffsetYears
            + NextRandomInt(PromotedAgeSpreadYears);
        string heroId = CreatePromotedHero(templateId, age);
        if (heroId == null)
            return null;

        try
        {
            // The rename is cosmetic; a template-named hero is still a working warden, so a
            // localization hiccup here must not abort the promotion (source behaviour).
            RenamePromotedHero(heroId, troopId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"[Refuge] promoted-warden rename failed: {ex.Message}");
        }
        EnrolPromotedHero(heroId);
        return heroId;
    }

    // --- campaign-static seams (the untested boundary sliver; overridden in tests) ---

    /// <summary>Every hero in the main party's member roster, in roster order, with the two facts
    /// the companion filter reads. Empty when there is no main party roster.</summary>
    protected virtual IReadOnlyList<PartyHeroInfo> HeroesInMainParty()
    {
        var result = new List<PartyHeroInfo>();
        var roster = MobileParty.MainParty?.MemberRoster;
        var clan = Clan.PlayerClan;
        if (roster == null)
            return result;
        for (int i = 0; i < roster.Count; i++)
        {
            var hero = roster.GetCharacterAtIndex(i)?.HeroObject;
            if (hero == null)
                continue;
            result.Add(new PartyHeroInfo
            {
                HeroId = hero.StringId,
                DisplayName = hero.Name?.ToString(),
                IsMainHero = hero == Hero.MainHero,
                IsPlayerClanCompanion = clan != null && hero.CompanionOf == clan,
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
                Tier = character.Tier,
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

    /// <summary>Reads the promotion inputs: the troop's hero flag and culture, and the player's
    /// culture. Null when the troop, the player clan or the main party is missing.</summary>
    protected virtual PromotionSource ReadPromotionSource(string troopId)
    {
        var troop = FindTroop(troopId);
        if (troop == null || Clan.PlayerClan == null || MobileParty.MainParty == null)
            return null;
        return new PromotionSource
        {
            TroopIsHero = troop.IsHero,
            TroopCultureId = troop.Culture?.StringId,
            PlayerCultureId = Hero.MainHero?.Culture?.StringId,
        };
    }

    /// <summary>One random wanderer companion template: of the given culture, or of any culture
    /// when <paramref name="cultureId"/> is null. Its StringId, or null when none matches. Draws
    /// from the campaign RNG.</summary>
    protected virtual string RandomCompanionTemplateId(string cultureId)
    {
        var template = cultureId == null
            ? CharacterHelper.GetRandomCompanionTemplateWithPredicate()
            : CharacterHelper.GetRandomCompanionTemplateWithPredicate(
                c => string.Equals(c.Culture?.StringId, cultureId, StringComparison.Ordinal));
        return template?.StringId;
    }

    /// <summary>The campaign AgeModel's coming-of-age, or null when there is no model.</summary>
    protected virtual int? HeroComesOfAge() => Campaign.Current?.Models?.AgeModel?.HeroComesOfAge;

    /// <summary>One campaign RNG draw in [0, <paramref name="maxExclusive"/>).</summary>
    protected virtual int NextRandomInt(int maxExclusive) => MBRandom.RandomInt(maxExclusive);

    /// <summary>Creates a special hero from the template into the player clan at the given age.
    /// The hero's StringId, or null when the template or the clan is missing or the engine
    /// refuses.</summary>
    protected virtual string CreatePromotedHero(string templateId, int age)
    {
        var template = FindTroop(templateId);
        var clan = Clan.PlayerClan;
        if (template == null || clan == null)
            return null;
        var hero = HeroCreator.CreateSpecialHero(template, bornSettlement: null, faction: clan, supporterOfClan: null, age: age);
        return hero?.StringId;
    }

    /// <summary>Renames the hero after the troop he was. May throw; the caller tolerates it.</summary>
    protected virtual void RenamePromotedHero(string heroId, string troopId)
    {
        var hero = FindHero(heroId);
        var troop = FindTroop(troopId);
        if (hero == null || troop == null)
            return;
        hero.SetName(troop.Name, troop.Name);
    }

    /// <summary>Activates the hero, makes him a player-clan companion and puts him in the main
    /// party (the source's three engine calls, in order).</summary>
    protected virtual void EnrolPromotedHero(string heroId)
    {
        var hero = FindHero(heroId);
        var clan = Clan.PlayerClan;
        var mainParty = MobileParty.MainParty;
        if (hero == null || clan == null || mainParty == null)
            return;
        hero.ChangeState(Hero.CharacterStates.Active);
        AddCompanionAction.Apply(clan, hero);
        AddHeroToPartyAction.Apply(hero, mainParty, showNotification: false);
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

    /// <summary>Removes a just-minted, never-attached companion from the game (the source's
    /// depromote action). KillCharacterAction.ApplyByRemove handles the party row, the clan
    /// companion slot and the hero record in one engine call.</summary>
    protected virtual bool RemoveMintedCompanion(string heroId)
    {
        var hero = FindHero(heroId);
        if (hero == null || !hero.IsAlive)
            return false;
        try
        {
            KillCharacterAction.ApplyByRemove(hero);
            return true;
        }
        catch (Exception ex)
        {
            // A failed rollback leaves an extra companion, an annoyance; a throw here would eat
            // the player's founding-failure message.
            _logger.LogWarning($"[Refuge] promotion unwind failed for '{heroId}': {ex.Message}");
            return false;
        }
    }

    protected virtual void AddOneTroopToMainParty(string troopId)
    {
        var roster = MobileParty.MainParty?.MemberRoster;
        var troop = FindTroop(troopId);
        if (roster == null || troop == null)
            return;
        roster.AddToCounts(troop, 1);
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
