using System;
using System.Collections.Generic;
using System.Linq;
using SandBox.Conversation.MissionLogics;
using SandBox.Missions.MissionLogics.Arena;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.AgentOrigins;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.ElephantLike;
using TAOM.Features.TrainingGrounds.Waves;
using TAOM.Features.TrainingGrounds.Waves.Domain;
using TAOM.Features.TrainingGrounds.Waves.Hooks;

namespace TAOM.Adapters;

/// <summary>
/// The training yard of one mission (docs/features/training-waves.md), the only place of the wave fight that spawns or fades an agent.
/// It runs beside vanilla's <see cref="ArenaPracticeFightMissionController"/>, which keeps six practice fighters in the yard for as long
/// as nobody fights: a wave first stands that controller down (its fighters faded, its spawn count set to its cap so its tick spawns
/// nothing, ArenaPracticeFightMissionController.cs :197 and :203), and the way back runs the controller's own reset and its own spawn of
/// the player beside the master (:223 and :224), through <see cref="ArenaPracticeControllerAccess"/>. The wave's fighters are built as
/// vanilla builds its own (a <see cref="SimpleAgentOrigin"/>, no horse, the practice kit) but on a team of this adapter's, hostile to
/// the player's team only. A fighter handle is held only inside the mission and checked with <see cref="AgentSlotIdentity"/> before it
/// is read or faded. Every method that writes is called from the mission tick.
/// </summary>
internal sealed class ArenaWaveArena : IArenaWaveArena
{
    private const string Tag = "[TrainingWaves]";
    private const string InitialFrameTag = "sp_arena";
    private const string RespawnFrameTag = "sp_arena_respawn";
    private const int VanillaSpawnCap = 30;
    private const int FirstArmourSlot = 5;
    private const int LastArmourSlot = 9;
    private const int LastWeaponSlot = 3;

    private readonly Mission _mission;
    private readonly ArenaPracticeFightMissionController _controller;
    private readonly IModLogger _logger;
    private readonly List<Agent> _fighters = new();
    private List<MatrixFrame>? _initialFrames;
    private List<MatrixFrame>? _respawnFrames;
    private Team? _waveTeam;
    private int _turn;

    public ArenaWaveArena(Mission mission, ArenaPracticeFightMissionController controller, IModLogger logger)
    {
        _mission = mission;
        _controller = controller;
        _logger = logger;
    }

    public int FrameCount => InitialFrames.Count;

    public bool InConversation => Campaign.Current?.ConversationManager?.IsConversationInProgress == true;

    public bool MissionEnded => _mission.MissionEnded || _mission.IsMissionEnding;

    public bool PlayerDown
    {
        get
        {
            var main = _mission.MainAgent;
            return main == null || !main.IsActive();
        }
    }

    public int ActiveFighters
    {
        get
        {
            var count = 0;
            for (var i = _fighters.Count - 1; i >= 0; i--)
            {
                var fighter = _fighters[i];
                if (AgentSlotIdentity.IsCurrentOccupant(fighter) && fighter.IsActive())
                    count++;
                else
                    _fighters.RemoveAt(i);
            }

            return count;
        }
    }

    public bool AgentRoom => CrewAgentBudget.HasRoom(_mission.AllAgents.Count, DefaultBattleMissionAgentSpawnLogic.MaxNumberOfAgentsForMission);

    public int VanillaSpawnedCount => ArenaPracticeControllerAccess.SpawnedCount(_controller);

    public IReadOnlyList<PoolTroop> TownTroops()
    {
        var settlement = Settlement.CurrentSettlement;
        if (settlement == null)
            return Array.Empty<PoolTroop>();

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var pool = new List<PoolTroop>();
        foreach (var troop in ArenaPracticeFightMissionController.GetParticipantCharacters(settlement))
        {
            if (troop != null && seen.Add(troop.StringId))
                pool.Add(new PoolTroop(troop.StringId, troop.Tier));
        }

        return pool;
    }

    public bool StandVanillaDown(out int faded)
    {
        faded = 0;
        var participants = ArenaPracticeControllerAccess.IsBound ? ArenaPracticeControllerAccess.ParticipantsCopy(_controller) : null;
        if (participants == null)
        {
            _logger.LogError($"{Tag} The practice controller's members did not bind: the wave cannot stand vanilla's practice down");
            return false;
        }

        var main = _mission.MainAgent;
        var toFade = participants.Where(a => a != null && a != main).ToList();
        SandBox.SandBoxHelpers.MissionHelper.FadeOutAgents(toFade, hideInstantly: true, hideMount: false);
        ArenaPracticeControllerAccess.SetSpawnedCount(_controller, VanillaSpawnCap);
        _mission.ClearCorpses(false);
        _mission.RemoveSpawnedItemsAndMissiles();
        faded = toFade.Count;
        return true;
    }

    public bool SpawnPlayer(int stage)
    {
        var player = CharacterObject.PlayerCharacter;
        var kit = player == null ? null : PracticeKit(player, stage);
        var frames = InitialFrames;
        if (player == null || kit == null || frames.Count == 0)
        {
            _logger.LogWarning($"{Tag} The player's practice kit for stage {stage} or a spawn frame is missing");
            return false;
        }

        if (!CrewAgentBudget.HasRoom(_mission.AllAgents.Count, DefaultBattleMissionAgentSpawnLogic.MaxNumberOfAgentsForMission))
        {
            _logger.LogWarning($"{Tag} No room is left for the player's body");
            return false;
        }

        _mission.MainAgent?.FadeOut(hideInstantly: true, hideMount: false);
        _mission.SetMissionMode(MissionMode.Battle, atStart: false);

        var frame = frames[0];
        var position = frame.origin;
        var direction = frame.rotation.f.AsVec2.Normalized();
        var body = _mission.SpawnAgent(new AgentBuildData(player)
            .Team(_mission.PlayerTeam)
            .InitialPosition(in position)
            .InitialDirection(in direction)
            .NoHorses(true)
            .Equipment(kit)
            .TroopOrigin(new SimpleAgentOrigin(player))
            .Controller(AgentControllerType.Player));
        body.WieldInitialWeapons();
        return true;
    }

    public bool SpawnFighter(string troopId, int stage)
    {
        var troop = Game.Current?.ObjectManager?.GetObject<CharacterObject>(troopId);
        var kit = troop == null ? null : PracticeKit(troop, stage);
        var main = _mission.MainAgent;
        if (troop == null || kit == null || main == null)
            return false;
        if (!CrewAgentBudget.HasRoom(_mission.AllAgents.Count, DefaultBattleMissionAgentSpawnLogic.MaxNumberOfAgentsForMission))
            return false;

        var frame = PickFrame(main.Position);
        if (frame == null)
            return false;

        var position = frame.Value.origin;
        var direction = frame.Value.rotation.f.AsVec2.Normalized();
        var fighter = _mission.SpawnAgent(new AgentBuildData(troop)
            .Team(WaveTeam())
            .InitialPosition(in position)
            .InitialDirection(in direction)
            .NoHorses(true)
            .Equipment(kit)
            .TroopOrigin(new SimpleAgentOrigin(troop))
            .Controller(AgentControllerType.AI));
        fighter.FadeIn();
        if (fighter.IsAIControlled)
            fighter.SetWatchState(Agent.WatchState.Alarmed);
        _fighters.Add(fighter);
        return true;
    }

    public bool ReturnToMaster()
    {
        foreach (var fighter in _fighters)
        {
            if (AgentSlotIdentity.IsCurrentOccupant(fighter) && fighter.IsActive())
                fighter.FadeOut(hideInstantly: true, hideMount: false);
        }

        _fighters.Clear();
        if (!ArenaPracticeControllerAccess.StartPractice(_controller) || !ArenaPracticeControllerAccess.SpawnPlayerNearMaster(_controller))
        {
            _logger.LogError($"{Tag} The practice controller's reset did not bind: the player cannot be put back by the master");
            return false;
        }

        var master = _mission.Agents.FirstOrDefault(a => a.Character is CharacterObject c && c.Occupation == Occupation.ArenaMaster);
        var conversation = _mission.GetMissionBehavior<MissionConversationLogic>();
        if (master == null || conversation == null || _mission.MainAgent == null)
            return false;

        conversation.StartConversation(master, true);
        return true;
    }

    public void Announce(WaveAnnouncement kind, int wave, int amount) =>
        MBInformationManager.AddQuickInformation(TrainingWavesAnnouncementTexts.For(kind, wave, amount), extraTimeInMs: 2000);

    private List<MatrixFrame> InitialFrames => _initialFrames ??= ReadFrames(InitialFrameTag);

    private List<MatrixFrame> RespawnFrames => _respawnFrames ??= ReadFrames(RespawnFrameTag);

    private List<MatrixFrame> ReadFrames(string tag)
    {
        var frames = _mission.Scene.FindEntitiesWithTag(tag).Select(e => e.GetGlobalFrame()).ToList();
        for (var i = 0; i < frames.Count; i++)
        {
            var frame = frames[i];
            frame.rotation.OrthonormalizeAccordingToForwardAndKeepUpAsZAxis();
            frames[i] = frame;
        }

        return frames;
    }

    /// <summary>A frame at least 12 m from the player, cycling by turn: the respawn frames when the scene has any, as vanilla does, else the initial ones.</summary>
    private MatrixFrame? PickFrame(Vec3 player)
    {
        var frames = RespawnFrames.Count > 0 ? RespawnFrames : InitialFrames;
        var spots = frames.Select(f => (f.origin.x, f.origin.y)).ToList();
        var index = SpawnPicker.Pick(spots, player.x, player.y, _turn++);
        return index < 0 ? null : frames[index];
    }

    /// <summary>The fighters' team: a new Attacker team on first use, hostile to the player's team and friendly to every neutral team of the yard.</summary>
    private Team WaveTeam()
    {
        if (_waveTeam != null)
            return _waveTeam;

        var team = _mission.Teams.Add(BattleSideEnum.Attacker);
        team.SetIsEnemyOf(_mission.PlayerTeam, true);
        foreach (var other in _mission.Teams)
        {
            if (other.Side == BattleSideEnum.None)
                team.SetIsEnemyOf(other, false);
        }

        _waveTeam = team;
        return team;
    }

    /// <summary>
    /// The kit vanilla gives a practice fighter (ArenaPracticeFightMissionController.cs :325-354): the four weapons of one of the stage's
    /// weapon sets of the town's culture (else the troop's), the armour of the tournament model for slots 5 to 9. Null when no weapon set exists.
    /// </summary>
    private static Equipment? PracticeKit(CharacterObject troop, int stage)
    {
        var sets = WeaponSets(troop, stage);
        if (sets.Count == 0)
            return null;

        var source = sets[MBRandom.RandomInt(sets.Count)];
        var kit = new Equipment();
        for (var slot = 0; slot <= LastWeaponSlot; slot++)
            Copy(source, kit, (EquipmentIndex)slot);

        var armour = Campaign.Current?.Models?.TournamentModel?.GetParticipantArmor(troop);
        if (armour != null)
        {
            for (var slot = FirstArmourSlot; slot <= LastArmourSlot; slot++)
                Copy(armour, kit, (EquipmentIndex)slot);
        }

        return kit;
    }

    private static void Copy(Equipment from, Equipment to, EquipmentIndex slot)
    {
        var element = from.GetEquipmentFromSlot(slot);
        if (element.Item != null)
            to.AddEquipmentToSlotWithoutAgent(slot, element);
    }

    /// <summary>
    /// The town's culture set, then the troop's culture set, then the Empire set: vanilla's own last fallback
    /// (ArenaPracticeFightMissionController.cs :328). Empty only when none of the three exists.
    /// </summary>
    private static List<Equipment> WeaponSets(CharacterObject troop, int stage)
    {
        var town = Settlement.CurrentSettlement?.MapFaction?.Culture?.StringId;
        var set = WeaponSetOf(stage, town) ?? WeaponSetOf(stage, troop.Culture?.StringId) ?? WeaponSetOf(stage, "empire");
        return set?.BattleEquipments?.ToList() ?? new List<Equipment>();
    }

    private static CharacterObject? WeaponSetOf(int stage, string? cultureId) =>
        cultureId == null ? null : Game.Current?.ObjectManager?.GetObject<CharacterObject>("weapon_practice_stage_" + stage + "_" + cultureId);
}
