using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Localization;

namespace TAOM.Features.Diplomacy.Models;

public class TaomAllianceModel : DefaultAllianceModel
{
    private readonly IDiplomacyService _diplomacyService;
    private readonly HostilePeace.HostilePeaceService? _hostilePeace;

    /// <param name="hostilePeace">Hostile Peace block D; null keeps the old full freedom for the player's kingdom.</param>
    public TaomAllianceModel(IDiplomacyService diplomacyService, HostilePeace.HostilePeaceService? hostilePeace = null)
    {
        _diplomacyService = diplomacyService;
        _hostilePeace = hostilePeace;
    }

    /// <summary>The Hostile tier's penalty, which a player pair across the Free/Evil line gets instead of the player bonus.</summary>
    internal const float CrossSidePlayerPenalty = -10000f;

    public override int MaxNumberOfAlliances => int.MaxValue;

    public override CampaignTime MaxDurationOfAlliance => CampaignTime.Years(100);

    public override ExplainedNumber GetScoreOfStartingAlliance(
        Kingdom querierKingdom,
        Kingdom queriedKingdom,
        out TextObject explanationText,
        bool includeDescription = false)
    {
        var result = base.GetScoreOfStartingAlliance(
            querierKingdom, queriedKingdom, out explanationText, includeDescription);

        bool involvesPlayer = PlayerKingdomHelper.InvolvesPlayerRuledKingdom(querierKingdom, queriedKingdom);

        // Block D (docs/features/hostile-peace.md): the player's kingdom across the Free/Evil line gets the Hostile penalty, not
        // the player bonus, so the AI never offers it an alliance and the score explains why.
        bool crossSide = involvesPlayer && HostilePeace.Models.PlayerAllianceGate.Blocks(_hostilePeace, querierKingdom, queriedKingdom);

        float modifier = crossSide
            ? CrossSidePlayerPenalty
            : _diplomacyService.GetAllianceScoreModifier(querierKingdom.StringId, queriedKingdom.StringId, involvesPlayer);

        if (modifier != 0f)
        {
            var label = involvesPlayer && !crossSide
                ? new TextObject("{=taom_alliance_player_freedom}Player Diplomacy")
                : new TextObject("{=taom_alliance_lore}Lore Alignment");
            result.Add(modifier, label);
        }

        return result;
    }
}
