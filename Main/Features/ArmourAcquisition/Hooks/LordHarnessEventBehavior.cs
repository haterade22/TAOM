using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;

namespace TAOM.Features.ArmourAcquisition.Hooks;

/// <summary>
/// Thin boundary for "A Lord's Harness Unclaimed" (ADR-002): after the player's battle ends, reads the enemy
/// lords who led parties on the losing side and asks <see cref="LordHarnessService.RollHarnessFind"/>, which
/// rolls, picks the lord and his culture's lord piece, and stamps the cooldown. The player takes it, or sends
/// it back to his kin for his goodwill. Main hero, authority only.
/// </summary>
public sealed class LordHarnessEventBehavior : CampaignBehaviorBase
{
    private readonly LordHarnessService _harness;
    private readonly IArmourGateService _gate;
    private readonly IArmourAcquisitionConfigProvider _config;
    private readonly IArmourAcquisitionSettingsProvider _settings;
    private readonly IArmouryTownAdapter _towns;
    private readonly IArmouryPlayerAdapter _player;
    private readonly ICoopSessionProvider _coop;
    private readonly IDedicatedServerProvider _server;
    private readonly IModLogger _logger;
    private readonly Random _rng = new();

    public LordHarnessEventBehavior(LordHarnessService harness, IArmourGateService gate, IArmourAcquisitionConfigProvider config,
        IArmourAcquisitionSettingsProvider settings, IArmouryTownAdapter towns, IArmouryPlayerAdapter player,
        ICoopSessionProvider coop, IDedicatedServerProvider server, IModLogger logger)
    {
        _harness = harness;
        _gate = gate;
        _config = config;
        _settings = settings;
        _towns = towns;
        _player = player;
        _coop = coop;
        _server = server;
        _logger = logger;
    }

    public override void RegisterEvents() =>
        CampaignEvents.OnPlayerBattleEndEvent.AddNonSerializedListener(this, OnPlayerBattleEnd);

    public override void SyncData(IDataStore dataStore)
    {
    }

    private void OnPlayerBattleEnd(MapEvent mapEvent)
    {
        if (mapEvent == null || !_gate.IsActive || !_settings.LordEventEnabled || _coop.ShouldDeferToHost || _server.IsDedicatedServer)
            return;
        var won = mapEvent.WinningSide != BattleSideEnum.None && mapEvent.WinningSide == mapEvent.PlayerSide;
        var lords = won ? DefeatedLords(mapEvent) : new List<Hero>();
        var find = _harness.RollHarnessFind(_player.HeroId, _towns.Today, won, lords.Select(l => l.Culture?.StringId).ToList(), _rng);
        if (find == null)
            return;

        var lord = lords[find.Value.LordIndex];
        var itemId = find.Value.ItemId;
        _logger.LogInfo($"[ArmourAcquisition] Lord's harness event: {itemId} from {lord.StringId}'s household.");
        InformationManager.ShowInquiry(new InquiryData(
            new TextObject("{=taom_lh_event_title}A Lord's Harness Unclaimed").ToString(),
            new TextObject("{=taom_lh_event_body}Among the spoils of the field lies {ITEM}, the harness of {LORD}'s household. It is yours by right of arms, though his kin would take its return as a courtesy.")
                .SetTextVariable("ITEM", _gate.GetName(itemId)).SetTextVariable("LORD", lord.Name).ToString(),
            true, true, new TextObject("{=taom_lh_event_take}Take it").ToString(), new TextObject("{=taom_lh_event_return}Send it to his kin").ToString(),
            () => _player.AddPiece(itemId, null, 1),
            () => _player.ChangeRelationWith(lord.StringId, _config.GetConfig().LordEventLeaveRelation)), pauseGameActiveState: true);
    }

    private static List<Hero> DefeatedLords(MapEvent mapEvent)
    {
        var lords = new List<Hero>();
        var side = mapEvent.GetMapEventSide(mapEvent.DefeatedSide);
        if (side == null)
            return lords;
        foreach (var party in side.Parties)
        {
            var leader = party?.Party?.LeaderHero;
            if (leader != null && leader.IsLord && !lords.Contains(leader))
                lords.Add(leader);
        }
        return lords;
    }
}
