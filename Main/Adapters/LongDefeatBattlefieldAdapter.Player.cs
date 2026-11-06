using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.LongDefeat.Domain;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace TAOM.Adapters;

/// <summary>
/// The player's lord: his spawn at the start and at a respawn, the class kit he carries into it, the read-back of what the engine made, and for a mounted class (spike S9) the sally: the points
/// he is put at, the teleport that carries him and his horse, the one-step remount of a lord on foot, and the reads the sally lines and checks are written from.
/// </summary>
internal sealed partial class LongDefeatBattlefieldAdapter
{
    private static readonly LdKitFacts NoLord = new(false, Array.Empty<LdKitSlot>(), Array.Empty<string>(), false);

    // The facts are read once, in the frame of the spawn, and kept in place of the Agent: an Agent slot recycles, and a held one would answer IsActive for whoever fills it next.
    private LdKitFacts _kitFacts = NoLord;
    private List<string> _kitSkipped = new();

    public bool SpawnPlayer(bool respawn, LdLoadout? loadout, LdPlace? place = null)
    {
        var player = Game.Current?.PlayerTroop;
        var team = _mission.DefenderTeam;
        if (player == null || team == null)
            return false;

        var kit = loadout == null ? null : BuildKit(player, loadout);
        if (loadout?.Mounted == true)
        {
            var rider = SpawnMounted(player, team, kit, place);
            _lordWatch.SetLord(rider);
            _kitFacts = ReadBack(rider, loadout);
            return true;
        }

        if (respawn)
        {
            var formation = team.GetFormation(FormationClass.Infantry);
            formation.BeginSpawn(1, false);
            var respawned = Spawn(player, team, true, FormationClass.Infantry, true, 1, 0, true, kit: kit);
            formation.EndSpawn();
            _lordWatch.SetLord(respawned);
            _kitFacts = ReadBack(respawned, loadout);
            return true;
        }

        var count = _open.TryGetValue((false, FormationSlot.Infantry), out var started) ? started : 1;
        var lord = Spawn(player, team, true, FormationClass.Infantry, false, count, count - 1, false, kit: kit);
        _lordWatch.SetLord(lord);
        _kitFacts = ReadBack(lord, loadout);
        return true;
    }

    // A mounted lord belongs to the defenders' cavalry formation, not the foot batch: one rider in a formation switches every soldier in it to mounted spacing (Formation.cs:412-414, :587). The first spawn
    // positions the formation at the place, so the spawn finds it positioned and never asks the deployment plan for a cavalry plan nobody added troops to (Mission.cs:4214-4226, SetPositioning sets
    // HasBeenPositioned, Formation.cs:864-867). A remount finds it positioned and leaves its order point where it was: moving it would show the code's own write as the formation's pull on the out line, and
    // the engine itself positions only a formation that is not (Mission.cs:4214). The director gives a mounted lord a place at every spawn; with none the engine's own layout frame would apply, which this class has never been tried on.
    private Agent SpawnMounted(BasicCharacterObject player, Team team, Equipment? kit, LdPlace? place)
    {
        var formation = team.GetFormation(FormationClass.Cavalry);
        if (place != null && !formation.HasBeenPositioned)
            formation.SetPositioning(new WorldPosition(_mission.Scene, new Vec3(place.X, place.Y, place.Z)), FacingOf(place));

        // The real mounted flag, as vanilla opens a formation's spawn (MissionBattleSideSpawnContext.cs:360, :371): false would make HasAnyMountedUnit read foot during the spawn (Formation.cs:404-411).
        formation.BeginSpawn(1, true);
        var rider = Spawn(player, team, true, FormationClass.Cavalry, true, 1, 0, true, kit: kit, place: place, mounted: true);
        formation.EndSpawn();
        return rider;
    }

    // A place with no facing (the keep point) takes the direction a spawn needs from the front the scene's own frames use; the lord keeps his own on a teleport.
    private static Vec2 FacingOf(LdPlace place) => place.HasFacing ? new Vec2(place.Fx, place.Fy) : new Vec2(0f, 1f);

    // The kit is a copy of the lord's first battle set with the loadout's slots overwritten; with no override the engine picks his set itself (Mission.cs:4108 takes this one first).
    // The XML slot names go through the engine's own mapping, because Item0 is not a member of EquipmentIndex. A slot the engine cannot take (an unknown item, a misfit, a horse that
    // cannot be ridden) is left as the lord had it and named, so the class check fails instead of the run quietly playing another kit. A troop with no battle set starts from an empty one:
    // spawned without the override, a hero would throw on FirstBattleEquipment.Clone() (Mission.cs:4284-4287, :4108) and any other troop would get an empty set (Equipment.cs:562).
    private Equipment BuildKit(BasicCharacterObject troop, LdLoadout loadout)
    {
        _kitSkipped = new List<string>();
        var kit = troop.FirstBattleEquipment?.Clone() ?? new Equipment(Equipment.EquipmentType.Battle);
        foreach (var slot in loadout.Slots)
        {
            if (!TryIndex(slot.Slot, out var index))
            {
                _kitSkipped.Add(slot.Slot);
                continue;
            }

            if (slot.ItemId.Length == 0)
            {
                kit[index] = default;
                continue;
            }

            var item = MBObjectManager.Instance.GetObject<ItemObject>(slot.ItemId);
            var rideable = index != EquipmentIndex.Horse || item?.HorseComponent?.IsRideable == true;
            if (item == null || !rideable || !Equipment.IsItemFitsToSlot(index, item))
            {
                _kitSkipped.Add(slot.Slot);
                continue;
            }

            kit[index] = new EquipmentElement(item);
        }

        if (_kitSkipped.Count > 0)
            _logger.LogWarning($"[LongDefeat][Class] the {loadout.ClassId} kit could not fill {string.Join(", ", _kitSkipped)} (an unknown item, or one that does not fit the slot); the lord keeps his own there");
        return kit;
    }

    private static bool TryIndex(string slot, out EquipmentIndex index)
    {
        try
        {
            index = Equipment.GetEquipmentIndexFromOldEquipmentIndexName(slot);
            return true;
        }
        catch (ArgumentException)
        {
            index = EquipmentIndex.None;
            return false;
        }
    }

    // ---- the sally (spike S9) ----

    // The agent is the current tenant of its slot and active: the lord is held as the body the field spawned, and a held body is gated before it touches native state (csharp-architecture.md).
    private static bool Live(Agent agent) => agent.IsActive() && AgentSlotIdentity.IsCurrentOccupant(agent);

    public LdPointFacts SallyPointUsable(LdPlace place)
    {
        var at = new Vec3(place.X, place.Y, place.Z);
        var navmesh = _mission.Scene.GetNavigationMeshForPosition(in at) != UIntPtr.Zero;
        return new LdPointFacts(navmesh, _mission.IsPositionInsideBoundaries(at.AsVec2));
    }

    // MissionBoundaryCrossingHandler.TickForMainAgent tests !Mission.IsPositionInsideBoundaries(Agent.Main.Position.AsVec2) (MissionBoundaryCrossingHandler.cs:210), so the main agent's position is read
    // whatever body it is. The detailed look walks the attackers once, so it is asked for only when a line is written.
    public LdLordFacts ReadLord(bool detail)
    {
        var main = _mission.MainAgent;
        if (main == null || !Live(main))
            return LdLordFacts.None;

        var at = main.Position;
        var inside = _mission.IsPositionInsideBoundaries(at.AsVec2);
        var own = ReferenceEquals(main, _lordWatch.Lord);
        var id = main.Character?.StringId ?? "";
        if (!detail)
            return new LdLordFacts(true, own, id, main.HasMount, Point(at), inside, "", null, -1f, -1f, "");

        var formation = main.Formation;
        LdPoint? orderAt = formation != null && formation.OrderPositionIsValid ? Point(formation.OrderGroundPosition) : null;
        var toFormation = orderAt is { } order ? at.AsVec2.Distance(new Vec2(order.X, order.Y)) : -1f;
        return new LdLordFacts(true, own, id, main.HasMount, Point(at), inside, formation == null ? "none" : formation.FormationIndex.ToString(), orderAt, toFormation, NearestAttackerTo(at), GateState());
    }

    private float NearestAttackerTo(Vec3 at)
    {
        var nearest = float.MaxValue;
        foreach (var attacker in _mission.AttackerTeam.ActiveAgents)
        {
            if (!attacker.IsHuman || !attacker.IsActive())
                continue;
            nearest = Math.Min(nearest, at.AsVec2.Distance(attacker.Position.AsVec2));
        }

        return nearest == float.MaxValue ? -1f : nearest;
    }

    private string GateState()
    {
        var gate = _gates.FirstOrDefault();
        return gate == null ? "none" : gate.IsDestroyed ? "broken" : gate.IsGateOpen ? "open" : "closed";
    }

    // TeleportToPosition moves the mount, then the rider (Agent.cs:4287-4299), and LookDirection has a public setter (Agent.cs:1493-1495) that only changes the agent it is called on. Vanilla turns a
    // mounted main agent's horse, then the rider, then teleports (SandBox MissionConversationLogic.cs:176-182): the camera rewrites a human rider's look on every tick, so only the horse's heading lasts.
    // The director only asks for the lord's own body.
    public bool TeleportPlayer(LdPlace place)
    {
        var main = _mission.MainAgent;
        if (main == null || !Live(main))
            return false;

        if (place.HasFacing)
        {
            var facing = new Vec3(place.Fx, place.Fy, 0f);
            if (main.MountAgent != null)
                main.MountAgent.LookDirection = facing;
            main.LookDirection = facing;
        }

        main.TeleportToPosition(new Vec3(place.X, place.Y, place.Z));
        return true;
    }

    // One main-thread step (phase2-plan.md "The sally"): read his health share, mark the old body as a planned removal by identity, fade it without its horse (Agent.cs:4304-4310 hands removal to native, and a
    // horse is never faded: orientation trap "Riderless creatures"), spawn the new body mounted at the place with the Player controller (it becomes the main agent as it builds, Agent.cs:5206 and :1255), then
    // write the share onto it (decision D3). Native reports the faded body Routed inside FadeOut, before the new body spawns (the native fade_out runs FUN_1805f02b0(agent, 2) and then calls the callback slot at +0x248 with the state read from +0x560, tools/native_decompile.py --engine-method IMBAgent.FadeOut); only its
    // deletion comes later. The old body's state is read right after the fade, and every callback that reaches it is kept by NoteBodyEvent. The new body's kit is not read back: nothing reads it (SpawnLord reads
    // only its own spawn's), and the class check is written at the spawn.
    public LdRemount RemountPlayer(LdPlace place, LdLoadout loadout)
    {
        var old = _mission.MainAgent;
        var player = Game.Current?.PlayerTroop;
        var team = _mission.DefenderTeam;
        if (old == null || !Live(old) || !ReferenceEquals(old, _lordWatch.Lord) || player == null || team == null)
            return new LdRemount(false, "", false, 0f, "the main agent is not the lord's own live body");

        var share = LdRemount.ShareOf(old.Health, old.HealthLimit);
        var kit = BuildKit(player, loadout);
        _lordWatch.Plan(old);
        old.FadeOut(true, false);
        var state = old.State;
        var fresh = SpawnMounted(player, team, kit, place);
        _lordWatch.SetLord(fresh);
        if (fresh.HealthLimit > 0f)
            fresh.Health = Math.Max(1f, fresh.HealthLimit * share);
        return new LdRemount(true, state.ToString(), state == AgentState.Killed || state == AgentState.Unconscious, share, "");
    }

    // A hit is read in the engine's callback, on a thread native picks. The other side must be an attacker; an attacker's horse counts as its rider.
    public bool TryContact(object? affected, object? affector, float victimHealth, out LdContact contact) =>
        _lordWatch.TryContact(affected, affector, (_lordWatch.Lord as Agent)?.MountAgent, victimHealth, IsAttackerBody, out contact);

    private bool IsAttackerBody(object? agent) =>
        agent is Agent body && (body.IsMount ? body.RiderAgent : body) is { } rider && ReferenceEquals(rider.Mission, _mission) && rider.Team?.Side == BattleSideEnum.Attacker;

    public void NoteBodyEvent(object agent, string callback, string state) =>
        _lordWatch.Note(agent, callback, state, state == nameof(AgentState.Killed) || state == nameof(AgentState.Unconscious),
            ReferenceEquals(agent, _mission.MainAgent) || ReferenceEquals(agent, _knownMain));

    public IReadOnlyList<LdBodyEvent> TakeBodyEvents() => _lordWatch.Take();

    public LdKitFacts ReadKit() => _kitFacts;

    // Read from the body the engine built, never from the kit above: the check must see what the spawn made. SpawnEquipment is set at the spawn (Mission.cs:4303) after every engine
    // edit of the override, so it also shows a slot the engine cleared itself.
    private LdKitFacts ReadBack(Agent? lord, LdLoadout? loadout)
    {
        if (lord == null || loadout == null || !lord.IsActive())
            return NoLord;

        var found = new List<LdKitSlot>();
        foreach (var slot in loadout.Slots)
            found.Add(new LdKitSlot(slot.Slot, TryIndex(slot.Slot, out var index) ? lord.SpawnEquipment[index].Item?.StringId ?? "" : ""));
        return new LdKitFacts(true, found, _kitSkipped.ToList(), lord.HasMount);
    }
}
