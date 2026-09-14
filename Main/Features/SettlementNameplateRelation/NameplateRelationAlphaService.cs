namespace TAOM.Features.SettlementNameplateRelation;

public class NameplateRelationAlphaService : INameplateRelationAlphaService
{
    private readonly INameplateRelationSettingsProvider _settings;

    public NameplateRelationAlphaService(INameplateRelationSettingsProvider settings)
    {
        _settings = settings;
    }

    public float Adjust(float vanillaTarget, int relationType, bool isTracked)
    {
        if (isTracked)
            return vanillaTarget;

        // Positive requirement, not "<= 0 return": NaN fails it and falls through untouched, and
        // an off-window 0 stays hidden.
        if (!(vanillaTarget > 0f))
            return vanillaTarget;

        float configured;
        switch (relationType)
        {
            case NameplateRelationPalette.Neutral:
                configured = _settings.NeutralPlateAlpha;
                break;
            case NameplateRelationPalette.SameFaction:
            case NameplateRelationPalette.Enemy:
            case NameplateRelationPalette.Ally:
                configured = _settings.RelationPlateAlpha;
                break;
            default:
                return vanillaTarget;
        }

        // The provider validates, but a value about to reach the renderer is gated here too.
        return configured > 0f ? configured : vanillaTarget;
    }
}
