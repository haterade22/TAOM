namespace TAOM.Features.SettlementGuards;

/// <summary>
/// Decision for the hideout guard-dialog suppression (regression of #258). A hideout has no guards, but vanilla's
/// <c>GuardsCampaignBehavior</c> start lines (<c>guard_start</c>, <c>disguise_start_conversation</c>,
/// <c>disguise_start_conversation_alt</c>) answer any Occupation.Soldier NPC while the player is inside a settlement,
/// and they sit ahead of the hideout boss line (same priority 100, Guards registers after Hideout in
/// <c>SandBoxSubModule</c>). When <c>HideoutMissionController.SelectBossAgent</c> falls back to the highest-level
/// agent (dedicated boss troop missing or wounded), that agent is a Soldier in 8 of 9 hideout cultures, the guard
/// line closes the window and the boss fight never starts. Pure so it tests without the engine; the patch only
/// reads the two mission behaviours.
/// </summary>
public static class HideoutGuardDialogRule
{
    public static bool ShouldSuppress(bool hasHideoutController, bool hasHideoutAmbushController)
        => hasHideoutController || hasHideoutAmbushController;

    /// <summary>The value the condition should return: false in a hideout mission, vanilla's answer elsewhere.</summary>
    public static bool ApplyToResult(bool vanillaResult, bool hasHideoutController, bool hasHideoutAmbushController)
        => vanillaResult && !ShouldSuppress(hasHideoutController, hasHideoutAmbushController);
}
