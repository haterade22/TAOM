namespace TAOM.Features.CrashReport.Domain;

public sealed record IdentitySnapshot(
    string BannerlordVersion,
    string BannerlordExeFileVersion,
    string TaomVersion,
    string TaomDllSha1,
    string OriginatingPatchTarget,
    string LanguageCode);
