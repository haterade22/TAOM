using System.Collections.Generic;
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

    // A callToWarRefusals row in diplomacy.json: the called kingdom refuses any call to war against the target.
    bool RefusesCallToWar(string calledKingdomId, string targetKingdomId);

    /// <summary>
    /// Ends the permanent alliance of a pair (#1105 slice 5): records it as broken, so the pair reads Neutral and never goes to war with
    /// each other, then ends the engine alliance. <see cref="PermanentAllianceEnd.NotPermanent"/> for a pair with no Permanent row.
    /// </summary>
    PermanentAllianceEnd EndPermanentAlliance(string kingdomAId, string kingdomBId, string why);

    bool IsBrokenAlliance(string kingdomAId, string kingdomBId);

    /// <summary>The broken pairs as sorted <c>"a|b"</c> strings, for the save.</summary>
    IReadOnlyList<string> BrokenAlliancesForSave();

    /// <summary>Replaces the broken pairs from a save; keeps only pairs that still have a Permanent row.</summary>
    void RestoreBrokenAlliances(IEnumerable<string>? saved);

    void ResetBrokenAlliances();

    void EstablishInitialAlliances();
    void EnforcePermanentAlliances();
}
