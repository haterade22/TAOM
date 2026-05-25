namespace TAOM.Features.CrashReport.Domain;

// One frame of the throwing stack. File + line populated when PDBs are present
// (TAOM ships its own .pdb; third-party mods often don't).
public sealed record StackFrameSnapshot(
    int Index,
    string? MethodFullName,
    string? DeclaringTypeFullName,
    string? DeclaringAssemblyName,
    string? FileName,
    int FileLine,
    int IlOffset);
