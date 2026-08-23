using TAOM.Features.Refuge;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace TAOM.Features.BattleBalance.Models;

public class TaomCombatSimulationModel : DefaultCombatSimulationModel
{
    private readonly IBattleBalanceSettingsProvider _settings;
    private readonly IRefugeDefenseService _refugeDefense;

    public TaomCombatSimulationModel(IBattleBalanceSettingsProvider settings)
        : this(settings, null)
    {
    }

    public TaomCombatSimulationModel(IBattleBalanceSettingsProvider settings, IRefugeDefenseService refugeDefense)
    {
        _settings = settings;
        _refugeDefense = refugeDefense;
    }

    // Refuge (#507): defenders of a ready refuge take reduced auto-resolve damage. The source
    // module patched this method with Harmony; TAOM owns the model slot, so the reduction is an
    // ordinary override consulting one service (the same service the real-time battle path uses,
    // so the two systems cannot drift apart).
    public override ExplainedNumber SimulateHit(CharacterObject strikerTroop,
        CharacterObject struckTroop, PartyBase strikerParty, PartyBase struckParty,
        float strikerAdvantage, MapEvent battle, float strikerSideMorale, float struckSideMorale)
    {
        var result = base.SimulateHit(strikerTroop, struckTroop, strikerParty, struckParty,
            strikerAdvantage, battle, strikerSideMorale, struckSideMorale);

        var reduction = _refugeDefense?.DefenderDamageReduction(struckParty?.MobileParty?.StringId) ?? 0f;
        // Shared composition contract (RefugeDamageReduction): (1 - r) on the FINAL number, same
        // as the real-time path. A bare AddFactor(-r) here composed against the BASE and drifted
        // from real-time whenever vanilla factors were non-zero. NaN/out-of-range applies nothing.
        RefugeDamageReduction.Apply(ref result, reduction);

        return result;
    }

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
