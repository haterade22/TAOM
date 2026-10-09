// Adapted from yotthani's VanillaTuning (MIT, (c) 2026 yotthani), frame-buffer-guard and frame-buffer-watch.
namespace TAOM.Adapters;

/// <summary>What <see cref="ISkeletonBufferMemoryAdapter.PatchCode"/> left behind in the engine's code.</summary>
public enum EngineCodePatchResult
{
    /// <summary>The replacement bytes are in place and were read back.</summary>
    Written,

    /// <summary>The engine's own bytes are in place (the expected bytes were not there, or the write failed and was undone).</summary>
    NotWritten,

    /// <summary>The write failed and the original bytes could not be confirmed back: the site may jump anywhere.</summary>
    Unknown,
}

/// <summary>
/// The native memory of this process the skeleton buffer guard and watch work on (ADR-007): safe reads that fail instead
/// of crashing, pages allocated near a module, and the one write to engine code. TAOM's only adapter that writes native
/// memory, and the only file that may (a test pins it); the read-only <see cref="INativeModuleMemoryAdapter"/> stays
/// read-only. Holds no decisions: every address and every byte comes from the service.
/// </summary>
public interface ISkeletonBufferMemoryAdapter
{
    /// <summary>Base address of a module already loaded in this process, or 0 when it is not. Takes no reference.</summary>
    long GetModuleBase(string moduleFileName);

    /// <summary>A copy of <paramref name="count"/> bytes at <paramref name="address"/>, or null when any of them cannot be read.</summary>
    byte[]? Read(long address, int count);

    int? ReadInt32(long address);

    long? ReadInt64(long address);

    /// <summary>
    /// Commits <paramref name="bytes"/> of read-write memory at the highest free 64 KB-aligned address that is at most
    /// <paramref name="highest"/> and at least <paramref name="lowest"/>, trying every megabyte. 0 when none is free.
    /// </summary>
    long AllocateNear(long highest, long lowest, int bytes);

    /// <summary>Releases pages from <see cref="AllocateNear"/>. Never call it for pages engine code may jump into.</summary>
    bool Free(long address);

    /// <summary>Copies bytes into pages from <see cref="AllocateNear"/> that are still read-write. False when the copy fails.</summary>
    bool Write(long address, byte[] bytes);

    /// <summary>Makes the pages read-execute (no longer writable) and flushes the instruction cache.</summary>
    bool MakeExecutable(long address, int bytes);

    /// <summary>
    /// Replaces <paramref name="expected"/> with <paramref name="replacement"/> (same length) at an address in engine code:
    /// compares first, makes the pages writable, writes, flushes the instruction cache, reads back, restores the page
    /// protection, and puts <paramref name="expected"/> back if the read-back differs.
    /// </summary>
    EngineCodePatchResult PatchCode(long address, byte[] expected, byte[] replacement);
}
