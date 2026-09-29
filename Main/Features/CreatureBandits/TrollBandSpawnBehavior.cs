using System;
using System.Linq;
using TAOM.Core.Logging;
using TAOM.Features.CreatureBandits.Diagnostics;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace TAOM.Features.CreatureBandits;

/// <summary>
/// Keeps about one troll band per kingdom (#694). The troll clan is a looter faction like the spider brood's, so
/// <c>TaomBanditDensityModel</c> caps vanilla's map-wide spawn at zero and this behavior spawns instead: each day, while
/// there are fewer bands than living kingdoms (not eliminated, owning a town, castle or village), one band near a random
/// settlement of a kingdom that has none, told to patrol it (<see cref="CreatureBandParties"/>; the rule is
/// <see cref="CreatureBanditRules.KingdomsOwedATrollBand"/>). A band counts for its home settlement's current kingdom,
/// so a band whose settlement changes hands moves with it, and its old kingdom gets a new one once the total drops
/// below the cap.
///
/// A band spawns with its template's two to four trolls, and stays trolls only: no troll is ever a prisoner to free
/// back, and <c>TaomBattleRewardModel</c> gives it no freed prisoner. No save data: the counts are read from the clan
/// and the kingdoms each day. The clan is defined in XML, which the engine reads only for a new campaign, so an older save
/// without it spawns nothing. Every campaign hook here is main-thread.
/// </summary>
internal sealed class TrollBandSpawnBehavior : CampaignBehaviorBase
{
    private readonly IModLogger _logger;
    // Per campaign: the feature module builds a new behavior at every game start.
    private bool _missingClanLogged;

    public TrollBandSpawnBehavior(IModLogger logger) => _logger = logger;

    public override void RegisterEvents() => CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);

    public override void SyncData(IDataStore dataStore) { }

    private static Clan? TrollClan => Clan.All.FirstOrDefault(c => CreatureBanditRules.IsTrollBandClan(c.StringId));

    // The MCM switch, read each day. It gates new bands only: the looter cap of 0 (TaomBanditDensityModel) stays, or
    // vanilla would spawn the clan map-wide.
    private static bool SpawnEnabled => TaomSettings.Instance?.CreatureBanditSpawnTrollBands ?? CreatureBanditsConfig.DefaultSpawnTrollBands;

    private void OnDailyTick()
    {
        try
        {
            var clan = TrollClan;
            if (clan?.DefaultPartyTemplate == null)
            {
                if (!_missingClanLogged)
                    _logger.LogInfo($"[CreatureBandits] No '{CreatureBanditsConfig.TrollClanId}' clan with a party template in this campaign (a save from before #694?); no troll bands spawn.");
                _missingClanLogged = true;
                return;
            }

            int strays = CreatureBandParties.ReturnStraysToPatrol(clan);
            // Kingdom.Settlements holds only towns, castles and their villages (Kingdom.OnFortificationAdded,
            // OnBoundVillageAdded), so any settlement is an anchor. A band counts by kingdom id: Clan.MapFaction is the
            // clan itself when it has no kingdom.
            var living = Kingdom.All.Where(k => !k.IsEliminated && k.Settlements.Count > 0).ToList();
            var bandKingdoms = clan.WarPartyComponents
                .Select(c => (c.MobileParty?.HomeSettlement?.MapFaction as Kingdom)?.StringId).ToList();
            var owed = CreatureBanditRules.KingdomsOwedATrollBand(living.Select(k => k.StringId).ToList(), bandKingdoms, SpawnEnabled);
            CreatureBroodCampaignDiag.TrollCensus(clan, living.Count, owed.Count, strays);
            if (owed.Count > 0)
            {
                string kingdomId = owed[MBRandom.RandomInt(owed.Count)];
                var settlements = living.First(k => k.StringId == kingdomId).Settlements;
                CreatureBandParties.Spawn(clan, settlements[MBRandom.RandomInt(settlements.Count)]);
            }
        }
        catch (Exception e)
        {
            _logger.LogError($"[CreatureBandits] Daily troll band tick failed: {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
        }
    }
}
