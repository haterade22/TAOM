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
    private readonly IBattleBalanceConfigProvider _configProvider;

    public TaomCombatSimulationModel(IBattleBalanceSettingsProvider settings, IRefugeDefenseService refugeDefense,
        IBattleBalanceConfigProvider configProvider)
    {
        _settings = settings;
        _refugeDefense = refugeDefense;
        _configProvider = configProvider;
    }

    // Refuge (#507): defenders of a ready refuge take reduced auto-resolve damage. The source
    // module patched this method with Harmony; TAOM owns the model slot, so the reduction is an
    // ordinary override consulting one service (the same service the real-time battle path uses,
    // so the two systems cannot drift apart).
    public override ExplainedNumber SimulateHit(CharacterObject strikerTroop,
        CharacterObject struckTroop, PartyBase strikerParty, PartyBase struckParty,
        float strikerAdvantage, MapEvent battle, BattleEnvironment battleEnvironment,
        float strikerSideMorale, float struckSideMorale)
    {
        // v1.5.2 added BattleEnvironment between the map event and the morale pair. Passed straight
        // through: the refuge reduction below does not depend on where the battle is fought.
        var result = base.SimulateHit(strikerTroop, struckTroop, strikerParty, struckParty,
            strikerAdvantage, battle, battleEnvironment, strikerSideMorale, struckSideMorale);

        // #1283: numbers over quality. The base raised the power ratio to 0.7; rescale it to the configured exponent, on the
        // final number, before the refuge reduction (both scale the final number, so the order does not matter). This runs
        // for every simulated strike, the player's Send troops and raids included.
        AutoResolveQualityMath.Apply(ref result, TroopPower(strikerTroop, strikerParty), TroopPower(struckTroop, struckParty), PowerRatioExponent);

        var reduction = _refugeDefense?.DefenderDamageReduction(struckParty?.MobileParty?.StringId) ?? 0f;
        // Shared composition contract (RefugeDamageReduction): (1 - r) on the FINAL number, same
        // as the real-time path. A bare AddFactor(-r) here composed against the BASE and drifted
        // from real-time whenever vanilla factors were non-zero. NaN/out-of-range applies nothing.
        RefugeDamageReduction.Apply(ref result, reduction);

        return result;
    }

    private float PowerRatioExponent => _configProvider?.GetConfig()?.AutoResolve?.PowerRatioExponent ?? AutoResolveQualityMath.VanillaPowerRatioExponent;

    // The power the base read for this troop (DefaultCombatSimulationModel.SimulateHit, line 19). The base passes the
    // internal MapEventSide.LeaderSimulationModifier, which the engine caches from the leader party's hero's PowerModifier
    // at event start, after a load and when the leader party leaves. This live read matches it, except after the side's
    // leader hero dies or is replaced during the event: the engine keeps the old cached value (a few percent).
    private static float TroopPower(CharacterObject troop, PartyBase party) =>
        Campaign.Current.Models.MilitaryPowerModel.GetTroopPower(troop, party.Side, party.MapEvent.SimulationContext,
            party.MapEventSide.LeaderParty?.LeaderHero?.PowerModifier ?? 0f);

    // #1283: the defenders of a besieged fortification hit harder when its realm's culture has a siege defence bonus. The
    // realm, as for the survival row (CultureFeatAdapter.ResolvePartyCulture), so a player vassal's castle counts too.
    // The base gives the attackers AddFactor(-0.1) in every siege (DefaultCombatSimulationModel.GetBattleAdvantage); the
    // bonus is a factor on the defenders' side. MapEvent.SimulateBattleRound reads both once per round.
    public override void GetBattleAdvantage(MapEvent mapEvent, out ExplainedNumber defenderAdvantage,
        out ExplainedNumber attackerAdvantage)
    {
        base.GetBattleAdvantage(mapEvent, out defenderAdvantage, out attackerAdvantage);

        var bonus = SiegeDefenceBonus(mapEvent.EventType == MapEvent.BattleTypes.Siege,
            mapEvent.MapEventSettlement?.MapFaction?.Culture?.StringId, _configProvider?.GetConfig()?.SiegeDefence);
        if (bonus > 0f)
            defenderAdvantage.AddFactor(bonus);
    }

    internal static float SiegeDefenceBonus(bool isSiege, string ownerCultureId, BattleBalanceConfig.SiegeDefenceSection section)
        => isSiege && section != null ? section.GetDefenderAdvantageBonus(ownerCultureId) : 0f;

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
