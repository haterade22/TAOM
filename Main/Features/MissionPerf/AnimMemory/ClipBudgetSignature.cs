using System;
using System.Collections.Generic;

namespace TAOM.Features.MissionPerf.AnimMemory;

/// <summary>
/// Locates the engine's on-demand animation clip counter and budget by signature, never by a fixed
/// offset. The clip eviction pass in <c>TaleWorlds.Native.dll</c> loads the loaded-bytes counter with
/// <c>mov eax, dword ptr [rip+disp32]</c> and, a few instructions later, subtracts the 12 MiB budget
/// float with <c>subss xmm0, dword ptr [rip+disp32]</c>; this pattern covers both instructions and the
/// code between them, with the four rip displacements wildcarded.
/// </summary>
internal static class ClipBudgetSignature
{
    internal const string ModuleName = "TaleWorlds.Native.dll";
    internal const string Pattern = "8B 05 ? ? ? ? 41 8B EC 48 8B 3D ? ? ? ? 48 2B 3D ? ? ? ? 48 C1 FF 04 83 EF 01 66 0F 6E C0 0F 5B C0 F3 0F 5C 05 ? ? ? ? F3 44 0F 2C F8";
    internal const int LoadOffset = 0, LoadLength = 6, LoadDispOffset = 2;        // mov eax, dword ptr [rip+disp32]
    internal const int BudgetOffset = 37, BudgetLength = 8, BudgetDispOffset = 4; // subss xmm0, dword ptr [rip+disp32]
    internal const float ExpectedBudgetBytes = 12582912f;

    /// <summary>Hex bytes separated by spaces; <c>?</c> or <c>??</c> is a wildcard, returned as -1.</summary>
    internal static int[] Parse(string pattern)
    {
        var tokens = pattern.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var result = new int[tokens.Length];
        for (var i = 0; i < tokens.Length; i++)
            result[i] = tokens[i] == "?" || tokens[i] == "??" ? -1 : Convert.ToInt32(tokens[i], 16);
        return result;
    }

    /// <summary>Start offsets of <paramref name="pattern"/> in <paramref name="haystack"/>, at most <paramref name="maxHits"/>.</summary>
    internal static List<int> Find(byte[] haystack, int[] pattern, int maxHits)
    {
        var hits = new List<int>();
        for (var i = 0; i <= haystack.Length - pattern.Length; i++)
        {
            var j = 0;
            while (j < pattern.Length && (pattern[j] == -1 || haystack[i + j] == pattern[j]))
                j++;
            if (j < pattern.Length)
                continue;
            hits.Add(i);
            if (hits.Count >= maxHits)
                break;
        }
        return hits;
    }

    internal static int RipTarget(int instructionRva, int instructionLength, int disp32) =>
        instructionRva + instructionLength + disp32;

    /// <summary>
    /// Scans <paramref name="text"/> (the module's code, mapped at <paramref name="textRva"/>) for the
    /// eviction-pass site. Only a single match carries RVAs; zero or two leave them 0.
    /// </summary>
    internal static SignatureMatch Resolve(byte[] text, int textRva)
    {
        var hits = Find(text, Parse(Pattern), 2);
        if (hits.Count != 1)
            return new SignatureMatch(hits.Count, 0, 0, 0, 0);

        var at = hits[0];
        var loadSite = textRva + at + LoadOffset;
        var budgetSite = textRva + at + BudgetOffset;
        var counter = RipTarget(loadSite, LoadLength, BitConverter.ToInt32(text, at + LoadOffset + LoadDispOffset));
        var budget = RipTarget(budgetSite, BudgetLength, BitConverter.ToInt32(text, at + BudgetOffset + BudgetDispOffset));
        return new SignatureMatch(1, loadSite, budgetSite, counter, budget);
    }
}

/// <summary>What <see cref="ClipBudgetSignature.Resolve"/> found: the match count and, for one match, the RVAs.</summary>
internal sealed class SignatureMatch
{
    internal SignatureMatch(int matchCount, int loadSiteRva, int budgetSiteRva, int counterRva, int budgetRva)
    {
        MatchCount = matchCount;
        LoadSiteRva = loadSiteRva;
        BudgetSiteRva = budgetSiteRva;
        CounterRva = counterRva;
        BudgetRva = budgetRva;
    }

    internal int MatchCount { get; }
    internal int LoadSiteRva { get; }
    internal int BudgetSiteRva { get; }
    internal int CounterRva { get; }
    internal int BudgetRva { get; }
}
