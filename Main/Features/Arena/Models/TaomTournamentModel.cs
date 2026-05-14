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
    // Phase 9b #137 — tier band constants stay on the model so config XML/MCM can reference them.
    internal const float RegularMinTier = 2f;
    internal const float RegularMaxTier = 4f;
    internal const float EliteMinTier = 4f;

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
        var items = _service.BuildPrizePool(town?.Culture?.StringId, RegularMinTier, RegularMaxTier);
        return items.Count > 0
            ? items
            : base.GetRegularRewardItems(town, regularRewardMinValue, regularRewardMaxValue);
    }

    public override MBList<ItemObject> GetEliteRewardItems(
        Town town, int regularRewardMinValue, int regularRewardMaxValue)
    {
        var items = _service.BuildPrizePool(town?.Culture?.StringId, EliteMinTier, float.MaxValue);
        return items.Count > 0
            ? items
            : base.GetEliteRewardItems(town, regularRewardMinValue, regularRewardMaxValue);
    }

    public override Equipment GetParticipantArmor(CharacterObject participant)
    {
        var dummyId = _service.ResolveDummyId(participant?.Culture?.StringId, null);
        var dummy = Game.Current?.ObjectManager?.GetObject<CharacterObject>(dummyId);
        if (dummy?.RandomBattleEquipment != null)
            return dummy.RandomBattleEquipment;
        return base.GetParticipantArmor(participant);
    }
}
