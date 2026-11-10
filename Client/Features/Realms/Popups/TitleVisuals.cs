using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TAOM.Features.Realms;
using TAOM.Features.Realms.Domain.Titles;

namespace TAOM.Client.Features.Realms.Popups;

/// <summary>The banner a title is drawn with and the link to its encyclopedia page.</summary>
internal static class TitleVisuals
{
    /// <summary>The holder's clan banner; for a vacant title the realm's banner, else the fief owner's clan banner.</summary>
    public static Banner Banner(RealmsCampaign b, TitleDefinition title)
    {
        var holder = Hero.Find(b.Service.Ledger.HolderOf(title.Id) ?? string.Empty);
        var banner = holder?.ClanBanner
            ?? Kingdom(title.KingdomId)?.Banner
            ?? Fief(title)?.OwnerClan?.Banner;
        return banner ?? new Banner();
    }

    /// <summary>Every title, of any rank, opens its own encyclopedia page (<see cref="Encyclopedia.TitleEncyclopediaPageVM"/>).</summary>
    public static string Link(TitleDefinition title) => Encyclopedia.RealmsTitle.LinkOf(title.Id);

    public static void GoTo(string? link)
    {
        if (!string.IsNullOrEmpty(link))
            TaleWorlds.CampaignSystem.Campaign.Current?.EncyclopediaManager.GoToLink(link);
    }

    private static Kingdom? Kingdom(string? kingdomId) =>
        kingdomId == null ? null : TaleWorlds.CampaignSystem.Campaign.Current?.CampaignObjectManager.Find<Kingdom>(kingdomId);

    private static Settlement? Fief(TitleDefinition title) =>
        title.SettlementId == null ? null : Settlement.Find(title.SettlementId);
}
