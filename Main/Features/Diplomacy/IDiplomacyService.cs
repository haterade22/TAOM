using TAOM.Features.Diplomacy.Models;

namespace TAOM.Features.Diplomacy;

public interface IDiplomacyService
{
    AllianceTier GetRelationshipTier(string kingdomAId, string kingdomBId);
    float GetAllianceScoreModifier(string kingdomAId, string kingdomBId);

    /// <summary>
    /// Alliance score modifier with player-freedom awareness. When
    /// <paramref name="involvesPlayer"/> is true (the player's kingdom is one of the
    /// pair), returns a large positive bonus that clears the vanilla acceptance
    /// threshold and never applies the lore Hostile penalty — the player may ally
    /// any kingdom. Otherwise identical to the 2-arg overload.
    /// </summary>
    float GetAllianceScoreModifier(string kingdomAId, string kingdomBId, bool involvesPlayer);

    bool IsAllianceAllowed(string kingdomAId, string kingdomBId);

    /// <summary>
    /// Whether a start-alliance decision is permitted. When
    /// <paramref name="involvesPlayer"/> is true, always allowed (full freedom);
    /// otherwise blocks only lore Hostile pairs (same as <see cref="IsAllianceAllowed"/>).
    /// </summary>
    bool IsAllianceDecisionAllowed(string kingdomAId, string kingdomBId, bool involvesPlayer);

    /// <summary>
    /// Structural check (no lore gate) for the player initiating an alliance: both ids
    /// present + distinct, the kingdoms are at peace, and they are not already allied.
    /// </summary>
    bool CanPlayerProposeAlliance(string playerKingdomId, string targetKingdomId);

    /// <summary>
    /// Form an alliance the player initiated. Returns true only if the alliance actually formed
    /// (false when <see cref="CanPlayerProposeAlliance"/> rejects the pair or the engine call
    /// had no effect — e.g. a missing/eliminated kingdom).
    /// </summary>
    bool FormPlayerAlliance(string playerKingdomId, string targetKingdomId);

    bool IsWarAllowed(string kingdomAId, string kingdomBId);
    void EstablishInitialAlliances();
    void EnforcePermanentAlliances();
}
