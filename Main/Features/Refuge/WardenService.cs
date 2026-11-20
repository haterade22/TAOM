using System;
using System.Collections.Generic;
using TAOM.Core.Logging;
using TAOM.Features.Refuge.Components;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
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

/// <summary>The ties of a promoted warden that make removing him unsafe, read at the campaign
/// boundary. Pure data for <see cref="WardenService.MayReturnToRanks"/>.</summary>
public sealed class PromotedWardenFacts
{
    /// <summary>True when the hero's <c>CompanionOf</c> is the player clan.</summary>
    public bool IsPlayerClanCompanion;
    public bool HasSpouse;
    public bool HasChildren;
    public bool IsClanLeader;
}

/// <summary>The hero a promotion just created, handed from the create seam to the rename and
/// enrol seams so they act on that exact hero without a second lookup (the
/// <see cref="RefugePrisoner"/> precedent).</summary>
public sealed class MintedHero
{
    public string HeroId;

    /// <summary>The engine <c>Hero</c>; null in tests, opaque to the decision logic.</summary>
    public object EngineHero;
}

/// <summary>
/// Warden lifecycle (port of the Refuge module's SoldierPromotion + the behavior's companion
/// enumeration). Two deliberate departures from the source, both contract-mandated:
///
/// <para>Candidates are CLAN COMPANIONS in the main party, not any hero riding along; a visiting
/// noble or quest hero must never be strandable in a refuge. Promotable soldiers follow, and only
/// while the clan has a companion slot free, because resolving one mints a real companion.</para>
///
/// <para>Release never kills a companion warden. A promoted warden goes back to the ranks on
/// dismantle, as in the source (KillCharacterAction.ApplyByRemove plus one of his troop),
/// but only after he left the refuge by action and only when he has no family or role that the
/// removal would break; otherwise he stays a clan companion. UnwindPromotion uses the same
/// removal for a founding that failed before the minted hero ever attached to a refuge.</para>
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

        RemoveOneTroopFromMainParty(candidate.Id);
        promoted = true;
        promotedFromTroopId = candidate.Id;
        return heroId;
    }

    public void ReleaseWarden(string wardenHeroId, bool promoted, string promotedFromTroopId)
    {
        if (string.IsNullOrEmpty(wardenHeroId))
            return;
        if (!IsHeroWithRefugeParty(wardenHeroId))
            return;
        // Every warden leaves the refuge by action first: a raw roster copy+clear nulls a hero's
        // PartyBelongedTo (the engine's OnHeroRemoved fires on the clear). A promoted warden is
        // then removed from the MAIN party, the same state UnwindPromotion removes a minted hero
        // from: removing him while he still led the refuge would run the engine's
        // party-leader branch of the kill, which starts a disband.
        MoveHeroToMainParty(wardenHeroId);
        if (promoted)
            ReturnToRanks(wardenHeroId, promotedFromTroopId);
    }

    /// <summary>
    /// Turns a promoted warden back into one soldier on dismantle (owner, 15 Nov 2026: keeping him
    /// as a companion made found, promote, dismantle a free companion every time). Removal is
    /// KillCharacterAction.ApplyByRemove, the call UnwindPromotion already uses: no death pop-up
    /// (the engine's CommentOnCharacterKilledBehavior still logs "NAME was lost." to chat), the
    /// clan companion slot is freed, his gold goes to the clan leader. A governorship needs no
    /// tie check: AddHeroToPartyAction already removed it when he rejoined the main party.
    /// His equipment is the template kit he was minted with (a refuge warden's gear cannot be
    /// changed: the stash screen holds items only), so it is not handed out; handing it out would
    /// be a free kit every cycle.
    /// </summary>
    private void ReturnToRanks(string heroId, string promotedFromTroopId)
    {
        if (!MayReturnToRanks(ReadPromotedWarden(heroId)))
        {
            _logger.LogInfo(
                $"[Refuge] promoted warden '{heroId}' has family or a role; he stays a clan companion.");
            return;
        }
        string troopId = IsRegularTroop(promotedFromTroopId) ? promotedFromTroopId : CultureBasicTroopId(heroId);
        if (!IsRegularTroop(troopId))
        {
            _logger.LogWarning(
                $"[Refuge] no troop to return promoted warden '{heroId}' as (source '{promotedFromTroopId}'); he stays a clan companion.");
            return;
        }
        if (!RemoveMintedCompanion(heroId))
            return;
        AddOneTroopToMainParty(troopId);
        ShowReturnedToRanks(troopId);
        _logger.LogInfo($"[Refuge] promoted warden '{heroId}' returned to the ranks as one '{troopId}'.");
    }

    /// <summary>True when removing the promoted hero breaks nothing: still the player's companion,
    /// no spouse, no children, leads no clan. Null facts (hero record gone) are
    /// false. internal for TAOM.Tests (InternalsVisibleTo).</summary>
    internal static bool MayReturnToRanks(PromotedWardenFacts facts) =>
        facts != null
        && facts.IsPlayerClanCompanion
        && !facts.HasSpouse
        && !facts.HasChildren
        && !facts.IsClanLeader;

    public void UnwindPromotion(string wardenHeroId, string promotedFromTroopId)
    {
        if (string.IsNullOrEmpty(wardenHeroId) || string.IsNullOrEmpty(promotedFromTroopId))
            return;
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

        string cultureId = source.TroopCultureId ?? source.PlayerCultureId;
        string templateId = RandomCompanionTemplateId(cultureId) ?? RandomCompanionTemplateId(null);
        if (templateId == null)
            return null;

        int age = (HeroComesOfAge() ?? DefaultComesOfAgeYears)
            + PromotedAgeBaseOffsetYears
            + NextRandomInt(PromotedAgeSpreadYears);
        var minted = CreatePromotedHero(templateId, age);
        if (minted?.HeroId == null)
            return null;

        try
        {
            // The rename is cosmetic; a template-named hero is still a working warden, so a
            // localization hiccup here must not abort the promotion (source behaviour).
            RenamePromotedHero(minted, troopId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"[Refuge] promoted-warden rename failed: {ex.Message}");
        }
        EnrolPromotedHero(minted);
        return minted.HeroId;
    }

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
    /// The created hero with its StringId, or null when the template or the clan is missing or the
    /// engine refuses.</summary>
    protected virtual MintedHero CreatePromotedHero(string templateId, int age)
    {
        var template = FindTroop(templateId);
        var clan = Clan.PlayerClan;
        if (template == null || clan == null)
            return null;
        var hero = HeroCreator.CreateSpecialHero(template, bornSettlement: null, faction: clan, supporterOfClan: null, age: age);
        return hero == null ? null : new MintedHero { HeroId = hero.StringId, EngineHero = hero };
    }

    /// <summary>Renames the hero after the troop he was. May throw; the caller tolerates it.</summary>
    protected virtual void RenamePromotedHero(MintedHero minted, string troopId)
    {
        var troop = FindTroop(troopId);
        if (!(minted?.EngineHero is Hero hero) || troop == null)
            return;
        hero.SetName(troop.Name, troop.Name);
    }

    /// <summary>Activates the hero, makes him a player-clan companion and puts him in the main
    /// party (the source's three engine calls, in order).</summary>
    protected virtual void EnrolPromotedHero(MintedHero minted)
    {
        var clan = Clan.PlayerClan;
        var mainParty = MobileParty.MainParty;
        if (!(minted?.EngineHero is Hero hero) || clan == null || mainParty == null)
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

    /// <summary>Removes a minted companion from the game (the source's depromote action): the
    /// never-attached rollback and the dismantle return to the ranks. KillCharacterAction
    /// .ApplyByRemove (showNotification false) removes the party row and frees the clan companion
    /// slot in one engine call; the hero stays in the records as dead. The dismantle guard checks
    /// the refuge's map event, not the main party's; no caller is known to reach this while the
    /// main party is in a battle or a siege.</summary>
    protected virtual bool RemoveMintedCompanion(string heroId)
    {
        var hero = FindHero(heroId);
        if (hero == null || !hero.IsAlive)
            return false;
        try
        {
            KillCharacterAction.ApplyByRemove(hero);
            // Inside a map or siege event the engine only adds a death mark, which a later daily
            // tick executes; report that as not removed, so no soldier is added on top of a hero
            // who is still there.
            return !hero.IsAlive;
        }
        catch (Exception ex)
        {
            // A failed rollback leaves an extra companion, an annoyance; a throw here would eat
            // the player's founding-failure message.
            _logger.LogWarning($"[Refuge] removing minted warden '{heroId}' failed: {ex.Message}");
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

    /// <summary>The ties that make removing a promoted warden unsafe, read from the hero. Null
    /// when the hero is not found.</summary>
    protected virtual PromotedWardenFacts ReadPromotedWarden(string heroId)
    {
        var hero = FindHero(heroId);
        if (hero == null)
            return null;
        var clan = Clan.PlayerClan;
        return new PromotedWardenFacts
        {
            IsPlayerClanCompanion = clan != null && hero.CompanionOf == clan,
            HasSpouse = hero.Spouse != null,
            HasChildren = (hero.Children?.Count ?? 0) > 0,
            IsClanLeader = hero.Clan?.Leader == hero,
        };
    }

    /// <summary>True when the id names a regular (non-hero) troop that exists in this game.</summary>
    protected virtual bool IsRegularTroop(string troopId)
    {
        var troop = FindTroop(troopId);
        return troop != null && !troop.IsHero;
    }

    /// <summary>The basic troop of the hero's culture, or null.</summary>
    protected virtual string CultureBasicTroopId(string heroId) => FindHero(heroId)?.Culture?.BasicTroop?.StringId;

    protected virtual void ShowReturnedToRanks(string troopId)
    {
        var troop = FindTroop(troopId);
        var text = new TextObject("{=taom_rf_warden_ranks}Your promoted warden returns to the ranks as {TROOP}.");
        text.SetTextVariable("TROOP", troop?.Name ?? new TextObject(troopId));
        InformationManager.DisplayMessage(new InformationMessage(text.ToString()));
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
