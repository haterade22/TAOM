using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;
using TAOM.Core.Logging;
using TAOM.Features.FieldCommission.Domain;

namespace TAOM.Adapters;

public class HeroCommissionAdapter : IHeroCommissionAdapter
{
    // A modest stipend so a freshly-promoted companion isn't destitute — matches the donor mod's
    // flourish; not part of the mandatory design, kept as a documented constant rather than a new
    // config knob (simplicity-criterion: tiny win, not worth another JSON field).
    private const int StartingGold = 250;

    private readonly IModLogger _logger;

    public HeroCommissionAdapter(IModLogger logger)
    {
        _logger = logger;
    }

    public CompanionRoomInfo GetCompanionRoomInfo()
    {
        var clan = Clan.PlayerClan;
        if (clan == null || Campaign.Current == null)
            return new CompanionRoomInfo(0, 0);

        var current = clan.Heroes.Count(h => h != null && h.IsAlive && h.IsPlayerCompanion && h.Clan == clan);
        var limit = Campaign.Current.Models.ClanTierModel.GetCompanionLimit(clan);
        return new CompanionRoomInfo(current, limit);
    }

    public string CreateCompanionFromTroop(string troopId, CommissionSkillPlan skillPlan, string chosenName)
    {
        var template = string.IsNullOrEmpty(troopId) ? null : MBObjectManager.Instance?.GetObject<CharacterObject>(troopId);
        var mainParty = MobileParty.MainParty;
        if (template == null || mainParty == null || skillPlan == null)
            return null;

        // The promotion must come OUT of the roster — a soldier is being commissioned, not conjured.
        // Between the offer being queued and the player answering the prompt, the last of that type
        // can die, be given away, or upgrade out of existence; without this check the hero is created
        // anyway, the roster decrement silently fails, and the player gains a companion from nothing.
        if (mainParty.MemberRoster == null || mainParty.MemberRoster.GetTroopCount(template) <= 0)
            return null;

        // Culture-matched fallback chain. Settlement.CurrentSettlement is null for the ordinary case
        // — a promotion completed on the world map after a field battle — and a null born settlement
        // sends the engine to HeroCreationModel.GetBornSettlement, which picks a RANDOM town. Naming
        // the commander's current settlement, then a town of the soldier's own culture, keeps a
        // Gondorian soldier from being recorded as born in Mordor.
        var bornSettlement = ResolveBornSettlement(template);
        var hero = HeroCreator.CreateSpecialHero(template, bornSettlement, null, null, System.Math.Max(23, skillPlan.HeroLevel + 5));

        var name = string.IsNullOrWhiteSpace(chosenName) ? (template.Name?.ToString() ?? "Promoted Soldier") : chosenName.Trim();
        hero.SetName(new TextObject(name), new TextObject(name));
        hero.ChangeState(Hero.CharacterStates.Active);
        hero.SetHasMet();
        hero.ChangeHeroGold(StartingGold);

        ApplySkillPlan(hero, skillPlan);

        // Filling THROUGH the getter is safe here, but only by one call frame: Hero.BattleEquipment
        // and its siblings return a campaign-wide shared singleton when the backing field is null,
        // and CreateSpecialHero has just run SetInitialValuesFromCharacter, which assigns all three
        // (falling back to neutral_culture). Patch71 cannot rely on that at fire time and carries an
        // explicit ReferenceEquals guard instead. Do not copy this pattern anywhere the hero was not
        // constructed moments earlier.
        //
        // No Clone on the source: FillFrom copies the 12 slots out by value and never writes to the
        // source, so cloning the template's equipment first only allocated a throwaway (#486 review).
        if (template.FirstBattleEquipment != null)
            hero.BattleEquipment.FillFrom(template.FirstBattleEquipment);

        // Most TAOM troops do NOT declare a civilian set: 743 of 895 troop blocks across the 18
        // files in ModuleData/troops (measured 2026-08-20, #486), every Dale, Dunland, Gondor,
        // Harad and Rhûn troop among them. Rivendell, Lindon, Umbar and Erebor are the exceptions.
        // For those the engine has already handed the hero vanilla's `neutral_culture` fallback —
        // a Calradian peasant tunic — and the settlement spawn uses civilian equipment in towns and
        // castles, so the promoted soldier would walk Minas Tirith dressed as a Battanian villager.
        // Their own battle kit is the closer thing to right.
        var civilian = template.FirstCivilianEquipment ?? template.FirstBattleEquipment;
        if (civilian != null)
            // useSourceEquipmentType is false on the battle fallback: FillFrom copies the source's
            // EquipmentType onto the target, so passing true here retyped the hero's civilian kit
            // EquipmentType.Battle. Same defect Patch71 guards against on the fire path (#486).
            hero.CivilianEquipment.FillFrom(civilian, civilian.IsCivilian);

        hero.SetNewOccupation(Occupation.Wanderer);
        // AddCompanionAction — NEVER a raw Clan.Heroes.Add (bug fix (e); donor's dropped
        // Occupation.Lord path did exactly that).
        AddCompanionAction.Apply(Clan.PlayerClan, hero);

        AddHeroToPartyAction.Apply(hero, mainParty, false);

        return hero.StringId;
    }

    public bool IsHeroAliveAndValid(string heroId)
    {
        var hero = FindAliveHero(heroId);
        return hero != null && hero.IsAlive;
    }

    public PromotedHeroSnapshot GetPromotedHeroSnapshot(string heroId)
    {
        var hero = FindAliveHero(heroId);
        if (hero == null)
            return PromotedHeroSnapshot.Missing;

        // Template => CharacterObject.OriginalCharacter, a saveable field on the character the
        // promotion built, so the origin troop is still known after a load. PartyBelongedTo is a
        // plain field read; null for a governor, a prisoner and a fugitive alike.
        var party = hero.PartyBelongedTo;
        return new PromotedHeroSnapshot(
            hero.Name?.ToString(),
            hero.CharacterObject?.OriginalCharacter?.StringId,
            hero.IsPlayerCompanion,
            party?.IsMainParty == true,
            IsPartyInBattle(party),
            hero.IsWounded);
    }

    public bool RemoveCompanionFromGame(string heroId)
    {
        var hero = FindAliveHero(heroId);
        if (hero == null)
            return false;

        // KillCharacterAction.ApplyInternal adds a DeathMark and RETURNS while the party has a
        // MapEvent or a SiegeEvent, so the hero would be removed later, after the fight, with no
        // way for the caller to know. A refund against that is a soldier from nowhere. Refuse.
        if (IsPartyInBattle(hero.PartyBelongedTo))
            return false;

        FadeOutSceneAgent(hero);

        try
        {
            // The one call vanilla's own fire line ends on (companion_fire_on_consequence) and the
            // Refuge warden rollback ships. isForced skips CanDie; MakeDead takes the hero out of
            // the roster; the companion link is cut through RemoveCompanionAction.ApplyByDeath, and
            // the Death detail skips the fugitive interlude and Hero.ResetEquipments (#486).
            KillCharacterAction.ApplyByRemove(hero);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning($"[FieldCommission] removing '{heroId}' threw {ex.GetType().Name}: {ex.Message}");
            return false;
        }

        return hero.IsDead;
    }

    /// <summary>
    /// Inside a settlement scene the hero also has a live <c>Agent</c>, and nothing on the engine's
    /// removal path touches it: <c>KillCharacterAction</c> only drops the <c>LocationCharacter</c>,
    /// which is the spawn list for the NEXT entry, so the dismissed companion would keep standing in
    /// the tavern as a ghost the player can still click (<c>MissionConversationLogic.IsThereAgentAction</c>
    /// never asks whether the hero is alive). Vanilla never meets this because its own fire line is
    /// map-only. Remove the agent first, the way <c>MissionAgentHandler.FadeoutExitingLocationCharacter</c>
    /// removes a character leaving through a passage and with the same refusal on a mission that is
    /// already ending, but with <c>hideInstantly</c> set: a visible fade keeps the agent Active for its
    /// whole duration, and <c>IsThereAgentAction</c> would let a click in those frames open a
    /// conversation with a hero that is already dead and un-clanned. The instant form is what vanilla
    /// uses for a departing multiplayer peer. On the map there is no mission and nothing to do. A hide
    /// that throws is logged and does not block the dismissal: a lingering figure is a lesser wrong
    /// than a hero who stays.
    /// </summary>
    private void FadeOutSceneAgent(Hero hero)
    {
        try
        {
            var mission = Mission.Current;
            if (mission == null || mission.CurrentState != Mission.State.Continuing)
                return;

            var character = hero.CharacterObject;
            foreach (var agent in mission.Agents)
            {
                if (agent == null || !agent.IsActive() || !ReferenceEquals(agent.Character, character))
                    continue;

                agent.FadeOut(hideInstantly: true, hideMount: true);
                return;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning($"[FieldCommission] fading out the scene agent of '{hero.StringId}' threw {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// NOT MBObjectManager.GetObject&lt;Hero&gt; — it cannot resolve these heroes at all.
    /// CampaignObjectManager.AddHero hand-assigns hero.Id and appends to its own alive list; it
    /// never calls MBObjectManager.RegisterObject. So every hero HeroCreator built at runtime —
    /// which is every promoted companion — looked "dead or invalid" to that lookup, and the prune
    /// on load silently emptied the promoted-hero list on the first save-load after any promotion.
    /// Same lookup the sibling NamedCompanionAdapter uses.
    /// </summary>
    private static Hero FindAliveHero(string heroId)
    {
        if (string.IsNullOrEmpty(heroId))
            return null;

        return Hero.AllAliveHeroes?.FirstOrDefault(h => h != null && h.StringId == heroId);
    }

    /// <summary>The exact predicate <c>KillCharacterAction.ApplyInternal</c> defers a removal on.</summary>
    private static bool IsPartyInBattle(MobileParty party) =>
        party != null && (party.MapEvent != null || party.SiegeEvent != null);

    /// <summary>
    /// Where this soldier is recorded as born. Restores the donor mod's three-step fallback, which
    /// the first port dropped: the settlement the player is standing in, else any town of the
    /// troop's own culture, else any town at all. Returning null is still safe — the engine falls
    /// back to <c>HeroCreationModel.GetBornSettlement</c> — it is just not deterministic, and
    /// <c>Hero.BornSettlement</c> is what <c>UpdateHomeSettlement</c> lands on for a companion.
    /// </summary>
    private static Settlement ResolveBornSettlement(CharacterObject template)
    {
        var current = Settlement.CurrentSettlement ?? Hero.MainHero?.CurrentSettlement;
        if (current != null)
            return current;

        if (Settlement.All == null)
            return null;

        // One pass, not two: remember the first town of any culture while looking for one of the
        // troop's own, so the fallback costs nothing extra when no culture match exists.
        // Settlement.Culture is a plain saveable field, not a computed getter, so it is safe to read
        // once the settlement itself is non-null (adapters rule).
        Settlement anyTown = null;
        foreach (var settlement in Settlement.All)
        {
            if (settlement == null || !settlement.IsTown)
                continue;

            if (settlement.Culture == template.Culture)
                return settlement;

            anyTown = anyTown ?? settlement;
        }

        return anyTown;
    }

    private static void ApplySkillPlan(Hero hero, CommissionSkillPlan plan)
    {
        var developer = hero.HeroDeveloper;
        developer.SetInitialLevel(plan.HeroLevel);
        hero.Level = plan.HeroLevel;

        foreach (var pair in plan.SkillValues)
        {
            var skill = MBObjectManager.Instance?.GetObject<SkillObject>(pair.Key);
            if (skill == null)
                continue;

            developer.SetInitialSkillLevel(skill, pair.Value);

            // AddFocus with checkUnspentFocusPoints:false does no bounds checking of its own, and
            // InitializeHeroDeveloper has already spent this hero's starting focus points during
            // CreateSpecialHero — so a skill it happened to max out would be pushed past the engine
            // cap here, which the character screen renders as an out-of-range focus row.
            if (pair.Value > 0 && plan.FocusPerNonZeroSkill > 0)
            {
                var headroom = Campaign.Current.Models.CharacterDevelopmentModel.MaxFocusPerSkill
                    - developer.GetFocus(skill);
                if (headroom > 0)
                    developer.AddFocus(skill, System.Math.Min(plan.FocusPerNonZeroSkill, headroom), checkUnspentFocusPoints: false);
            }
        }

        if (plan.FlatAttributeBonus > 0)
        {
            foreach (CharacterAttribute attribute in Attributes.All)
            {
                if (attribute != null)
                    developer.AddAttribute(attribute, plan.FlatAttributeBonus, checkUnspentPoints: false);
            }
        }
    }
}
