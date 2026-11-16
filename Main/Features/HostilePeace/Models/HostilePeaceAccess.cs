using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.Localization;

namespace TAOM.Features.HostilePeace.Models;

/// <summary>
/// What the player gets at a Hostile realm's settlement during peace: exactly the base game's war answer
/// (<c>DefaultSettlementAccessModel</c>, v1.5.7, read on the installed DLL 2026-11-15). A town gives limited access through a
/// disguise (<c>CanMainHeroEnterTown</c>, war branch); a castle gives none, by request (<c>CanMainHeroEnterCastle</c>, war branch);
/// a village can be entered but refuses trade, recruits and rest (<c>CanMainHeroTrade</c>, <c>CanMainHeroRecruitTroops</c>,
/// <c>CanMainHeroWaitInSettlement</c>, with the same texts). The menus read only these values: the sneak-in option is
/// <c>LimitedAccessSolution == Disguise</c> and the closed-gate text is <c>AccessLimitationReason.HostileFaction</c>
/// (<c>EncounterGameMenuBehavior</c>). Vanilla already uses a disguise at peace for a town where the player has a crime rating,
/// so a caught sneak at peace is an existing base-game path (crime rating, then captivity).
/// </summary>
internal static class HostilePeaceAccess
{
    internal static SettlementAccessModel.AccessDetails AsAtWar(bool isTown) =>
        isTown
            ? new SettlementAccessModel.AccessDetails
            {
                AccessLevel = SettlementAccessModel.AccessLevel.LimitedAccess,
                LimitedAccessSolution = SettlementAccessModel.LimitedAccessSolution.Disguise,
                AccessLimitationReason = SettlementAccessModel.AccessLimitationReason.HostileFaction,
            }
            : new SettlementAccessModel.AccessDetails
            {
                AccessLevel = SettlementAccessModel.AccessLevel.NoAccess,
                AccessMethod = SettlementAccessModel.AccessMethod.ByRequest,
                AccessLimitationReason = SettlementAccessModel.AccessLimitationReason.HostileFaction,
            };

    /// <summary>
    /// The keep's answer at war (<c>CanMainHeroEnterKeepInternal</c>, war branch): limited access by disguise, reason Disguised.
    /// A castle's "Request entry to the castle" grants entry only for full access or a bribe, so this lands on "entry denied".
    /// </summary>
    internal static SettlementAccessModel.AccessDetails KeepAsAtWar() =>
        new SettlementAccessModel.AccessDetails
        {
            AccessLevel = SettlementAccessModel.AccessLevel.LimitedAccess,
            LimitedAccessSolution = SettlementAccessModel.LimitedAccessSolution.Disguise,
            AccessLimitationReason = SettlementAccessModel.AccessLimitationReason.Disguised,
        };

    /// <summary>The base game's refusal for this village action at war, or null for an action war does not refuse.</summary>
    internal static TextObject? VillageRefusal(SettlementAccessModel.SettlementAction action)
    {
        switch (action)
        {
            case SettlementAccessModel.SettlementAction.Trade:
                return HostilePeaceTexts.HostileVillageTrade();
            case SettlementAccessModel.SettlementAction.RecruitTroops:
                return HostilePeaceTexts.HostileVillageRecruit();
            case SettlementAccessModel.SettlementAction.WaitInSettlement:
                return HostilePeaceTexts.HostileVillageWait();
            default:
                return null;
        }
    }
}
