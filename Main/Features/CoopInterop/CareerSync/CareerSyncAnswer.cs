namespace TAOM.Features.CoopInterop.CareerSync;

/// <summary>
/// A career the co-op server sends to a client. <see cref="Sequence"/> is the sequence of the last report the server
/// handled for that hero, so the hero's own client can drop an answer to an older report. <see cref="Known"/> is false
/// when the server holds no entry for the hero: the client then keeps its own copy.
/// </summary>
public sealed record CareerSyncAnswer(CareerSyncSnapshot Career, long Sequence, bool Refused, bool Known);

/// <summary>What the server does with one report: an answer to the sender, one to every other player, or neither.</summary>
public sealed record CareerSyncOutcome(CareerSyncRefusal Refusal, CareerSyncAnswer? ToSender, CareerSyncAnswer? ToOthers);
