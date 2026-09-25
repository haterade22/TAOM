using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Library;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Domain;
using TAOM.Features.CombatMechanics;

namespace TAOM.Features.TroopProgression.Models;

// #394 — this model owns the career `Health` passive for the WHOLE game, mission included.
// `SandboxAgentStatCalculateModel.GetEffectiveMaxHealth` opens with
// `if (agent.IsHero) return agent.Character.MaxHitPoints();`, and CharacterObject.MaxHitPoints()
// is this model, so a hero's in-battle health limit already flows from here. Do NOT also add the
// Health passive on the agent-stat path — that double-counts (a +75 pip becomes +150 in battle).
//
// Race base health (Mike, 2026-09-25: both trolls have 200). Vanilla starts every campaign character at 100 whatever
// its race, and a troop's battle health is this number (Agent BaseHealthLimit = CharacterObject.MaxHitPoints()), so
// the race's baseHitPoints row lifts it here: campaign battles, heroes and auto-resolve alike. Custom Battle reads the
// race Monster's hit_points instead; TrollHitPointsLiveDataTests keeps the two equal.
public class TaomCharacterStatsModel : DefaultCharacterStatsModel
{
    private readonly ICareerPassiveService _careerPassives;
    private readonly IRaceCombatModifiersResolver _raceModifiers;

    public TaomCharacterStatsModel(ICareerPassiveService careerPassives, IRaceCombatModifiersResolver raceModifiers)
    {
        _careerPassives = careerPassives;
        _raceModifiers = raceModifiers;
    }

    public override int MaxCharacterTier => 10;

    public override ExplainedNumber MaxHitpoints(CharacterObject character, bool includeDescriptions = false)
    {
        var result = base.MaxHitpoints(character, includeDescriptions);
        // ExplainedNumber.Add returns at once on 0, so every race without a baseHitPoints row is untouched.
        result.Add(_raceModifiers.BaseHitPointsBonus(character?.Race));
        _careerPassives.ApplyFlat(character?.HeroObject?.StringId, ref result, PassiveEffectType.Health);
        return result;
    }
}
