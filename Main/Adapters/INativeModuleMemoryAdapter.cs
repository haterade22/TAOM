namespace TAOM.Adapters;

/// <summary>
/// Read-only access to a native module mapped in this process (ADR-007). Callers pass only addresses
/// inside the module's mapped image; this adapter checks nothing, and an access violation is a
/// corrupted-state exception on .NET Framework, which no plain catch stops.
/// </summary>
public interface INativeModuleMemoryAdapter
{
    /// <summary>Base address of a module already loaded in this process, or 0 when it is not.</summary>
    long GetModuleBase(string moduleFileName);

    /// <summary>A copy of <paramref name="count"/> bytes starting at <paramref name="address"/>.</summary>
    byte[] Copy(long address, int count);

    /// <summary>One aligned 32-bit read; atomic on x64.</summary>
    int ReadInt32(long address);
}
