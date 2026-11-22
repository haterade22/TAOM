using System;
using System.Linq;
using TAOM.Features.LongDefeat.Domain;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Adapters;

/// <summary>
/// The lord's gear swap and the bot's errand (phase4-plan.md Slice 5, spike S11), main thread only, called from the director's tick and never from a removal or hit callback. Every agent is tested with
/// <c>Live</c> before each use and nothing is keyed on <c>Agent.Index</c>. The swap is variant A. The engine members, read in the installed v1.5.7 source: <c>Agent.UpdateSpawnEquipmentAndRefreshVisuals</c>
/// (Agent.cs:3719-3740) sets the spawn equipment, refills the mission equipment from it, re-equips the weapon slots, updates the properties twice and wields the initial weapons, and writes no health and never touches the mount;
/// the Custom Battle model's <c>InitializeAgentStats</c> writes the armour sums and the armour's weight, which its update never writes (CustomBattleAgentStatCalculateModel.cs:22-28; the Sandbox model writes them on every update), and it does not read the build data;
/// <c>Agent.UpdateAgentProperties</c> (:3692-3699) then runs the model's update, which reads that weight. <c>Agent.SetScriptedPosition</c> (:2460-2467) is native and teleports only when
/// <c>Mission.IsTeleportingAgents</c>; <c>DisableScriptedMovement</c> (:4318-4321) releases it. Whether a formation order overrides the scripted position is native and not read (spike A34).
/// </summary>
internal sealed partial class LongDefeatBattlefieldAdapter
{
    private const string VariantA = "A";

    private Agent? _errandBody;

    /// <summary>
    /// Puts <paramref name="loadout"/> on the lord's own live body (variant A): the kit the spawn builds, the engine's own equipment swap, the mission's stat model's init over the new spawn equipment, then an update, which
    /// must come after the init because the update reads the armour weight the init writes. The driven armour, health, mount and wielded item are read before and after. Refuses, writing nothing, when the main agent is not
    /// the lord's own live body, so the set stays owned and reaches the next spawn.
    /// </summary>
    public LdSwap SwapGear(LdLoadout loadout)
    {
        var lord = _lordWatch.Lord as Agent;
        var player = Game.Current?.PlayerTroop;
        var model = MissionGameModels.Current?.AgentStatCalculateModel;
        if (lord == null || !Live(lord) || !ReferenceEquals(lord, _mission.MainAgent))
            return LdSwap.Refused("not-own-body");
        if (player == null || model == null || lord.AgentDrivenProperties == null)
            return LdSwap.Refused(player == null ? "no-player-troop" : model == null ? "no-stat-model" : "no-driven-properties");

        var before = ArmourOf(lord);
        var health = lord.Health;
        var healthLimit = lord.HealthLimit;
        var mounted = lord.HasMount;

        var kit = BuildKit(player, loadout);
        lord.UpdateSpawnEquipmentAndRefreshVisuals(kit);
        model.InitializeAgentStats(lord, lord.SpawnEquipment, lord.AgentDrivenProperties, null!);
        lord.UpdateAgentProperties();

        _kitFacts = ReadBack(lord, loadout);
        var wrong = loadout.Slots.Count(s => _kitFacts.Found.FirstOrDefault(f => f.Slot == s.Slot)?.ItemId != s.ItemId);
        var sums = new LdArmour(kit.GetHeadArmorSum(), kit.GetHumanBodyArmorSum(), kit.GetLegArmorSum(), kit.GetArmArmorSum(), kit.GetTotalWeightOfArmor(true));
        return new LdSwap(true, "", VariantA, before, ArmourOf(lord), sums, health, healthLimit, lord.Health, lord.HealthLimit, mounted, lord.HasMount, WieldedOf(lord), wrong);
    }

    /// <summary>The driven armour of the live main agent, or <see cref="LdArmour.Unread"/> with none: the read at the rise, which depends on none of the swap's own writes.</summary>
    public LdArmour ReadLordArmour()
    {
        var main = _mission.MainAgent;
        return main == null || !Live(main) ? LdArmour.Unread : ArmourOf(main);
    }

    private static LdArmour ArmourOf(Agent agent)
    {
        var p = agent.AgentDrivenProperties;
        return p == null ? LdArmour.Unread : new LdArmour(p.ArmorHead, p.ArmorTorso, p.ArmorLegs, p.ArmorArms, p.ArmorEncumbrance);
    }

    private static string WieldedOf(Agent agent)
    {
        var index = agent.GetPrimaryWieldedItemIndex();
        return index == EquipmentIndex.None ? "none" : agent.Equipment[index].Item?.StringId ?? "none";
    }

    /// <summary>
    /// Sets the scripted walk of the live, AI-driven main agent to <paramref name="place"/> and holds that body for <see cref="EndErrand"/>. A person walks himself, so a main agent a person controls is refused. The
    /// engine's own answer to <c>CanBeAssignedForScriptedMovement</c> and whether he uses a game object are read before the walk is set.
    /// </summary>
    public LdErrandMove StartErrand(LdPlace place)
    {
        var main = _mission.MainAgent;
        if (main == null || !Live(main))
            return LdErrandMove.Refused("no-live-main");
        if (!main.IsAIControlled)
            return LdErrandMove.Refused("person-controlled");

        var assignable = main.CanBeAssignedForScriptedMovement();
        var usingObject = main.IsUsingGameObject;
        ScriptPosition(main, place);
        _errandBody = main;
        return new LdErrandMove(true, "", main.HasMount, assignable, usingObject, IsScripted(main), DistanceTo(main, place));
    }

    /// <summary>
    /// The errand's teleport: reads the held body's distance and scripted flag first (the A34 evidence), puts him at the place with the lord's own teleport, and sets the scripted position again so the AI does not walk him
    /// back before the bot buys. Refuses unless the held body is live and still the main agent.
    /// </summary>
    public LdErrandMove TeleportErrand(LdPlace place)
    {
        var body = _errandBody;
        if (body == null)
            return LdErrandMove.Refused("none-held");
        if (!Live(body) || !ReferenceEquals(body, _mission.MainAgent))
            return LdErrandMove.Refused("body-gone");

        var scripted = IsScripted(body);
        var distance = DistanceTo(body, place);
        var usingObject = body.IsUsingGameObject;
        var mounted = body.HasMount;
        if (!TeleportPlayer(place))
            return new LdErrandMove(false, "teleport-refused", mounted, false, usingObject, scripted, distance);

        ScriptPosition(body, place);
        return new LdErrandMove(true, "", mounted, false, usingObject, scripted, distance);
    }

    /// <summary>Releases the scripted walk of the body the errand walked, if it still stands, and drops it. Acts on the held body only, never on whoever is the main agent now; safe with none held.</summary>
    public LdErrandMove EndErrand()
    {
        var body = _errandBody;
        _errandBody = null;
        if (body == null)
            return LdErrandMove.Refused("none-held");
        if (!Live(body))
            return LdErrandMove.Refused("body-gone");

        body.DisableScriptedMovement();
        return new LdErrandMove(true, "", body.HasMount, false, body.IsUsingGameObject, IsScripted(body), float.NaN);
    }

    private void ScriptPosition(Agent agent, LdPlace place)
    {
        var target = new WorldPosition(_mission.Scene, UIntPtr.Zero, new Vec3(place.X, place.Y, place.Z), false);
        agent.SetScriptedPosition(ref target, false, Agent.AIScriptedFrameFlags.None);
    }

    private static bool IsScripted(Agent agent) => (agent.GetScriptedFlags() & Agent.AIScriptedFrameFlags.GoToPosition) != 0;

    private static float DistanceTo(Agent agent, LdPlace place) => new Vec2(place.X, place.Y).Distance(agent.Position.AsVec2);
}
