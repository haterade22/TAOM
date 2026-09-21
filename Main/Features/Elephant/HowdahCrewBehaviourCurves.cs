namespace TAOM.Features.Elephant;

/// <summary>
/// The AI behaviour curves a seated howdah archer runs on (#627, 2026-09-19). Pure: numbers and the engine's own
/// interpolation, no engine types, so the choice is testable.
///
/// The seat does NOT detach its archer: it keeps the formation, because a null one pins
/// <c>Agent.MissileRangeAdjusted</c> at 0, and holds the archer in place with a scripted position. Detaching was
/// tried and dropped, and why the curves below still matter starts there. <c>Formation.DetachUnit</c> ends with
/// <c>SetBehaviorValueSet(BehaviorValueSet.DefaultDetached)</c>. That set keeps Melee at (8, 7, 4, 20, 1) and crushes
/// Ranged to (0.02, 7, 0.04, 20, 0.03), about a hundredth of Melee at every distance (HumanAIComponent.cs:770-777).
/// It is tuned for loose skirmishers who still carry a sword; a crew archer carries a bow and quivers only, so it
/// chased a melee stance it could never reach. The second crew test logged <c>act_unequip_bow_back</c> cycling and no
/// arrows at all. A formation carrying a Move order applies <c>DefaultMove</c>, whose Ranged row is the same
/// (0.02, 7, 0.04, 20, 0.03), so an ATTACHED archer needs this override just as much. The seat overrides three of the
/// seven curves:
///
/// <list type="bullet">
/// <item>Ranged goes back to vanilla's Default row, which is what an ordinary archer uses.</item>
/// <item>Melee goes flat zero: there is no melee weapon on the roster and nothing within reach of one.</item>
/// <item>GoToPos goes flat zero: the seat owns where the archer stands, and a walk it can never take is wasted.</item>
/// </list>
///
/// The other four (ChargeHorseback, RangedHorseback, AttackEntityMelee, AttackEntityRanged) are left as the engine
/// set them: the crew never mount, and the entity-attack pair is the siege-engine path.
/// </summary>
internal static class HowdahCrewBehaviourCurves
{
    // BehaviorValueSet.Default's Ranged row, verbatim.
    public const float RangedY1 = 2f;
    public const float RangedX2 = 7f;
    public const float RangedY2 = 4f;
    public const float RangedX3 = 20f;
    public const float RangedY3 = 5f;

    // Flat zero, with the engine's own knees so the curve shape stays legal.
    public const float MeleeY1 = 0f;
    public const float MeleeX2 = 7f;
    public const float MeleeY2 = 0f;
    public const float MeleeX3 = 20f;
    public const float MeleeY3 = 0f;

    public const float GoToPosY1 = 0f;
    public const float GoToPosX2 = 7f;
    public const float GoToPosY2 = 0f;
    public const float GoToPosX3 = 20f;
    public const float GoToPosY3 = 0f;

    /// <summary>
    /// How often the seat reasserts these, in seconds. <c>RefreshBehaviorValues</c> re-stamps a value set on the
    /// agent every time its formation re-applies a movement order (HumanAIComponent.cs:783-789,
    /// MovementOrder.OnApply), so setting them once is not enough. The cadence is not about interop:
    /// <c>OverrideBehaviorParams</c> makes no native call at all, it writes five floats into a managed array and
    /// marks it dirty, and the engine pushes it natively once per parallel tick from
    /// <c>HumanAIComponent.OnTickParallel</c>. Twice a second is about not re-dirtying that array 250 times a second.
    /// </summary>
    public const float ReassertSeconds = 0.5f;
}
