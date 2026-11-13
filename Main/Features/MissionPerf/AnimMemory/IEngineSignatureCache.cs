namespace TAOM.Features.MissionPerf.AnimMemory;

/// <summary>
/// Remembers, between launches, where in <c>TaleWorlds.Native.dll</c> each byte signature was found, so the full scan of
/// the code section can be skipped when the DLL has not changed. <see cref="Begin"/> never throws and never trusts the
/// file: every remembered address is checked against the bytes in memory before it is used.
/// </summary>
public interface IEngineSignatureCache
{
    /// <summary>
    /// Opens one scan: <paramref name="headers"/> is the module's first page as read from memory, <paramref name="textRva"/>
    /// and <paramref name="textSize"/> are its code section. A missing, unreadable or foreign cache gives a session that
    /// scans in full.
    /// </summary>
    SignatureScanSession Begin(string moduleFileName, byte[] headers, int textRva, int textSize);
}
