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
using TaleWorlds.MountAndBlade.MissionSpawnHandlers;
using TaleWorlds.MountAndBlade.Objects;

namespace TAOM.Adapters;

/// <summary>
/// The wall battle for the staged siege (docs/features/staged-siege.md), over the spawn logic, the zone counts, the flags, the
/// gates, the defender formations and the team AI (v1.5.6). One per mission, built by the mission behavior inside
/// <c>EarlyStart</c>'s try and used on the main thread only. Reads throw on an engine fault and the service catches them (its
/// fail-safe), so nothing here swallows. Members referenced here are pinned by <c>StagedSiegeBindingTests</c>, because a member
/// that stops resolving fails when the method that references it is first compiled, before any try inside that method can run.
/// </summary>
public sealed class StagedSiegeMissionAdapter : IStagedSiegeMissionAdapter
{
    private const string FlagPrefab = "flag_pole_big_sergeant_";
    private const int PointCount = 4;

    private readonly Mission _mission;
    private readonly Func<Team, TacticComponent>? _tacticFactory;
    private readonly MBList<Agent> _nearby = new();
    private readonly FlagCapturePoint?[] _flags = new FlagCapturePoint?[PointCount];
    private readonly List<IStagedTacticProbe> _probes = new();

    private DefaultBattleMissionAgentSpawnLogic? _spawnLogic;
    private Formation? _detachment;
    private bool _detachmentDriven;
    private bool _detachmentHeld;

    public StagedSiegeMissionAdapter(Mission mission, Func<Team, TacticComponent>? tacticFactory)
    {
        _mission = mission ?? throw new ArgumentNullException(nameof(mission));
        _tacticFactory = tacticFactory;
    }

    public object MissionToken => _mission;

    public string SceneName => _mission.SceneName ?? string.Empty;

    public float Time => _mission.CurrentTime;

    public bool IsSiegeBattle => _mission.IsSiegeBattle;

    // A sally-out and a relief force both carry a controller derived from this one.
    public bool HasSallyOutController => _mission.GetMissionBehavior<SallyOutMissionController>() != null;

    public bool IsClientOrReplay => GameNetwork.IsClientOrReplay;

    public bool IsCampaignWallsAssault =>
        Campaign.Current != null && PlayerEncounter.EncounterSettlement?.CurrentSiegeState == Settlement.SiegeState.OnTheWalls;

    public bool IsCustomBattleSiege =>
        Campaign.Current == null && _mission.IsSiegeBattle && _mission.GetMissionBehavior<CustomSiegeMissionSpawnHandler>() != null;

    public string EncounterSettlementId => Campaign.Current == null ? string.Empty : PlayerEncounter.EncounterSettlement?.StringId ?? string.Empty;

    public string SiegeStateName
    {
        get
        {
            if (Campaign.Current == null)
                return "no campaign";
            var settlement = PlayerEncounter.EncounterSettlement;
            return settlement == null ? "no encounter settlement" : settlement.CurrentSiegeState.ToString();
        }
    }

    private DefaultBattleMissionAgentSpawnLogic? SpawnLogic =>
        _spawnLogic ??= _mission.GetMissionBehavior<DefaultBattleMissionAgentSpawnLogic>();

    public StagedSide? PlayerSide
    {
        get
        {
            var side = SpawnLogic?.PlayerSide;
            return side == BattleSideEnum.Attacker ? StagedSide.Attacker
                : side == BattleSideEnum.Defender ? StagedSide.Defender
                : (StagedSide?)null;
        }
    }

    public bool IsDeploymentFinished => _mission.IsDeploymentFinished;

    public SpawnCounts ReadSpawnCounts(StagedSide side)
    {
        var logic = SpawnLogic;
        if (logic == null)
            return default;
        return side == StagedSide.Attacker
            ? new SpawnCounts(logic.NumberOfActiveAttackerTroops, logic.NumberOfRemainingAttackerTroops)
            : new SpawnCounts(logic.NumberOfActiveDefenderTroops, logic.NumberOfRemainingDefenderTroops);
    }

    public int CountStanding(StagedSide side)
    {
        // Every team of the side, like vanilla's rout check (BattleEndLogic walks each friendly or enemy team): a campaign
        // siege can field an ally team beside the main one.
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

    public bool SpawnSettingsAreStaged
    {
        get
        {
            var logic = SpawnLogic;
            if (logic == null)
                return false;
            var settings = logic.SpawnSettings;
            return settings.ReinforcementTroopsSpawnMethod == MissionSpawnSettings.ReinforcementSpawnMethod.Balanced
                && settings.ReinforcementTroopsTimingMethod == MissionSpawnSettings.ReinforcementTimingMethod.CustomTimer;
        }
    }

    // --- zone counts, flags, gates --------------------------------------------------------------------------------------

    public ZoneCount CountInZone(StagePoint center, float radius, float heightBand)
    {
        _mission.GetNearbyAgents(new Vec2(center.X, center.Y), radius, _nearby);
        var radiusSquared = radius * radius;
        int attackers = 0, defenders = 0;
        for (var i = 0; i < _nearby.Count; i++)
        {
            var agent = _nearby[i];
            if (agent == null || !agent.IsHuman || !agent.IsActive() || agent.IsRunningAway || agent.Team == null)
                continue;
            var position = agent.Position;
            var dx = position.x - center.X;
            var dy = position.y - center.Y;
            // Negated so a NaN position (a comparison with NaN is false) is skipped, not counted.
            if (!(dx * dx + dy * dy <= radiusSquared) || !(Math.Abs(position.z - center.Z) <= heightBand))
                continue;
            if (agent.Team.Side == BattleSideEnum.Attacker)
                attackers++;
            else if (agent.Team.Side == BattleSideEnum.Defender)
                defenders++;
        }

        return new ZoneCount(attackers, defenders);
    }

    public int SpawnFlags(IReadOnlyList<StageZone> zones)
    {
        var made = 0;
        for (var i = 0; i < zones.Count; i++)
        {
            var zone = zones[i];
            var rotation = Mat3.Identity;
            rotation.f = new Vec3(0f, 1f, 0f);
            rotation.u = Vec3.Up;
            rotation.Orthonormalize();
            var frame = new MatrixFrame(rotation, new Vec3(zone.Center.X, zone.Center.Y, zone.Center.Z));
            var letter = (char)('A' + (int)zone.Id);
            var created = _mission.CreateMissionObjectFromPrefab(FlagPrefab + letter, frame, false, 0f, _ => { });
            var flag = created as FlagCapturePoint;
            _flags[(int)zone.Id] = flag;
            if (flag != null)
                made++;
        }

        return made;
    }

    public void ColourFlag(StageId stage, StagedSide owner)
    {
        var flag = _flags[(int)stage];
        var team = owner == StagedSide.Attacker ? _mission.AttackerTeam : _mission.DefenderTeam;
        if (flag != null && team != null)
            flag.SetTeamColorsWithAllSynched(team.Color, team.Color2);
    }

    public int OpenGates()
    {
        var opened = 0;
        foreach (var gate in _mission.ActiveMissionObjects.FindAllWithType<CastleGate>().ToList())
        {
            if (gate.IsDestroyed || gate.IsDisabled)
                continue;
            gate.OpenDoor();
            opened++;
        }

        return opened;
    }

    // --- the defender detachment ----------------------------------------------------------------------------------------

    // Infantry formations of the defenders that do not hold the player and that the AI may split: TransferUnits (isPlayerOrder
// false) returns at once for a formation a player general or sergeant owns, so a player-led defence forms no detachment.
    private IEnumerable<Formation> EligibleInfantry()
    {
        var team = _mission.DefenderTeam;
        if (team == null)
            yield break;
        var formations = team.FormationsIncludingEmpty;
        for (var i = 0; i < formations.Count; i++)
        {
            var formation = formations[i];
            if (formation.CountOfUnits > 0 && formation.QuerySystem.IsInfantryFormation
                && !formation.IsPlayerTroopInFormation && !formation.HasPlayerControlledTroop && formation.IsSplittableByAI)
                yield return formation;
        }
    }

    public int ActiveDefenderInfantry() => EligibleInfantry().Sum(f => f.CountOfUnits);

    public string DetachmentNote { get; private set; } = string.Empty;

    private Formation? EmptyTarget()
    {
        var team = _mission.DefenderTeam;
        foreach (var formationClass in new[] { FormationClass.Skirmisher, FormationClass.HeavyInfantry, FormationClass.LightCavalry, FormationClass.HeavyCavalry })
        {
            var formation = team.GetFormation(formationClass);
            if (formation != null && formation.CountOfUnits == 0)
                return formation;
        }

        return null;
    }

    public DetachmentReading CreateDetachment(int count, IReadOnlyList<StagePoint> slots, StagePoint holdAt, StageFacing facing)
    {
        var target = EmptyTarget();
        if (target == null)
        {
            DetachmentNote = "no empty formation to receive it";
            return default;
        }

        var wanted = count;
        var sourceCount = 0;
        foreach (var source in EligibleInfantry().OrderByDescending(f => f.CountOfUnits).ToList())
        {
            if (wanted <= 0)
                break;
            var take = Math.Min(wanted, source.CountOfUnits);
            source.TransferUnits(target, take);
            wanted -= take;
            sourceCount++;
        }

        _detachment = target;
        _detachmentHeld = true;
        _detachmentDriven = target.IsAIControlled;
        DetachmentNote = $"from {sourceCount} formation(s) into {target.FormationIndex}";
        if (target.CountOfUnits <= 0)
            return default;
        if (_detachmentDriven)
            target.SetControlledByAI(false);

        var slot = 0;
        target.ApplyActionOnEachUnit(agent =>
        {
            if (slot < slots.Count)
                agent.TeleportToPosition(new Vec3(slots[slot].X, slots[slot].Y, slots[slot].Z));
            slot++;
        });
        IssueMove(target, holdAt, facing);
        return Read(target);
    }

    public DetachmentReading ReadDetachment() => _detachment == null ? default : Read(_detachment);

    private DetachmentReading Read(Formation formation)
    {
        var average = formation.CachedAveragePosition;
        return new DetachmentReading(formation.CountOfUnits, _detachmentDriven, average.x, average.y);
    }

    public void OrderDetachment(DetachmentOrder order, StagePoint at, StageFacing facing)
    {
        if (_detachment == null || !_detachmentDriven)
            return;
        if (order == DetachmentOrder.Charge)
            _detachment.SetMovementOrder(MovementOrder.MovementOrderCharge);
        else
            IssueMove(_detachment, at, facing);
    }

    private void IssueMove(Formation formation, StagePoint at, StageFacing facing)
    {
        formation.SetMovementOrder(MovementOrder.MovementOrderMove(Position(at)));
        formation.SetFacingOrder(FacingOrder.FacingOrderLookAtDirection(new Vec2(facing.X, facing.Y)));
    }

    public void ReleaseDetachment()
    {
        if (_detachment == null || !_detachmentHeld)
            return;
        _detachmentHeld = false;
        if (_detachmentDriven)
            _detachment.SetControlledByAI(true);
    }

    private WorldPosition Position(StagePoint p) =>
        new WorldPosition(_mission.Scene, UIntPtr.Zero, new Vec3(p.X, p.Y, p.Z), false);

    // --- the team AI ---------------------------------------------------------------------------------------------------

    private IEnumerable<Team> AiTeams()
    {
        foreach (var team in _mission.Teams)
        {
            if (team.HasTeamAi && (team.Side == BattleSideEnum.Attacker || team.Side == BattleSideEnum.Defender))
                yield return team;
        }
    }

    public int InstallTactics()
    {
        if (_tacticFactory == null)
            return 0;
        _probes.Clear();
        var installed = 0;
        foreach (var team in AiTeams())
        {
            var tactic = _tacticFactory(team);
            team.AddTacticOption(tactic);
            if (tactic is IStagedTacticProbe probe)
                _probes.Add(probe);
            installed++;
        }

        return installed;
    }

    public void ResetTactics()
    {
        foreach (var team in AiTeams())
            team.ResetTactic();
    }

    public int EnsureDefendBehaviors()
    {
        var added = 0;
        foreach (var team in AiTeams())
        {
            var formations = team.FormationsIncludingEmpty;
            for (var i = 0; i < formations.Count; i++)
            {
                var formation = formations[i];
                if (formation.CountOfUnits <= 0 || formation.AI.GetBehavior<BehaviorDefend>() != null)
                    continue;
                formation.AI.AddAiBehavior(new BehaviorDefend(formation));
                added++;
            }
        }

        return added;
    }

    public string DescribeTactics()
    {
        if (_probes.Count == 0)
            return "none installed";
        return string.Join("; ", _probes.Select(p => $"{p.Side} current {p.IsCurrent} {p.Status}"));
    }

    // BattleEndLogic shows these two lines only for a depleted enemy, which a captured D is not (v1.5.6 :110-111, :138), and the
    // mission does not close by itself. Vanilla's own texts, the leave key set the way vanilla sets it.
    public void AnnounceVictory()
    {
        MBInformationManager.AddQuickInformation(GameTexts.FindText("str_battle_won"));
        GameTexts.SetVariable("leave_key", HyperlinkTexts.GetKeyHyperlinkText(HotKeyManager.GetHotKeyId("Generic", 4)));
        MBInformationManager.AddQuickInformation(GameTexts.FindText("str_battle_won_press_tab_to_view_results"));
    }
}
