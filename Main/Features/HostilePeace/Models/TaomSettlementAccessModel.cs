using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace TAOM.Features.HostilePeace.Models;

/// <summary>
/// Block C: the player meets a Hostile realm's settlements during peace as the base game does at war
/// (<see cref="HostilePeaceAccess"/>). It runs the base model first and changes only its answer, so crime, disguise, clan tier and
/// the player's own fiefs keep the base rules. What it leaves alone:
/// <list type="bullet">
/// <item>A settlement the player's clan owns, and any of the player's own realm (never a Hostile pair).</item>
/// <item>The settlement the player's party is in right now: switching the rule on, or a check the menus repeat while inside, never
/// strands the player in a town. Village actions are the exception, as at war they are refused from inside.</item>
/// <item>The keep and the dungeon (<c>CanMainHeroEnterLordsHall</c>, <c>CanMainHeroEnterDungeon</c>): reached only from inside a
/// town, where the base model already treats a disguised player as at war.</item>
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
        if (Limits(settlement, entering: true))
            accessDetails = HostilePeaceAccess.AsAtWar(settlement.IsTown);
    }

    public override bool CanMainHeroDoSettlementAction(Settlement settlement, SettlementAction settlementAction,
        out bool disableOption, out TextObject disabledText)
    {
        var allowed = base.CanMainHeroDoSettlementAction(settlement, settlementAction, out disableOption, out disabledText);
        if (!allowed || settlement == null || !settlement.IsVillage)
            return allowed;

        var refusal = HostilePeaceAccess.VillageRefusal(settlementAction);
        if (refusal == null || !Limits(settlement, entering: false))
            return allowed;

        disableOption = true;
        disabledText = refusal;
        return false;
    }

    private bool Limits(Settlement settlement, bool entering)
    {
        var playerFaction = Hero.MainHero?.MapFaction;
        var settlementFaction = settlement.MapFaction;
        if (playerFaction == null || settlementFaction == null)
            return false;

        return _service.LimitsPlayerAccess(
            playerFaction.StringId,
            settlementFaction.StringId,
            FactionManager.IsAtWarAgainstFaction(playerFaction, settlementFaction),
            playerOwnsSettlement: settlement.OwnerClan == Clan.PlayerClan,
            playerIsInside: entering && MobileParty.MainParty?.CurrentSettlement == settlement);
    }
}
