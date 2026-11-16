using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace TAOM.Features.HostilePeace.Models;

/// <summary>
/// Block C: the player meets a Hostile realm's settlements during peace as the base game does at war
/// (<see cref="HostilePeaceAccess"/>). It runs the base model first and changes only its answer, so crime, disguise, clan tier and
/// the player's own fiefs keep the base rules. What it leaves alone:
/// <list type="bullet">
/// <item>A settlement the player's clan owns, and any of the player's own realm (never a Hostile pair).</item>
/// <item>A town's keep and the dungeon (<c>CanMainHeroEnterDungeon</c>, and <c>CanMainHeroEnterLordsHall</c> for a town): a town
/// keep is reached only after a sneak in, where the base model already answers as at war.</item>
/// <item>A castle's keep is the exception: "Request entry to the castle" (castle_guard menu) asks
/// <c>CanMainHeroEnterLordsHall</c> from outside, and the base model grants a clan tier 3 player full access at peace, so a Hostile
/// castle gets the war answer there (<see cref="HostilePeaceAccess.KeepAsAtWar"/>).</item>
/// <item>Request a meeting, ransom brokers, quests: unchanged base-game rules once the player is inside or meets a lord outside.</item>
/// </list>
/// Main hero only; the AI never asks this model. No co-op gate: it decides only the local player's own menus.
/// </summary>
public sealed class TaomSettlementAccessModel : DefaultSettlementAccessModel
{
    private readonly HostilePeaceService _service;

    public TaomSettlementAccessModel(HostilePeaceService service)
    {
        _service = service;
    }

    public override void CanMainHeroEnterSettlement(Settlement settlement, out AccessDetails accessDetails)
    {
        base.CanMainHeroEnterSettlement(settlement, out accessDetails);
        if (settlement == null || !(settlement.IsTown || settlement.IsCastle))
            return;
        if (Limits(settlement))
            accessDetails = HostilePeaceAccess.AsAtWar(settlement.IsTown);
    }

    public override void CanMainHeroEnterLordsHall(Settlement settlement, out AccessDetails accessDetails)
    {
        base.CanMainHeroEnterLordsHall(settlement, out accessDetails);
        if (settlement != null && settlement.IsCastle && Limits(settlement))
            accessDetails = HostilePeaceAccess.KeepAsAtWar();
    }

    public override bool CanMainHeroDoSettlementAction(Settlement settlement, SettlementAction settlementAction,
        out bool disableOption, out TextObject disabledText)
    {
        var allowed = base.CanMainHeroDoSettlementAction(settlement, settlementAction, out disableOption, out disabledText);
        if (!allowed || settlement == null || !settlement.IsVillage)
            return allowed;

        var refusal = HostilePeaceAccess.VillageRefusal(settlementAction);
        if (refusal == null || !Limits(settlement))
            return allowed;

        disableOption = true;
        disabledText = refusal;
        return false;
    }

    private bool Limits(Settlement settlement)
    {
        var playerFaction = Hero.MainHero?.MapFaction;
        var settlementFaction = settlement.MapFaction;
        if (playerFaction == null || settlementFaction == null)
            return false;

        return _service.LimitsPlayerAccess(
            playerFaction.StringId,
            settlementFaction.StringId,
            FactionManager.IsAtWarAgainstFaction(playerFaction, settlementFaction),
            playerOwnsSettlement: settlement.OwnerClan == Clan.PlayerClan);
    }
}
