using TAOM.Features.Execution;

namespace TAOM.Features.MarriageAlignment;

/// <summary>
/// Pure decision for whether two cultures may intermarry. Reuses the existing
/// <see cref="IAlignmentService"/> lookup (the same id→side table the Execution, Diplomacy and
/// AlignmentRecruitment features read), via <see cref="IAlignmentService.GetCultureSide"/>.
/// </summary>
/// <remarks>
/// Deliberately does NOT call <see cref="IAlignmentService.AreEnemyAlignments"/>, whose Neutral
/// semantics are inverted for this purpose: it returns true when either side is Neutral, so it
/// treats Neutral as an enemy of everyone and would bar every Umbar, Shaghana, Abanissa and Dunland
/// hero from marrying anybody. Here Neutral marries anyone, which matches how
/// <c>RecruitmentAlignmentService</c> and three other features read the same table.
/// <para>
/// <see cref="IsMarriageBlocked"/> is defined in terms of <see cref="AreCulturesCompatible"/> so the
/// rule the model enforces and the rule the AI draw is narrowed by cannot drift apart.
/// </para>
/// </remarks>
public class MarriageAlignmentService : IMarriageAlignmentService
{
    private readonly IAlignmentService _alignment;
    private readonly IMarriageAlignmentSettingsProvider _settings;

    public MarriageAlignmentService(IAlignmentService alignment, IMarriageAlignmentSettingsProvider settings)
    {
        _alignment = alignment;
        _settings = settings;
    }

    public bool IsMarriageBlocked(string? cultureIdA, string? cultureIdB, bool involvesPlayerClan)
    {
        if (!_settings.IsEnabled)
            return false;
        if (involvesPlayerClan && !_settings.ApplyToPlayer)
            return false;
        if (!involvesPlayerClan && !_settings.ApplyToAi)
            return false;

        return !AreCulturesCompatible(cultureIdA, cultureIdB);
    }

    public bool AreCulturesCompatible(string? cultureIdA, string? cultureIdB)
    {
        var sideA = _alignment.GetCultureSide(cultureIdA);
        var sideB = _alignment.GetCultureSide(cultureIdB);

        if (sideA == FactionSide.Neutral || sideB == FactionSide.Neutral)
            return true;

        // Both sides are non-Neutral here, so a difference is a Free↔Evil opposition.
        return sideA == sideB;
    }

    public bool ShouldSteerAiPartnerSearch =>
        _settings.IsEnabled && _settings.ApplyToAi && _settings.SteerAiPartnerSearch;
}
