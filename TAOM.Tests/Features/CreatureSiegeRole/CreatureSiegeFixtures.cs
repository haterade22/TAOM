using System.Collections.Generic;
using TAOM.Features.CreatureSiegeRole.Domain;

namespace TAOM.Tests.Features.CreatureSiegeRole;

/// <summary>Builders shared by the creature siege role tests. Plain data, no engine types.</summary>
internal static class CreatureSiegeFixtures
{
    /// <summary>
    /// A castle gate at (100, 200) facing +Y (the attacker side), 10 m up, with a middle position 2 m inside it on the
    /// ground, and a destruction component of its own. Every argument can be overridden by name.
    /// </summary>
    public static SiegeGateReading Gate(
        string name = "gate",
        bool visible = true,
        bool disabled = false,
        float x = 100f,
        float y = 200f,
        float z = 10f,
        float forwardX = 0f,
        float forwardY = 1f,
        bool hasMiddle = true,
        float middleX = 100f,
        float middleY = 198f,
        float middleZ = 10f,
        object? destruction = null,
        object? handle = null) =>
        new(handle ?? new object(), name, visible, disabled, x, y, z, forwardX, forwardY, hasMiddle, middleX, middleY, middleZ,
            destruction ?? new object());

    /// <summary>Inputs for a plain attacker under AI command with both gates shut: the baseline every row edits.</summary>
    public static RoleInputs AttackerInputs(
        bool isAIControlled = true,
        bool isFleeing = false,
        SiegeSide side = SiegeSide.Attacker,
        SiegeFormationState? formation = null,
        bool outerPassable = false,
        bool innerPresent = true,
        bool innerPassable = false,
        bool pastOuterGate = false,
        bool ramWorking = false) =>
        new(isAIControlled, isFleeing, side, formation ?? SiegeFormationState.None, outerPassable, innerPresent, innerPassable,
            pastOuterGate, ramWorking);

    public static SiegeFormationState PlayerFormation(SiegeFormationOrder order) => new(IsPlayerCommanded: true, order);

    public static SiegeFormationState AIFormation(SiegeFormationOrder order = SiegeFormationOrder.Other) => new(IsPlayerCommanded: false, order);

    public static IReadOnlyList<T> List<T>(params T[] items) => items;
}
