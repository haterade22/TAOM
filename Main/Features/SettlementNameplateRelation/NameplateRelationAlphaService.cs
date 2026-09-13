namespace TAOM.Features.SettlementNameplateRelation;

public class NameplateRelationAlphaService : INameplateRelationAlphaService
{
    /// <summary>Vanilla's own-faction in-window target
    /// (<c>SettlementNameplateWidget._normalAllyAlphaTarget</c>, v1.4.8 dump line 69).</summary>
    public const float RaisedTargetAlpha = 0.5f;

    public float Adjust(float vanillaTarget, int relationType, bool isTracked)
    {
        if (isTracked)
            return vanillaTarget;

        if (relationType != NameplateRelationPalette.Enemy && relationType != NameplateRelationPalette.Ally)
            return vanillaTarget;

        // Positive requirement, not "<= 0 return": NaN fails it and falls through untouched.
        if (!(vanillaTarget > 0f))
            return vanillaTarget;

        return vanillaTarget < RaisedTargetAlpha ? RaisedTargetAlpha : vanillaTarget;
    }
}
