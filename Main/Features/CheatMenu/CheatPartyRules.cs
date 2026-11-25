namespace TAOM.Features.CheatMenu;

/// <summary>
/// The cheat menu's "Unlimited party" switch (docs/features/cheat-menu.md, "Unlimited party"). While on, the player's own party
/// has no real size limit (<see cref="SizeBonusFor"/>, read by TaomPartySizeModel) and keeps troops of enemy or
/// opposite-alignment cultures: AlignmentDesertionBehavior skips it, and DesertionRecruitGuard no longer blocks recruiting such
/// troops into it. Those two read it through IAlignmentDesertionService.KeepsMainPartyTroops, before they test for the main
/// party. SpecialResourceEarningService skips upkeep desertion while it is on. Only the main party: other parties
/// keep every rule. Saved with the campaign by <c>CheatMenuCampaignBehavior</c>; off in a new campaign.
/// </summary>
public interface ICheatPartyRules
{
    bool UnlimitedParty { get; set; }

    /// <summary>The flat bonus TaomPartySizeModel adds to a party's size limit: <see cref="CheatPartyRules.UnlimitedSizeBonus"/> for the main party while on, else 0.</summary>
    float SizeBonusFor(bool isMainParty);
}

public sealed class CheatPartyRules : ICheatPartyRules
{
    /// <summary>
    /// "No limit" as a number: far above any party a campaign builds, and inside
    /// AiPartySizeService.AddResultFrameBonus's 100,000 ceiling.
    /// </summary>
    public const float UnlimitedSizeBonus = 10_000f;

    public bool UnlimitedParty { get; set; }

    public float SizeBonusFor(bool isMainParty) => UnlimitedParty && isMainParty ? UnlimitedSizeBonus : 0f;
}
