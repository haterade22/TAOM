using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Core;

namespace TAOM.Features.Arena.Models;

public class TaomTournamentModel : DefaultTournamentModel
{
    public override Equipment GetParticipantArmor(CharacterObject participant)
    {
        if (participant.Culture != null)
        {
            var dummy = Game.Current.ObjectManager.GetObject<CharacterObject>(
                $"gear_practice_dummy_{participant.Culture.StringId}");
            if (dummy?.RandomBattleEquipment != null)
                return dummy.RandomBattleEquipment;
        }

        return base.GetParticipantArmor(participant);
    }

    internal static string ResolveDummyId(string participantCultureId, string settlementCultureId)
    {
        if (!string.IsNullOrEmpty(participantCultureId))
            return $"gear_practice_dummy_{participantCultureId}";
        if (!string.IsNullOrEmpty(settlementCultureId))
            return $"gear_practice_dummy_{settlementCultureId}";
        return "gear_practice_dummy_empire";
    }
}
