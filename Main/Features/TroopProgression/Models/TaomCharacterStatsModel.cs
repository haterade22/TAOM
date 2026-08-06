using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Library;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.TroopProgression.Models;

// #394 — this model owns the career `Health` passive for the WHOLE game, mission included.
// `SandboxAgentStatCalculateModel.GetEffectiveMaxHealth` opens with
// `if (agent.IsHero) return agent.Character.MaxHitPoints();`, and CharacterObject.MaxHitPoints()
// is this model, so a hero's in-battle health limit already flows from here. Do NOT also add the
// Health passive on the agent-stat path — that double-counts (a +75 pip becomes +150 in battle).
public class TaomCharacterStatsModel : DefaultCharacterStatsModel
{
    private readonly ICareerPassiveService _careerPassives;

    public TaomCharacterStatsModel(ICareerPassiveService careerPassives)
    {
        _careerPassives = careerPassives;
    }

    public override int MaxCharacterTier => 10;

    public override ExplainedNumber MaxHitpoints(CharacterObject character, bool includeDescriptions = false)
    {
        var result = base.MaxHitpoints(character, includeDescriptions);
        _careerPassives.ApplyFlat(character?.HeroObject?.StringId, ref result, PassiveEffectType.Health);
        return result;
    }
}
