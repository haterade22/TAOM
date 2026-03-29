using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace TAOM.Features.BattleBalance.Models;

public class TaomCombatSimulationModel : DefaultCombatSimulationModel
{
    private readonly IBattleBalanceSettingsProvider _settings;

    public TaomCombatSimulationModel(IBattleBalanceSettingsProvider settings)
        => _settings = settings;

    public override float GetBluntDamageChance(CharacterObject strikerTroop,
        CharacterObject strikedTroop, PartyBase strikerParty, PartyBase strikedParty,
        MapEvent battle)
    {
        if (!_settings.EnableCustomCasualtyRatios)
            return base.GetBluntDamageChance(strikerTroop, strikedTroop,
                strikerParty, strikedParty, battle);

        return CalculateBluntChance(battle.IsPlayerMapEvent,
            _settings.PlayerBluntDamageChance, _settings.AIBluntDamageChance);
    }

    internal static float CalculateBluntChance(bool isPlayerMapEvent, float playerChance, float aiChance)
        => isPlayerMapEvent ? playerChance : aiChance;
}
