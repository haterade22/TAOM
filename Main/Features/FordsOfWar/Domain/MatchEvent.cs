namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>
/// Something worth a line in the event feed. Side is the side the subject belongs to: the owner of a fallen tower or keep,
/// the hero's side, the buyer's side, the new owner of a point, the clearing side of a camp. Subject is the tower, hero,
/// offer, point or camp id. Amount is the gold involved (tower bounty per slot, pack price, hero price) and 0 otherwise.
/// </summary>
internal sealed record MatchEvent(MatchEventKind Kind, MatchSide Side, string Subject, int Amount);
