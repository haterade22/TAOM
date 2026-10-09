// Adapted from yotthani's VanillaTuning (MIT, (c) 2026 yotthani), frame-buffer-guard and frame-buffer-watch.
using TAOM.Features.MissionPerf.AnimMemory;

namespace TAOM.Features.SkeletonBuffer;

/// <summary>
/// Locates, by byte pattern and never by a fixed offset, the places in <c>TaleWorlds.Native.dll</c> the skeleton buffer
/// feature needs (docs/reference/engine/mission-frame-threads-and-native-costs.md section 10): the load of the global
/// that holds the frame buffers, the 13 bytes where a skeleton reserves its entries, the second pool's load of the same
/// global, and the 13 bytes where that second pool reserves (with the check that they lie in the function the load
/// calls). Pure: it scans a byte array. Parsing and searching are <see cref="ClipBudgetSignature"/>'s.
/// </summary>
internal static class SkeletonBufferSignature
{
    internal const string ModuleName = "TaleWorlds.Native.dll";

    /// <summary>
    /// The bytes each guard replaces. Pool 1: <c>mov esi, r12d; mov [rsp+20h], r15; lock xadd [rbp], esi</c>. Pool 2:
    /// <c>mov [rsp+20h], r15; mov r15d, edx; lock xadd [rcx], r15d</c>.
    /// </summary>
    internal const int GuardSiteLength = 13;

    /// <summary><c>mov rcx, [rip+disp32]</c> (the global, wildcarded), then <c>add rcx, 9D0h; movsxd rax, [rcx+250h]; imul rbp, rax, 128h</c>.</summary>
    internal const string WatchPattern = "48 8B 0D ? ? ? ? 48 81 C1 D0 09 00 00 48 63 81 50 02 00 00 48 69 E8 28 01 00 00";

    internal const int WatchLoadLength = 7, WatchDispOffset = 3;

    /// <summary>
    /// The second per-frame pool's load (earlier in the same function, <c>FUN_180069aa0</c> in v1.5.4): the same global into rdx,
    /// then <c>add rdx, 0C28h; mov [rsp+50h], rbx; movsxd rax, [rdx+250h]; imul rbx, rax, 128h</c>. Its buffers are
    /// <see cref="SkeletonBufferWatchService.Pool2Offset"/> into the same structure; the watch reads their fill counters.
    /// </summary>
    internal const string Pool2Pattern = "48 8B 15 ? ? ? ? 48 81 C2 28 0C 00 00 48 89 5C 24 50 48 63 82 50 02 00 00 48 69 D8 28 01 00 00";

    internal const int Pool2LoadLength = 7, Pool2DispOffset = 3;

    private const int Pool2PatternLength = 33;

    /// <summary>The 13 site bytes and the instructions after them, which the resume jump re-enters.</summary>
    internal const string GuardPattern = "41 8B F4 4C 89 7C 24 20 F0 0F C1 75 00 45 8D 7C 24 FF 8B C6 C1 E8 0B";

    /// <summary>What the site looks like when another module already put a jump there: <c>E9 rel32</c> plus eight NOPs, then the original tail.</summary>
    internal const string ForeignGuardPattern = "E9 ? ? ? ? 90 90 90 90 90 90 90 90 45 8D 7C 24 FF 8B C6 C1 E8 0B";

    /// <summary>
    /// The second pool's reservation (TAOM's own site; yotthani's guard covers pool 1 only): the 13 site bytes
    /// <c>mov [rsp+20h], r15; mov r15d, edx; lock xadd [rcx], r15d</c>, then the instructions the cave jumps back to,
    /// <c>lea edi, [rdx-1]; mov eax, r15d; shr eax, 0Dh</c>.
    /// </summary>
    internal const string Pool2GuardPattern = "4C 89 7C 24 20 44 8B FA F0 44 0F C1 39 8D 7A FF 41 8B C7 C1 E8 0D";

    /// <summary>What pool 2's site looks like when another module put a jump there: <c>E9 rel32</c> plus eight NOPs, then the original tail.</summary>
    internal const string Pool2ForeignGuardPattern = "E9 ? ? ? ? 90 90 90 90 90 90 90 90 8D 7A FF 41 8B C7 C1 E8 0D";

    /// <summary>
    /// How far after the pool 2 load a <c>call rel32</c> to the function holding pool 2's site may sit (the call is 0x29 after
    /// the load in v1.5.4), and how far into that function the site may be (0x20 in v1.5.4). The link check: the load is
    /// followed by a call whose target is at most <see cref="Pool2CalleeSpan"/> bytes before the site.
    /// </summary>
    internal const int Pool2CallWindow = 0x80, Pool2CalleeSpan = 0x80;

    internal static int[] Parse(string pattern) => ClipBudgetSignature.Parse(pattern);

    /// <summary>
    /// Scans <paramref name="text"/> (the module's code, mapped at <paramref name="textRva"/>). A pattern's RVAs are set only
    /// for exactly one match; zero or two matches leave them 0 and the count says which.
    /// </summary>
    internal static SkeletonBufferSignatureResult Resolve(byte[] text, int textRva)
    {
        var watch = ClipBudgetSignature.Find(text, Parse(WatchPattern), 2);
        var guard = ClipBudgetSignature.Find(text, Parse(GuardPattern), 2);
        var foreign = ClipBudgetSignature.Find(text, Parse(ForeignGuardPattern), 2);
        var pool2 = ClipBudgetSignature.Find(text, Parse(Pool2Pattern), 2);

        var watchSite = 0;
        var global = 0;
        if (watch.Count == 1)
        {
            watchSite = textRva + watch[0];
            global = ClipBudgetSignature.RipTarget(watchSite, WatchLoadLength, System.BitConverter.ToInt32(text, watch[0] + WatchDispOffset));
        }

        var pool2Load = 0;
        var pool2Global = 0;
        if (pool2.Count == 1)
        {
            pool2Load = textRva + pool2[0];
            pool2Global = ClipBudgetSignature.RipTarget(pool2Load, Pool2LoadLength, System.BitConverter.ToInt32(text, pool2[0] + Pool2DispOffset));
        }

        var guardSite = guard.Count == 1 ? textRva + guard[0] : 0;

        var guard2 = ClipBudgetSignature.Find(text, Parse(Pool2GuardPattern), 2);
        var foreign2 = ClipBudgetSignature.Find(text, Parse(Pool2ForeignGuardPattern), 2);
        var guard2Site = guard2.Count == 1 ? textRva + guard2[0] : 0;
        var linked = guard2Site != 0 && pool2.Count == 1 && CalledFromLoad(text, textRva, pool2[0], guard2Site);

        return new SkeletonBufferSignatureResult(
            watch.Count, guard.Count, foreign.Count, pool2.Count,
            watchSite, global, guardSite, guardSite == 0 ? 0 : guardSite + GuardSiteLength, pool2Load, pool2Global,
            guard2.Count, foreign2.Count, guard2Site, guard2Site == 0 ? 0 : guard2Site + GuardSiteLength, linked);
    }

    /// <summary>
    /// True when a <c>call rel32</c> within <see cref="Pool2CallWindow"/> bytes after the pool 2 load (at text offset
    /// <paramref name="loadOffset"/>) targets a function entry at most <see cref="Pool2CalleeSpan"/> bytes before
    /// <paramref name="siteRva"/>. Any E8 byte in the window is tried and the target decides, so an E8 inside another
    /// instruction costs nothing unless its bytes also happen to point just before the site.
    /// </summary>
    private static bool CalledFromLoad(byte[] text, int textRva, int loadOffset, int siteRva)
    {
        var end = System.Math.Min(loadOffset + Pool2CallWindow, text.Length - 4);
        for (var i = loadOffset + Pool2PatternLength; i < end; i++)
        {
            if (text[i] != 0xE8) continue;
            var target = (long)textRva + i + 5 + System.BitConverter.ToInt32(text, i + 1);
            if (target <= siteRva && siteRva - target <= Pool2CalleeSpan) return true;
        }
        return false;
    }
}

/// <summary>What <see cref="SkeletonBufferSignature.Resolve"/> found: the match counts and, for single matches, the RVAs.</summary>
internal sealed class SkeletonBufferSignatureResult
{
    internal SkeletonBufferSignatureResult(int watchMatchCount, int guardMatchCount, int foreignGuardMatchCount,
        int pool2MatchCount, int watchSiteRva, int globalRva, int guardSiteRva, int resumeRva, int pool2LoadRva, int pool2GlobalRva,
        int pool2GuardMatchCount, int pool2ForeignGuardMatchCount, int pool2GuardSiteRva, int pool2ResumeRva, bool pool2GuardCalledFromLoad)
    {
        Pool2GuardMatchCount = pool2GuardMatchCount;
        Pool2ForeignGuardMatchCount = pool2ForeignGuardMatchCount;
        Pool2GuardSiteRva = pool2GuardSiteRva;
        Pool2ResumeRva = pool2ResumeRva;
        Pool2GuardCalledFromLoad = pool2GuardCalledFromLoad;
        WatchMatchCount = watchMatchCount;
        GuardMatchCount = guardMatchCount;
        ForeignGuardMatchCount = foreignGuardMatchCount;
        Pool2MatchCount = pool2MatchCount;
        WatchSiteRva = watchSiteRva;
        GlobalRva = globalRva;
        GuardSiteRva = guardSiteRva;
        ResumeRva = resumeRva;
        Pool2LoadRva = pool2LoadRva;
        Pool2GlobalRva = pool2GlobalRva;
    }

    internal int WatchMatchCount { get; }
    internal int GuardMatchCount { get; }
    internal int ForeignGuardMatchCount { get; }
    internal int Pool2MatchCount { get; }
    internal int WatchSiteRva { get; }
    internal int GlobalRva { get; }
    internal int GuardSiteRva { get; }
    internal int ResumeRva { get; }
    internal int Pool2LoadRva { get; }

    /// <summary>The global the second pool's load reads; 0 unless the pattern matched exactly once.</summary>
    internal int Pool2GlobalRva { get; }

    /// <summary>True when the site already holds another module's jump (VanillaTuning's shape).</summary>
    internal bool ForeignGuardPresent => ForeignGuardMatchCount > 0;

    internal int Pool2GuardMatchCount { get; }
    internal int Pool2ForeignGuardMatchCount { get; }
    internal int Pool2GuardSiteRva { get; }
    internal int Pool2ResumeRva { get; }

    /// <summary>True when pool 2's site matched once, the pool 2 load matched once, and a call after that load enters the function holding the site.</summary>
    internal bool Pool2GuardCalledFromLoad { get; }

    /// <summary>True when pool 2's site already holds another module's jump.</summary>
    internal bool Pool2ForeignGuardPresent => Pool2ForeignGuardMatchCount > 0;
}
