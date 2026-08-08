using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.BattleBalance.Models;

public class TaomPartyHealingModel : DefaultPartyHealingModel
{
    private readonly IBattleBalanceSettingsProvider _settings;
    private readonly IBattleBalanceConfigProvider _configProvider;
    private readonly ICareerPassiveService _careerPassives;
    private readonly TAOM.Features.Enlistment.IEnlistmentStateQuery _enlistment;

    public TaomPartyHealingModel(IBattleBalanceSettingsProvider settings,
        IBattleBalanceConfigProvider configProvider,
        ICareerPassiveService careerPassives,
        TAOM.Features.Enlistment.IEnlistmentStateQuery enlistment)
    {
        _settings = settings;
        _configProvider = configProvider;
        _careerPassives = careerPassives;
        _enlistment = enlistment;
    }

    public override float GetSurvivalChance(PartyBase party, CharacterObject character,
        DamageTypes damageType, bool canDamageKillEvenIfBlunt, PartyBase enemyParty = null)
    {
        // ENLISTED PLAYERS ARE ROLLED AGAINST THE COMPANY THAT TENDS THEM, not their own
        // hidden one-man party.
        //
        // Every survival bonus vanilla grants is read off the PASSED party:
        // AddSurgeonSurvivalBonus(mobileParty, ...), the PhysicianOfPeople perk, and
        // HasPerk(Medicine.CheatDeath, checkSecondaryRole: true). An enlisted player's party is
        // one hero, parked and hidden, with no surgeon and no perks — so a soldier who goes
        // down in his commander's battle is rolled as if nobody were there to save him, and is
        // MORE likely to die than the same character freelancing. That is a silent, invisible
        // penalty for serving, and it is the opposite of the bargain.
        //
        // Redirected here rather than by a Harmony patch because TAOM already owns this model.
        party = RedirectEnlistedPlayerToCommanderParty(party, character);

        float vanillaSurvival = base.GetSurvivalChance(
            party, character, damageType, canDamageKillEvenIfBlunt, enemyParty);

        if (party == null)
            return vanillaSurvival;

        float result = vanillaSurvival;

        if (_settings.EnableCulturalSurvivalBonuses)
        {
            var config = _configProvider.GetConfig();
            if (config.CasualtyRatios.EnableCulturalSurvivalBonuses)
            {
                // Vanilla PartyBaseHelper.HasFeat precedence — same fix family Codex 43 + 46
                // applied to feat-keyed models. Per-culture survival bonus is a culture-keyed
                // config lookup; should use the same leader→party→owner→settlement walk.
                var culture = TAOM.Features.CulturalFeats.CultureFeatAdapter.ResolvePartyCulture(party);
                if (culture != null)
                {
                    float bonus = config.CasualtyRatios.GetCulturalSurvivalBonus(culture.StringId);
                    if (bonus != 0f)
                        result = ApplyCulturalSurvivalBonus(result, bonus);
                }
            }
        }

        // Career passive: TroopSurvival increases survival chance. GetSurvivalChance returns a
        // raw float (not ExplainedNumber), so the multiplicative apply stays inline here.
        var heroId = CareerPassiveHero.ResolveId(party);
        if (heroId != null)
        {
            float magnitude = _careerPassives.GetPassiveMagnitude(heroId, PassiveEffectType.TroopSurvival);
            if (magnitude != 0f)
                result = Math.Min(1f, result * (1f + magnitude));
        }

        return result;
    }

    public override ExplainedNumber GetDailyHealingHpForHeroes(PartyBase party, bool isPrisoners, bool includeDescriptions = false)
    {
        var result = base.GetDailyHealingHpForHeroes(party, isPrisoners, includeDescriptions);
        // Career passive: HeroHealing boosts the party's hero daily HP recovery. Resolved
        // via CareerPassiveHero, never party.Owner — the engine getter throws for ownerless
        // settlement parties and this override runs on every settlement daily tick (crash 0b462fd8).
        _careerPassives.ApplyFactor(CareerPassiveHero.ResolveId(party), ref result, PassiveEffectType.HeroHealing);
        return result;
    }

    internal static float ApplyCulturalSurvivalBonus(float vanillaSurvival, float culturalBonus)
    {
        if (culturalBonus == 0f)
            return vanillaSurvival;

        float deathChance = 1f - vanillaSurvival;
        float newDeathChance = deathChance * (1f - culturalBonus);
        float result = 1f - newDeathChance;
        return Math.Max(0f, Math.Min(1f, result));
    }

    /// <summary>
    /// Substitutes the commander's party when the PLAYER's own survival is being rolled while
    /// enlisted. Narrow on purpose — three conditions must all hold, and any failure returns the
    /// original party untouched:
    ///  * the character being rolled is the player (companions and troops are unaffected),
    ///  * the party being rolled is the player's own,
    ///  * service is live and the commander resolves to a real party.
    ///
    /// Boundary conversion in a GameModel, which is where ADR-007 permits sealed engine types.
    /// </summary>
    private PartyBase RedirectEnlistedPlayerToCommanderParty(PartyBase party, CharacterObject character)
    {
        try
        {
            if (party == null || character == null || !character.IsPlayerCharacter)
                return party;
            if (_enlistment?.IsEnlisted != true)
                return party;
            if (party != PartyBase.MainParty)
                return party;

            var commanderId = _enlistment.CommanderHeroId;
            if (string.IsNullOrEmpty(commanderId))
                return party;

            var commanderParty = Campaign.Current?.CampaignObjectManager
                ?.Find<Hero>(commanderId)?.PartyBelongedTo?.Party;

            // Never hand back something WORSE than what we were given.
            return commanderParty ?? party;
        }
        catch
        {
            // A survival roll must never throw — vanilla behaviour is the safe answer.
            return party;
        }
    }
}
