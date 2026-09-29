using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using static TAOM.Features.CreatureBandits.Diagnostics.CreatureDiagFormat;

namespace TAOM.Features.CreatureBandits.Diagnostics;

/// <summary>
/// The broods' and troll bands' battle and destruction lines (#692, #694; the party ids tell them apart): who fought
/// one, what each side lost, and who destroyed one.
/// Listeners only, no save data; main-thread campaign events. Temporary, strip after sign-off with the folder and its
/// line in <see cref="CreatureBanditsModule"/>.
/// </summary>
internal sealed class CreatureBroodCampaignDiagBehavior : CampaignBehaviorBase
{
    public override void RegisterEvents()
    {
        CampaignEvents.MapEventStarted.AddNonSerializedListener(this, OnMapEventStarted);
        CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
        CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, OnMobilePartyDestroyed);
    }

    public override void SyncData(IDataStore dataStore) { }

    private static double Day => CreatureBroodCampaignDiag.Day;

    private static bool InvolvesBrood(MapEvent mapEvent) =>
        mapEvent.InvolvedParties.Any(p => CreatureBanditRules.IsCreatureBandClan(p.MobileParty?.ActualClan?.StringId));

    private static string Side(MapEvent mapEvent, BattleSideEnum side) =>
        string.Join("+", mapEvent.PartiesOnSide(side).Select(p =>
            (p.Party.MobileParty?.StringId ?? p.Party.Name?.ToString() ?? "?") + ":" + I(p.Party.MemberRoster.TotalManCount)));

    private void OnMapEventStarted(MapEvent mapEvent, PartyBase attacker, PartyBase defender) =>
        CreatureBroodCampaignDiag.Guard("battle-start", () =>
        {
            if (!InvolvesBrood(mapEvent)) return;
            CreatureBanditDiag.Logger?.LogInfo(CampaignLine("brood-battle-start", Day, "type", mapEvent.EventType.ToString(),
                "field", B(mapEvent.IsFieldBattle), "player", B(mapEvent.IsPlayerMapEvent),
                "attackers", Side(mapEvent, BattleSideEnum.Attacker), "defenders", Side(mapEvent, BattleSideEnum.Defender)));
        });

    private void OnMapEventEnded(MapEvent mapEvent) =>
        CreatureBroodCampaignDiag.Guard("battle-end", () =>
        {
            if (!InvolvesBrood(mapEvent)) return;
            string Losses(BattleSideEnum side) => string.Join("+", mapEvent.PartiesOnSide(side).Select(p =>
                (p.Party.MobileParty?.StringId ?? "?") + ":died" + I(p.DiedInBattle.TotalManCount) + "/wounded" + I(p.WoundedInBattle.TotalManCount)));
            CreatureBanditDiag.Logger?.LogInfo(CampaignLine("brood-battle-end", Day, "winner", mapEvent.WinningSide.ToString(),
                "player", B(mapEvent.IsPlayerMapEvent), "simulated", B(!mapEvent.IsPlayerMapEvent || mapEvent.IsPlayerSimulation),
                "attackerLosses", Losses(BattleSideEnum.Attacker), "defenderLosses", Losses(BattleSideEnum.Defender),
                "prisonersRefused", I(CreatureBroodCampaignDiag.PrisonersRefused)));
        });

    private void OnMobilePartyDestroyed(MobileParty party, PartyBase destroyer) =>
        CreatureBroodCampaignDiag.Guard("destroyed", () =>
        {
            if (!CreatureBanditRules.IsCreatureBandClan(party?.ActualClan?.StringId)) return;
            CreatureBanditDiag.Logger?.LogInfo(CampaignLine("brood-destroyed", Day, "party", party!.StringId,
                "by", destroyer?.MobileParty?.StringId ?? destroyer?.Name?.ToString() ?? "-"));
        });
}
