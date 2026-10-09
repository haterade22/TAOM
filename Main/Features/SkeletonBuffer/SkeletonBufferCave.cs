// Adapted from yotthani's VanillaTuning (MIT, (c) 2026 yotthani), frame-buffer-guard and frame-buffer-watch.
using System;

namespace TAOM.Features.SkeletonBuffer;

/// <summary>
/// The machine code of the guard, built for given addresses (pure, no memory access). The 13 bytes of the engine's
/// reservation are replaced by <c>jmp cave</c> plus NOPs; the cave runs the same reservation and, if it would pass the
/// buffer's 65,536 entries, takes it back, counts the overflow and gives the skeleton the buffer's first slots.
/// TAOM's change from upstream: the counter lives on a SECOND page (<see cref="PageSize"/> after the cave), so the page
/// with the code can be made read-execute and never writable again. The second pool's block (<see cref="BuildPool2Cave"/>)
/// is TAOM's own and shares the two pages: code at +0x40 of the code page, counter at +0x40 of the data page.
///
/// <para>The code blocks have no unwind information. A stack walk sampled while a thread is inside their 43 bytes (pool 1)
/// or 42 bytes (pool 2) cannot unwind past them (docs/features/skeleton-buffer-guard.md).</para>
/// </summary>
internal static class SkeletonBufferCave
{
    internal const int PageSize = 0x1000;

    /// <summary>Two pages: code, then the counter.</summary>
    internal const int AllocationSize = 2 * PageSize;

    internal const int CaveLength = 0x2B;

    private const int CounterIncrementEnd = 0x24;   // the rip a 'lock inc [rip+disp]' at 1Dh measures from
    private const int JumpEnd = CaveLength;         // the rip the final 'jmp resume' measures from

    /// <summary>Where the overflow counter sits for a cave at <paramref name="cave"/>.</summary>
    internal static long CounterAddress(long cave) => cave + PageSize;

    /// <summary>
    /// The cave's bytes, or null when the counter or the resume point is more than a signed 32-bit displacement away.
    /// <paramref name="resume"/> is the first instruction after the replaced 13 bytes.
    /// </summary>
    internal static byte[]? BuildCave(long cave, long counter, long resume)
    {
        var counterDisp = counter - (cave + CounterIncrementEnd);
        var resumeDisp = resume - (cave + JumpEnd);
        if (!FitsInt32(counterDisp) || !FitsInt32(resumeDisp)) return null;

        var code = new byte[CaveLength]
        {
            0x41, 0x8B, 0xF4,                               // 00 mov  esi, r12d
            0x4C, 0x89, 0x7C, 0x24, 0x20,                   // 03 mov  [rsp+20h], r15
            0xF0, 0x0F, 0xC1, 0x75, 0x00,                   // 08 lock xadd [rbp], esi        ; esi = fill before
            0x42, 0x8D, 0x04, 0x26,                         // 0D lea  eax, [rsi+r12]         ; fill after
            0x3D, 0x00, 0x00, 0x01, 0x00,                   // 11 cmp  eax, 10000h
            0x76, 0x0E,                                     // 16 jbe  26h
            0xF0, 0x44, 0x29, 0x65, 0x00,                   // 18 lock sub [rbp], r12d        ; take the reservation back
            0xF0, 0xFF, 0x05, 0, 0, 0, 0,                   // 1D lock inc dword [rip+disp32] ; overflow counter
            0x33, 0xF6,                                     // 24 xor  esi, esi               ; the buffer's first slots
            0xE9, 0, 0, 0, 0,                               // 26 jmp  resume
        };
        Array.Copy(BitConverter.GetBytes((int)counterDisp), 0, code, 0x20, 4);
        Array.Copy(BitConverter.GetBytes((int)resumeDisp), 0, code, 0x27, 4);
        return code;
    }

    /// <summary>The 13 bytes that replace the site: <c>E9 rel32</c> to the cave and eight NOPs. Null when the cave is out of reach.</summary>
    internal static byte[]? BuildSiteJump(long site, long cave)
    {
        var disp = cave - (site + 5);
        if (!FitsInt32(disp)) return null;

        var bytes = new byte[SkeletonBufferSignature.GuardSiteLength];
        bytes[0] = 0xE9;
        Array.Copy(BitConverter.GetBytes((int)disp), 0, bytes, 1, 4);
        for (var i = 5; i < bytes.Length; i++) bytes[i] = 0x90;
        return bytes;
    }

    /// <summary>Where the second pool's code block sits on the code page, and its counter on the data page (both 0x40 in).</summary>
    internal const int Pool2CaveOffset = 0x40;

    internal const int Pool2CaveLength = 0x2A;

    private const int Pool2CounterIncrementEnd = 0x22;   // the rip a 'lock inc [rip+disp]' at 1Bh measures from
    private const int Pool2JumpEnd = Pool2CaveLength;    // the rip the final 'jmp resume' measures from

    /// <summary>Where the second pool's code block sits for the allocation at <paramref name="pages"/>.</summary>
    internal static long Pool2CaveAddress(long pages) => pages + Pool2CaveOffset;

    /// <summary>Where the second pool's overflow counter sits for the allocation at <paramref name="pages"/>.</summary>
    internal static long Pool2CounterAddress(long pages) => CounterAddress(pages) + Pool2CaveOffset;

    /// <summary>
    /// The second pool's code block, or null when the counter or the resume point is out of a 32-bit displacement. TAOM's own
    /// design, mirroring pool 1's block (yotthani's VanillaTuning guards pool 1 only). The function takes the pool buffer in
    /// rcx and the entry count in edx and returns the start index, so the block reserves with <c>lock xadd [rcx], r15d</c>;
    /// past 262,144 entries (32 blocks of 8,192, a bound the engine never compares) it takes the reservation back, counts the
    /// overflow and returns start index 0. Same trade as pool 1: wrong bones on the skeleton that owned those slots, only
    /// while the pool would otherwise have frozen the battle. <paramref name="resume"/> is the first instruction after the
    /// replaced 13 bytes (<c>lea edi, [rdx-1]</c>), which writes neither eax nor the flags; the next two do, before any
    /// branch reads them (<c>mov eax, r15d</c> at RVA 0x6AF60 writes eax, <c>shr eax, 0Dh</c> at 0x6AF63 the flags), so
    /// both are dead at resume.
    /// </summary>
    internal static byte[]? BuildPool2Cave(long cave, long counter, long resume)
    {
        var counterDisp = counter - (cave + Pool2CounterIncrementEnd);
        var resumeDisp = resume - (cave + Pool2JumpEnd);
        if (!FitsInt32(counterDisp) || !FitsInt32(resumeDisp)) return null;

        var code = new byte[Pool2CaveLength]
        {
            0x4C, 0x89, 0x7C, 0x24, 0x20,                   // 00 mov  [rsp+20h], r15
            0x44, 0x8B, 0xFA,                               // 05 mov  r15d, edx
            0xF0, 0x44, 0x0F, 0xC1, 0x39,                   // 08 lock xadd [rcx], r15d       ; r15d = fill before
            0x42, 0x8D, 0x04, 0x3A,                         // 0D lea  eax, [rdx+r15]         ; fill after
            0x3D, 0x00, 0x00, 0x04, 0x00,                   // 11 cmp  eax, 40000h
            0x76, 0x0D,                                     // 16 jbe  25h
            0xF0, 0x29, 0x11,                               // 18 lock sub [rcx], edx         ; take the reservation back
            0xF0, 0xFF, 0x05, 0, 0, 0, 0,                   // 1B lock inc dword [rip+disp32] ; overflow counter
            0x45, 0x33, 0xFF,                               // 22 xor  r15d, r15d             ; the pool's first slots
            0xE9, 0, 0, 0, 0,                               // 25 jmp  resume
        };
        Array.Copy(BitConverter.GetBytes((int)counterDisp), 0, code, 0x1E, 4);
        Array.Copy(BitConverter.GetBytes((int)resumeDisp), 0, code, 0x26, 4);
        return code;
    }

    private static bool FitsInt32(long value) => value >= int.MinValue && value <= int.MaxValue;
}
