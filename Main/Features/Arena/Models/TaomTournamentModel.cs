using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.TournamentGames;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace TAOM.Features.Arena.Models;

public class TaomTournamentModel : DefaultTournamentModel
{
    private readonly ITournamentService _service;

    public TaomTournamentModel(ITournamentService service)
    {
        _service = service;
    }

    public override float GetTournamentStartChance(Town town)
    {
        // Phase 9b #137 — boundary work: extract lord count via TaleWorlds APIs; service decides
        // the chance value. Campaign.Current null-guard added per audit P2 #2 (NRE risk outside
        // campaign context — custom battles, main menu preview).
        if (town?.Settlement == null) return 0f;
        if (town.Settlement.SiegeEvent != null) return 0f;
        var ageModel = Campaign.Current?.Models?.AgeModel;
        if (ageModel == null) return 0f;
        int count = town.Settlement.Parties.Count(x => x.IsLordParty)
                  + town.Settlement.HeroesWithoutParty.Count(x =>
                        x.IsActive && x.Age >= ageModel.HeroComesOfAge);
        return _service.CalculateStartChance(count);
    }

    public override float GetTournamentEndChance(TournamentGame tournament)
    {
        if (tournament == null) return 0f;
        return _service.CalculateEndChance(tournament.CreationTime.ElapsedDaysUntilNow);
    }

    public override MBList<ItemObject> GetRegularRewardItems(
        Town town, int regularRewardMinValue, int regularRewardMaxValue)
    {
        // base is a last resort only: the service already falls back to every culture's items.
        var items = _service.BuildPrizePool(town?.Culture?.StringId, PrizeBand.Regular);
        return items.Count > 0
            ? items
            : base.GetRegularRewardItems(town, regularRewardMinValue, regularRewardMaxValue);
    }

    public override MBList<ItemObject> GetEliteRewardItems(
        Town town, int regularRewardMinValue, int regularRewardMaxValue)
    {
        var items = _service.BuildPrizePool(town?.Culture?.StringId, PrizeBand.Elite);
        return items.Count > 0
            ? items
            : base.GetEliteRewardItems(town, regularRewardMinValue, regularRewardMaxValue);
    }

    // Renown and influence for every winner (docs/features/tournament-rewards.md). The engine asks at the award
    // and again for the winner panel; both read the same tournament's hero count, noted by TournamentRewardsBehavior's
    // TournamentFinished listener (it runs before vanilla's handler; the order is traced on the behavior). A new
    // campaign's leaderboard seeding asks renown with a null town, which the service answers with vanilla's value.
    public override int GetRenownReward(Hero winner, Town town) =>
        _service.RenownReward(base.GetRenownReward(winner, town), town?.Settlement?.StringId, winner?.Culture?.StringId);

    public override int GetInfluenceReward(Hero winner, Town town) =>
        _service.InfluenceReward(base.GetInfluenceReward(winner, town), town?.Settlement?.StringId,
            winner?.Culture?.StringId, winner?.Clan?.Kingdom?.StringId, town?.OwnerClan?.Kingdom?.StringId);

    public override Equipment GetParticipantArmor(CharacterObject participant)
    {
        var dummyId = _service.ResolveDummyId(participant?.Culture?.StringId, null);
        var dummy = Game.Current?.ObjectManager?.GetObject<CharacterObject>(dummyId);
        if (dummy?.RandomBattleEquipment != null)
            return dummy.RandomBattleEquipment;
        return base.GetParticipantArmor(participant);
    }
}
