using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TAOM.Features.CareerSystem;
using TAOM.Adapters;
using TAOM.Features.CareerSystem.Domain;
using TAOM.Features.CoopInterop;
using TAOM.Features.WarChronicle.Effects;

namespace TAOM.Features.CulturalFeats.Models;

public class TaomPartyMoraleModel : DefaultPartyMoraleModel
{
    private readonly ICulturalFeatsService _feats;
    private readonly ICareerPassiveService _careerPassives;
    private readonly WarMoraleService _warMorale;
    private readonly IPlayerControlQuery _players;

    public TaomPartyMoraleModel(ICulturalFeatsService feats, ICareerPassiveService careerPassives,
        WarMoraleService warMorale, IPlayerControlQuery players)
    {
        _feats = feats;
        _careerPassives = careerPassives;
        _warMorale = warMorale;
        _players = players;
    }

    public override ExplainedNumber GetEffectivePartyMorale(
        MobileParty party, bool includeDescription = false)
    {
        var result = base.GetEffectivePartyMorale(party, includeDescription);
        // Vanilla PartyBaseHelper.HasFeat precedence via the shared helper.
        _feats.ApplyMoraleFeats(CultureFeatAdapter.FromOrNull(party.Party), ref result);
        _careerPassives.ApplyFactor(party.LeaderHero?.StringId, ref result, PassiveEffectType.TroopMorale);
        var kingdom = party.ActualClan?.Kingdom?.StringId;
        _warMorale.Apply(kingdom, _warMorale.IsActiveFor(kingdom) && PlayerClanCheck.IsAPlayersClan(party.ActualClan, _players),
            GetStandardBaseMorale(party.Party), ref result);
        return result;
    }
}
