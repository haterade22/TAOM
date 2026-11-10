using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Features.CoopInterop;
using TAOM.Features.Execution;

namespace TAOM.Adapters;

/// <summary>
/// Production <see cref="IWarPledgeWorldAdapter"/>. <c>Kingdom.RulingClan</c> and <c>Kingdom.Name</c> are
/// stored members (v1.5.7 Kingdom.cs:96, 206), so a non-null kingdom is safe to read. The ruler test goes
/// through <see cref="IPlayerControlQuery"/>, which knows co-op players; <c>Clan.PlayerClan</c> names this
/// peer only. One whole sentence per side, so each language can inflect it.
/// </summary>
public sealed class WarPledgeWorldAdapter : IWarPledgeWorldAdapter
{
    private static readonly Color NoticeColor = Color.FromUint(0xFFE8D8A8);

    private readonly IPlayerControlQuery _playerControl;

    public WarPledgeWorldAdapter(IPlayerControlQuery playerControl) => _playerControl = playerControl;

    // Unknown (a co-op registry that cannot be read) counts as a player: in doubt the AI leaves the realm alone.
    public bool IsPlayerRuled(string kingdomId)
    {
        var kingdom = FindKingdom(kingdomId);
        return kingdom != null && _playerControl.OfClan(kingdom.RulingClan) != PlayerControl.NotPlayer;
    }

    public void AnnouncePledge(string kingdomId, FactionSide side)
    {
        var kingdom = FindKingdom(kingdomId);
        if (kingdom == null || side == FactionSide.Neutral)
            return;

        // Present tense on purpose: a past-tense verb must agree with the realm's gender in Russian and
        // Polish, and those names carry no grammar tag.
        var text = side == FactionSide.Free
            ? new TextObject("{=taom_war_pledge_joined_free}{REALM} joins the Free Peoples in the War of the Ring.")
            : new TextObject("{=taom_war_pledge_joined_evil}{REALM} joins the Shadow in the War of the Ring.");
        text.SetTextVariable("REALM", kingdom.Name ?? TextObject.GetEmpty());

        MBInformationManager.AddQuickInformation(text);
        InformationManager.DisplayMessage(new InformationMessage(text.ToString(), NoticeColor));
    }

    private static Kingdom? FindKingdom(string kingdomId)
    {
        if (string.IsNullOrEmpty(kingdomId) || Campaign.Current == null)
            return null;
        return Kingdom.All.FirstOrDefault(k => k.StringId == kingdomId);
    }
}
