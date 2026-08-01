using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Domain;
using TAOM.Features.DevConsole.Domain;

namespace TAOM.Features.DevConsole;

/// <summary>
/// Boundary conversion for <c>taom.print_agent_info</c>: reads a live <see cref="Agent"/> and hands
/// back primitives, so nothing engine-shaped reaches the formatter (ADR-007). Split out of the cheat
/// class to keep that entry point thin (ADR-002).
///
/// Reads are individually guarded because several bottom out in native calls —
/// <c>MBActionSet.GetSkeletonName()</c> notably has no validity check before its native call, unlike
/// its sibling <c>GetName()</c>.
/// </summary>
internal static class AgentSnapshotBuilder
{
    internal static AgentSnapshot Build(Agent agent)
    {
        if (agent == null) return null;

        var races = ResolveRaceManager();
        var raceId = Safe(() => agent.Character?.Race ?? -1, -1);

        return new AgentSnapshot
        {
            Index = Safe(() => agent.Index, -1),
            Name = Safe(() => agent.Name),
            CharacterId = Safe(() => agent.Character?.StringId),

            // Validate BEFORE the lookup: GetRaceNameFromId coerces an unknown id to "human" with
            // only a warning, so calling it blind would make this diagnostic confidently report the
            // wrong race. Null means "not in the registry" and the formatter says so explicitly.
            RaceName = races != null && Safe(() => races.IsValidRaceId(raceId))
                ? Safe(() => races.GetRaceNameFromId(raceId))
                : null,
            RaceId = raceId,

            MonsterId = Safe(() => agent.Monster?.StringId),
            ActionSetName = Safe(() => agent.ActionSet.GetName()),
            SkeletonName = Safe(() => agent.ActionSet.GetSkeletonName()),

            // Nullable, not 0-on-failure: zero health is a meaningful value, so a defaulted read
            // would report a healthy agent as dead.
            Health = SafeNullable(() => agent.Health),
            MaxHealth = SafeNullable(() => agent.HealthLimit),

            TeamLabel = Safe(() => agent.Team?.Side.ToString()),
            FormationLabel = Safe(() => agent.Formation?.FormationIndex.ToString()),
            IsHuman = Safe(() => agent.IsHuman),
            IsMount = Safe(() => agent.IsMount),
            MountMonsterId = Safe(() => agent.MountAgent?.Monster?.StringId),
            RiderName = Safe(() => agent.RiderAgent?.Name),
            EquipmentSlots = ReadSpawnEquipment(agent),
        };
    }

    /// <summary>
    /// Reads <see cref="Agent.SpawnEquipment"/> — what the agent was BUILT with, not what it is
    /// currently wielding. That is the right source for the use case this command exists for
    /// (verifying a troop's equipment template rendered), but the formatter labels it "at spawn" so
    /// nobody reads it as the live loadout after a weapon switch or a dropped shield.
    ///
    /// The range covers all 12 slots (weapons 0-4, Head/Body/Leg/Gloves/Cape, Horse, HorseHarness) —
    /// `WeaponItemBeginSlot` is 0, the start of the whole flat enum, despite the name.
    /// </summary>
    private static IReadOnlyList<string> ReadSpawnEquipment(Agent agent)
    {
        var slots = new List<string>();
        try
        {
            var equipment = agent.SpawnEquipment;
            for (var index = EquipmentIndex.WeaponItemBeginSlot; index < EquipmentIndex.NumEquipmentSetSlots; index++)
            {
                var item = equipment[index].Item;
                if (item != null) slots.Add($"{index}: {item.StringId}");
            }
        }
        catch { /* a partially-built agent has no readable equipment; report whatever was gathered */ }
        return slots;
    }

    private static IRaceManager ResolveRaceManager()
    {
        try { return IoC.Resolve<IRaceManager>(); } catch { return null; }
    }

    private static T Safe<T>(Func<T> read, T fallback = default)
    {
        try { return read(); } catch { return fallback; }
    }

    // Distinguishes "the read threw" (null) from a legitimate value, for fields where the natural
    // default would itself be a plausible reading.
    private static float? SafeNullable(Func<float> read)
    {
        try { return read(); } catch { return null; }
    }
}
