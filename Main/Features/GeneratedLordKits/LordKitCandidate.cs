namespace TAOM.Features.GeneratedLordKits;

/// <summary>
/// One kit an XML lord is defined with, described without engine types. <see cref="Signature"/> is the item in
/// every slot, so lords defined with the same kit share it. <see cref="Token"/> is opaque here: the adapter
/// that built the candidate resolves it back to the engine set.
/// </summary>
public sealed record LordKitCandidate(string DonorId, string CultureId, int Race, bool IsFemale, bool IsCivilian,
    bool IsAlive, string Signature, object Token);
