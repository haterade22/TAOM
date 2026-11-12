using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.CustomBattles.Muster;

/// <summary>One horn blower's state across frames (held by <see cref="CustomBattleHornCallBehavior"/>).</summary>
internal sealed class HornBlower
{
    public HornBlower(Agent agent) => Agent = agent;

    public Agent Agent { get; }
    public readonly Dictionary<string, int> Prefabs = new Dictionary<string, int>();
    public float Until = -1f;
    public Vec2 LastTarget;
    public EquipmentIndex Main = EquipmentIndex.None;
    public EquipmentIndex Off = EquipmentIndex.None;
    public MovementOrder.MovementOrderEnum LastOrder;
    public bool Seen;
    public bool Equipped;
    public bool HornSet;
    public AgentFlag Flags;
}

/// <summary>
/// What a horn blower does: take his horn set, play the call (horn on the head bone, cornu clip on channel 1, the
/// culture's horn sound, a morale rally) and stop again. The caller has checked the agent with AgentSlotIdentity.
/// Boundary code (raw Agent and engine calls); game-tested per ADR-008.
/// </summary>
internal static class HornBlowerActions
{
    private const string Prefab = "tcb_horn_big";
    private const string Bone = "head";
    private const string Action = "act_play_cornu_stand";
    private const int Channel = 1;
    private const float Duration = 3.2f;
    private const float MoraleBoost = 12f;
    private const float EnemyMoraleShock = 4f;
    private const float ShockRadius = 30f;
    private const string FallbackSound = "event:/alerts/horns/attack";

    /// <summary>His horn set for the whole battle, given once while he stands: switching sets on a walking agent
    /// crashed the engine. It is his own warrior set plus the cornu clip (as_tcb_horn_&lt;own&gt;).</summary>
    internal static void GiveHornSet(HornBlower b)
    {
        var a = b.Agent;
        var own = a.ActionSet.GetName() ?? "";
        // another mod may give everyone as_kys_mk_<own set> at spawn: the horn set is keyed on the race's own
        if (own.StartsWith("as_kys_mk_")) own = "as_" + own.Substring("as_kys_mk_".Length);
        var want = own.StartsWith("as_") ? "as_tcb_horn_" + own.Substring(3) : null;
        if (want == null || a.HasMount || !a.IsHuman) { CustomBattleMuster.Log($"horn {a.Name}: keeps {own}, sounds only"); return; }
        var set = MBActionSet.GetActionSet(want);
        if (!set.IsValid || set.GetName() != want) { CustomBattleMuster.Log($"horn {a.Name}: no horn set for {own}, sounds only"); return; }
        var data = a.Monster.FillAnimationSystemData(set, a.Character.GetStepSize(), false);
        a.SetActionSet(ref data);
        b.HornSet = true;
    }

    internal static void Blow(Mission mission, HornBlower b, string why)
    {
        var a = b.Agent;
        try
        {
            if (a.IsHuman && !a.HasMount && b.HornSet)
            {
                b.Main = a.GetPrimaryWieldedItemIndex();
                b.Off = a.GetOffhandWieldedItemIndex();
                if (b.Main != EquipmentIndex.None) a.TryToSheathWeaponInHand(Agent.HandIndex.MainHand, Agent.WeaponWieldActionType.Instant);
                if (b.Off != EquipmentIndex.None) a.TryToSheathWeaponInHand(Agent.HandIndex.OffHand, Agent.WeaponWieldActionType.Instant);
                // the AI would draw his weapons again at once: not while he plays
                b.Flags = a.GetAgentFlags();
                a.SetAgentFlags(b.Flags & ~AgentFlag.CanWieldWeapon);
                ShowPrefab(b);
                var act = ActionIndexCache.Create(Action);
                if (act.Index >= 0) a.SetActionChannel(Channel, in act, true, 0UL, 0f, 1f, 0.2f, 0.4f, 0f, false, 0.2f, 0, true);
                else CustomBattleMuster.Log($"horn: no action {Action} in {a.ActionSet.GetName()}");
            }
            Sound(mission, a);
            Rally(mission, a);
            b.Until = mission.CurrentTime + Duration;
            CustomBattleMuster.Log($"horn {a.Name} ({a.Team?.Side}) {why}");
        }
        catch (Exception e)
        {
            CustomBattleMuster.Log("horn failed: " + e.Message);
            Stop(b);
        }
    }

    internal static void Stop(HornBlower b)
    {
        var a = b.Agent;
        b.Until = -1f;
        try
        {
            // a recycled slot answers IsActive for its new tenant: restore nothing onto him
            if (!TAOM.Features.AdvancedCombat.AgentSlotIdentity.IsCurrentOccupant(a) || !a.IsActive()) return;
            foreach (var id in b.Prefabs.Values) if (id >= 0) a.SetSynchedPrefabComponentVisibility(id, false);
            var none = ActionIndexCache.Create("act_none");
            a.SetActionChannel(Channel, in none, true, 0UL, 0f, 1f, 0.2f, 0.4f, 0f, false, 0.2f, 0, true);
            if (b.Flags != AgentFlag.None) { a.SetAgentFlags(b.Flags); b.Flags = AgentFlag.None; }
            if (b.Main != EquipmentIndex.None) a.TryToWieldWeaponInSlot(b.Main, Agent.WeaponWieldActionType.Instant, false);
            if (b.Off != EquipmentIndex.None) a.TryToWieldWeaponInSlot(b.Off, Agent.WeaponWieldActionType.Instant, false);
            b.Main = b.Off = EquipmentIndex.None;
        }
        catch (Exception e) { CustomBattleMuster.Log("horn stop failed: " + e.Message); }
    }

    /// <summary>The horn on the head bone: added once (synched), shown while he plays.</summary>
    private static void ShowPrefab(HornBlower b)
    {
        var a = b.Agent;
        if (!b.Prefabs.TryGetValue(Prefab, out var id))
        {
            sbyte index = Skeleton.GetBoneIndexFromName(a.ActionSet.GetSkeletonName(), Bone);
            if (index < 0) CustomBattleMuster.Log($"horn: no bone {Bone} on {a.ActionSet.GetSkeletonName()}");
            id = index < 0 ? -1 : a.AddSynchedPrefabComponentToBone(Prefab, index);
            b.Prefabs[Prefab] = id;
        }
        if (id >= 0) a.SetSynchedPrefabComponentVisibility(id, true);
    }

    /// <summary>TAOM's horn of the blower's people at his position; the game's attack horn when there is none.</summary>
    private static void Sound(Mission mission, Agent a)
    {
        var culture = a.Character?.Culture?.Name?.ToString() ?? "";
        if (culture.Length == 0) culture = a.Character?.Culture?.StringId ?? "";
        string name =
            Has(culture, "rohan", "rohirrim") ? "LOTR/Rohan/Alert/horn_1.wav" :
            Has(culture, "gondor", "dol amroth", "arnor", "arthedain", "dale", "barding", "rhovanion", "woodmen", "beorning") ? "LOTR/Rohan/Alert/french_horn_1.wav" :
            Has(culture, "isengard", "uruk", "dunland", "dunlending") ? "LOTR/Isengard/Alert/horn_trombone_1.wav" :
            Has(culture, "mordor", "orc", "goblin", "gundabad", "angmar", "dol guldur", "harad", "easterling", "khand", "umbar") ? "LOTR/Isengard/Alert/horn_isengard_helmsdeep.wav" :
            FallbackSound;
        int id = SoundEvent.GetEventIdFromString(name);
        if (id < 0) id = SoundEvent.GetEventIdFromString(FallbackSound);
        if (id >= 0) mission.MakeSound(id, a.GetEyeGlobalPosition(), false, true, -1, -1);
    }

    private static bool Has(string s, params string[] words) => words.Any(w => s.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0);

    /// <summary>His formation takes heart; enemies close by waver a little.</summary>
    private static void Rally(Mission mission, Agent a)
    {
        foreach (var other in mission.Agents)
        {
            if (!other.IsActive() || !other.IsHuman || other == a) continue;
            if (other.Formation != null && other.Formation == a.Formation) other.ChangeMorale(MoraleBoost);
            else if (other.Team != null && a.Team != null && other.Team.IsEnemyOf(a.Team)
                     && other.Position.DistanceSquared(a.Position) < ShockRadius * ShockRadius)
                other.ChangeMorale(-EnemyMoraleShock);
        }
    }
}
