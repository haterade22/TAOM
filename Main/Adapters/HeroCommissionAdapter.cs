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

        var bornSettlement = Settlement.CurrentSettlement;
        var hero = HeroCreator.CreateSpecialHero(template, bornSettlement, null, null, System.Math.Max(23, skillPlan.HeroLevel + 5));

        var name = string.IsNullOrWhiteSpace(chosenName) ? (template.Name?.ToString() ?? "Promoted Soldier") : chosenName.Trim();
        hero.SetName(new TextObject(name), new TextObject(name));
        hero.ChangeState(Hero.CharacterStates.Active);
        hero.SetHasMet();
        hero.ChangeHeroGold(StartingGold);

        ApplySkillPlan(hero, skillPlan);

        if (template.FirstBattleEquipment != null)
            hero.BattleEquipment.FillFrom(template.FirstBattleEquipment.Clone(false));
        if (template.FirstCivilianEquipment != null)
            hero.CivilianEquipment.FillFrom(template.FirstCivilianEquipment.Clone(false));

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

        var hero = MBObjectManager.Instance?.GetObject<Hero>(heroId);
        return hero != null && hero.IsAlive;
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
            if (pair.Value > 0 && plan.FocusPerNonZeroSkill > 0)
                developer.AddFocus(skill, plan.FocusPerNonZeroSkill, checkUnspentFocusPoints: false);
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
