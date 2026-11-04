namespace TAOM.Features.SeatOfSeeing.Domain;

/// <summary>What the view keeps to (build-spec section 7).</summary>
internal enum SeatKeepKind
{
    None,
    Soldier,
    Company,
}

/// <summary>The kept target's position as the adapter reads it; <c>Gone</c> means the soldier is not there or the company has no units.</summary>
internal readonly record struct SeatTargetRead(bool Gone, float X, float Y, float Z);

/// <summary>The rules of picking a target and of letting go of it (build-spec section 7).</summary>
internal static class SeatKeepRule
{
    /// <summary>The movement keys, rise and sink let go; the mouse, the wheel and the fast key do not.</summary>
    internal static bool LetsGo(SeatInput input) =>
        input.Right != 0f || input.Forward != 0f || input.Lift != 0f;

    /// <summary>The key keeps to the selected company, not to the soldier under the pointer: the panel is open and exactly one company with units is selected.</summary>
    internal static bool KeepsCompany(bool panelOpen, int selectedCount, int firstUnits) =>
        panelOpen && selectedCount == 1 && firstUnits > 0;

    /// <summary>
    /// A soldier counts when the terrain ray hits nothing or the agent is not farther than what it hits. The terrain distance starts as
    /// NaN, so the test is written to hold for it: a plain greater-than against NaN is false.
    /// </summary>
    internal static bool AgentCounts(bool terrainHit, float terrainDistance, float agentDistance) =>
        !terrainHit || !(agentDistance > terrainDistance);
}
