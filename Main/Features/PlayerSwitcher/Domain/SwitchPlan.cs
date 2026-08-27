namespace TAOM.Features.PlayerSwitcher.Domain;

/// <summary>
/// What the handover will do, decided before anything mutates. Expressing the decision as data
/// is what lets the ordered call sequence be asserted in a unit test.
/// </summary>
public readonly struct SwitchPlan
{
    public SwitchPlan(string heroId, SwitchPath path, bool transferGold, string careerId)
    {
        HeroId = heroId;
        Path = path;
        TransferGold = transferGold;
        CareerId = careerId;
    }

    public string HeroId { get; }
    public SwitchPath Path { get; }
    public bool TransferGold { get; }

    /// <summary>
    /// The career the player chose in TAOM's own career menu. Carried across explicitly, because
    /// it is the one character-creation choice that survives the handover.
    /// </summary>
    public string CareerId { get; }

    public bool IsValid => !string.IsNullOrEmpty(HeroId);

    public static SwitchPlan None => new SwitchPlan(string.Empty, SwitchPath.AssumeIdentity, false, string.Empty);
}
