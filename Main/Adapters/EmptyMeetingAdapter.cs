using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;

namespace TAOM.Adapters;

/// <summary>
/// Reads the partner <c>PlayerEncounter.DoMeetingInternal</c> would talk to if it reaches its conversation, with the
/// lookup that method uses. Its two army branches read no partner (#1099).
/// </summary>
internal sealed class EmptyMeetingAdapter : IEmptyMeetingAdapter
{
    public string? FindPartnerWithNoSpeaker()
    {
        var partner = MeetingPartner(PlayerEncounter.EncounteredParty);
        if (partner == null || ConversationHelper.GetConversationCharacterPartyLeader(partner) != null)
            return null;

        return partner.MobileParty?.StringId ?? partner.Settlement?.StringId ?? "unknown";
    }

    public void LeaveEncounter()
    {
        PlayerEncounter.Finish();
        MobileParty.MainParty?.SetMoveModeHold();
    }

    // A settlement partner stands for the first mobile defender, as in DoMeetingInternal.
    private static PartyBase? MeetingPartner(PartyBase? encountered)
    {
        if (encountered == null || !encountered.IsSettlement)
            return encountered;

        var defenders = MobileParty.MainParty?.MapEvent?.DefenderSide?.Parties;
        if (defenders == null)
            return encountered;

        foreach (var defender in defenders)
        {
            if (!defender.Party.IsSettlement)
                return defender.Party;
        }

        return encountered;
    }
}
