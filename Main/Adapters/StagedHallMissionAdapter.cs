using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Features.StagedSiege.Domain;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Objects;
using TaleWorlds.MountAndBlade.Source.Missions.Handlers;

namespace TAOM.Adapters;

/// <summary>
/// The lord's hall fight for the staged siege (docs/features/staged-siege.md "The lord's hall"), over the hall controller, the fight
/// area markers, the team agents and one flag (v1.5.6). One per mission, built by the mission behavior inside <c>EarlyStart</c>'s try
/// and used on the main thread only. Reads throw on an engine fault and the service catches them. Members referenced here are
/// pinned by <c>StagedSiegeBindingTests</c> for the reason given on <see cref="StagedSiegeMissionAdapter"/>.
/// </summary>
public sealed class StagedHallMissionAdapter : IStagedHallAdapter
{
    private const string FlagPrefab = "flag_pole_big_sergeant_E";

    private readonly Mission _mission;
    private FlagCapturePoint? _flag;
    private bool _spawnersStopped;

    public StagedHallMissionAdapter(Mission mission) =>
        _mission = mission ?? throw new ArgumentNullException(nameof(mission));

    public object MissionToken => _mission;

    public float Time => _mission.CurrentTime;

    // The hall fight is the one mission that carries this controller; it is also the spawn logic the capture stops.
    public bool IsHallMission => _mission.GetMissionBehavior<LordsHallFightMissionController>() != null;

    public bool HasCampaign => Campaign.Current != null;

    public bool IsClientOrReplay => GameNetwork.IsClientOrReplay;

    // PlayerEncounter.Current dereferences Campaign.Current without a check, so every campaign read is guarded first.
    private static Settlement? EncounterSettlement => Campaign.Current == null ? null : PlayerEncounter.EncounterSettlement;

    public bool IsInLordsHall => EncounterSettlement?.CurrentSiegeState == Settlement.SiegeState.InTheLordsHall;

    // The same call the player's assault makes for the walls scene: PlayerSiege.StartSiegeMission (v1.5.6 PlayerSiege.cs:171-172);
    // the sally-out opener PlayerEncounter.StartSiegeAmbushMission (:1692-1693) uses the same expression.
    public string WallsSceneName
    {
        get
        {
            var settlement = EncounterSettlement;
            var town = settlement?.Town;
            var center = settlement?.LocationComplex?.GetLocationWithId("center");
            return town == null || center == null ? string.Empty : center.GetSceneName(town.GetWallLevel()) ?? string.Empty;
        }
    }

    public string EncounterSettlementId => EncounterSettlement?.StringId ?? string.Empty;

    public IReadOnlyList<HallMarker> ReadMarkers()
    {
        var markers = new List<HallMarker>();
        foreach (var marker in _mission.ActiveMissionObjects.FindAllWithType<FightAreaMarker>().ToList())
        {
            var at = marker.GetPosition();
            markers.Add(new HallMarker(marker.AreaIndex, at.x, at.y, at.z, marker.AreaRadius));
        }

        return markers;
    }

    public int SpawnFlag(StagePoint centre)
    {
        var rotation = Mat3.Identity;
        rotation.f = new Vec3(0f, 1f, 0f);
        rotation.u = Vec3.Up;
        rotation.Orthonormalize();
        var frame = new MatrixFrame(rotation, new Vec3(centre.X, centre.Y, centre.Z));
        _flag = _mission.CreateMissionObjectFromPrefab(FlagPrefab, frame, false, 0f, _ => { }) as FlagCapturePoint;
        return _flag != null ? 1 : 0;
    }

    public void ColourFlag(StagedSide owner)
    {
        var team = owner == StagedSide.Attacker ? _mission.AttackerTeam : _mission.DefenderTeam;
        if (_flag != null && team != null)
            _flag.SetTeamColorsWithAllSynched(team.Color, team.Color2);
    }

    public ZoneCount CountInHall(HallZone zone)
    {
        int attackers = 0, defenders = 0;
        foreach (var team in _mission.Teams)
        {
            if (team == null || (team.Side != BattleSideEnum.Attacker && team.Side != BattleSideEnum.Defender))
                continue;
            var inside = 0;
            var agents = team.ActiveAgents;
            for (var i = 0; i < agents.Count; i++)
            {
                var agent = agents[i];
                if (agent == null || !agent.IsHuman || !agent.IsActive() || agent.IsRunningAway)
                    continue;
                var at = agent.Position;
                if (zone.Contains(at.x, at.y, at.z))
                    inside++;
            }

            if (team.Side == BattleSideEnum.Attacker)
                attackers += inside;
            else
                defenders += inside;
        }

        return new ZoneCount(attackers, defenders);
    }

    public int CountStanding(StagedSide side)
    {
        var engineSide = side == StagedSide.Attacker ? BattleSideEnum.Attacker : BattleSideEnum.Defender;
        var standing = 0;
        foreach (var team in _mission.Teams)
        {
            if (team == null || team.Side != engineSide)
                continue;
            var agents = team.ActiveAgents;
            for (var i = 0; i < agents.Count; i++)
            {
                var agent = agents[i];
                if (agent != null && agent.IsHuman && agent.IsActive() && !agent.IsRunningAway)
                    standing++;
            }
        }

        return standing;
    }

    public void StopSpawners()
    {
        if (_spawnersStopped)
            return;
        _spawnersStopped = true;
        var spawn = _mission.GetMissionBehavior<LordsHallFightMissionController>();
        spawn?.StopSpawner(BattleSideEnum.Attacker);
        spawn?.StopSpawner(BattleSideEnum.Defender);
    }

    // BattleEndLogic shows its win line and its "press Tab to view results" hint only for a depleted enemy, which a hall capture is
    // not, and the mission does not close by itself. So: the chat log line, then the same quick-information channel and hint lines
    // vanilla builds (v1.5.6 BattleEndLogic :110-111, :138), with the leave key set the same way.
    public void AnnounceHallTaken()
    {
        var taken = new TextObject("{=taom_staged_siege_hall_taken}The lord's hall is taken.");
        InformationManager.DisplayMessage(new InformationMessage(taken.ToString()));
        MBInformationManager.AddQuickInformation(taken);
        GameTexts.SetVariable("leave_key", HyperlinkTexts.GetKeyHyperlinkText(HotKeyManager.GetHotKeyId("Generic", 4)));
        MBInformationManager.AddQuickInformation(GameTexts.FindText("str_battle_won_press_tab_to_view_results"));
    }
}
