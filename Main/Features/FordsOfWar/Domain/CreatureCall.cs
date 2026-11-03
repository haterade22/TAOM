using System.Collections.Generic;

namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>A faction that can call in creatures: the commander picks one and pays for it, then waits CooldownSeconds for the next call of any kind.</summary>
internal sealed record CreatureCall(float CooldownSeconds, IReadOnlyList<UnitOffer> Offers);
