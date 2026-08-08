using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;
using TAOM.Features.FieldCommission.Domain;

namespace TAOM.Adapters;

public class HeroCommissionAdapter : IHeroCommissionAdapter
{
    // A modest stipend so a freshly-promoted companion isn't destitute — matches the donor mod's
    // flourish; not part of the mandatory design, kept as a documented constant rather than a new
    // config knob (simplicity-criterion: tiny win, not worth another JSON field).
    private const int StartingGold = 250;

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

        if (template.FirstBattleEquipment != null)
            hero.BattleEquipment.FillFrom(template.FirstBattleEquipment.Clone(false));

        // Most TAOM troops declare a civilian set, but ~60 do not (every Dale troop among them).
        // For those the engine has already handed the hero vanilla's `neutral_culture` fallback —
        // a Calradian peasant tunic — and the settlement spawn uses civilian equipment in towns and
        // castles, so the promoted soldier would walk Minas Tirith dressed as a Battanian villager.
        // Their own battle kit is the closer thing to right.
        var civilian = template.FirstCivilianEquipment ?? template.FirstBattleEquipment;
        if (civilian != null)
            hero.CivilianEquipment.FillFrom(civilian.Clone(false));

        hero.SetNewOccupation(Occupation.Wanderer);
        // AddCompanionAction — NEVER a raw Clan.Heroes.Add (bug fix (e); donor's dropped
        // Occupation.Lord path did exactly that).
        AddCompanionAction.Apply(Clan.PlayerClan, hero);

        AddHeroToPartyAction.Apply(hero, mainParty, false);

        return hero.StringId;
    }

    public bool IsHeroAliveAndValid(string heroId)
    {
        if (string.IsNullOrEmpty(heroId))
            return false;

        // NOT MBObjectManager.GetObject<Hero> — it cannot resolve these heroes at all.
        // CampaignObjectManager.AddHero hand-assigns hero.Id and appends to its own alive list; it
        // never calls MBObjectManager.RegisterObject. So every hero HeroCreator built at runtime —
        // which is every promoted companion — looked "dead or invalid" here, and the prune on load
        // silently emptied the promoted-hero list on the first save-load after any promotion.
        // Same lookup the sibling NamedCompanionAdapter uses.
        var hero = Hero.AllAliveHeroes?.FirstOrDefault(h => h != null && h.StringId == heroId);
        return hero != null && hero.IsAlive;
    }

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
