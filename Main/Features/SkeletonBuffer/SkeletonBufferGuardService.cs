// Adapted from yotthani's VanillaTuning (MIT, (c) 2026 yotthani), frame-buffer-guard and frame-buffer-watch.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.MissionPerf.AnimMemory;

namespace TAOM.Features.SkeletonBuffer;

/// <summary>
/// Installs the skeleton buffer guard (docs/features/skeleton-buffer-guard.md): the engine adds each skeleton's entries to
/// two per-frame pools (65,536 and 262,144 entries) with no bounds check, and one skeleton too many freezes the battle for
/// good. TAOM's first write to engine code, so every step is checked and the order is fixed: find the sites by byte pattern
/// in the IN-MEMORY code, allocate two pages near the module, write both code blocks and protect the code page, and only
/// then write the 13-byte jump at site 1 and at site 2. Each pool installs or reports its own reason: another module's jump
/// at one site does not stop TAOM guarding the other. A failure on the way frees the pages and leaves the engine's bytes
/// alone, except that a written site, or a site write whose result cannot be confirmed, keeps the pages, because that site
/// jumps (or may jump) into them.
///
/// <para>Main thread, once, at the first main menu: nothing is drawing skeletons then, so no thread can be inside the 13
/// bytes while they change. Everything is logged as <c>[SkeletonBuffer]</c> lines. Global steps end in one
/// <c>[SkeletonBuffer] guard OFF: &lt;reason&gt;</c> line; each pool then installs or reports on its own
/// (<c>guard ON pool N</c> or <c>guard OFF pool N</c>), plus one line when the second pool cannot be watched.
/// <see cref="Install"/> never throws.</para>
/// </summary>
public sealed class SkeletonBufferGuardService : ISkeletonBufferGuardService
{
    internal const int HeaderBytes = 4096;

    /// <summary>The two sites belong to one function; this is the most they may be apart (they are 0x33 apart in v1.5.4).</summary>
    internal const int MaxSiteDistance = 0x80;

    /// <summary>How far a 32-bit displacement reaches, less a margin for the jump's own length.</summary>
    internal const long ReachBytes = 0x7FF00000L;

    private const string ForeignReason = "already guarded by another module (the site holds a jump), so TAOM writes nothing at this site";

    private readonly ISkeletonBufferSettingsProvider _settings;
    private readonly ISkeletonBufferMemoryAdapter _memory;
    private readonly IDedicatedServerProvider _server;
    private readonly IModLogger _logger;

    private bool _ran;
    private long _pages;
    private bool _siteTouched;
    private long _globalAddress;
    private bool _watchPool2;
    private bool _guarded1, _guarded2;
    private long _counter1, _counter2;

    public SkeletonBufferGuardService(ISkeletonBufferSettingsProvider settings, ISkeletonBufferMemoryAdapter memory,
        IDedicatedServerProvider server, IModLogger logger)
    {
        _settings = settings;
        _memory = memory;
        _server = server;
        _logger = logger;
    }

    public SkeletonBufferTarget? Target { get; private set; }

    public void Install()
    {
        if (_ran) return;
        _ran = true;
        try
        {
            InstallCore();
        }
        catch (Exception ex)
        {
            if (_siteTouched)
                Say(_logger.LogError, SkeletonBufferLines.GuardOff(SiteUnconfirmed(ex.GetType().Name + ": " + ex.Message)));
            else
            {
                ReleasePages();
                Say(_logger.LogWarning, SkeletonBufferLines.GuardOff("the install threw " + ex.GetType().Name + ": " + ex.Message));
            }
        }
    }

    private void InstallCore()
    {
        if (_server.IsDedicatedServer)
        {
            Say(_logger.LogInfo, SkeletonBufferLines.GuardOff("dedicated server, nothing is drawn"));
            return;
        }

        var scan = Stopwatch.StartNew();
        var moduleBase = _memory.GetModuleBase(SkeletonBufferSignature.ModuleName);
        if (moduleBase == 0)
        {
            Say(_logger.LogWarning, SkeletonBufferLines.GuardOff(SkeletonBufferSignature.ModuleName + " is not loaded in this process"));
            return;
        }

        var headers = _memory.Read(moduleBase, HeaderBytes);
        if (headers == null)
        {
            Say(_logger.LogWarning, SkeletonBufferLines.GuardOff("could not read the module headers"));
            return;
        }
        var sections = PeSectionTable.Parse(headers);
        if (sections == null)
        {
            Say(_logger.LogWarning, SkeletonBufferLines.GuardOff("the module's PE headers did not parse"));
            return;
        }
        var text = PeSectionTable.Find(sections, ".text");
        if (text == null)
        {
            Say(_logger.LogWarning, SkeletonBufferLines.GuardOff("no .text section in the module headers"));
            return;
        }
        var data = PeSectionTable.Find(sections, ".data");
        if (data == null)
        {
            Say(_logger.LogWarning, SkeletonBufferLines.GuardOff("no .data section in the module headers"));
            return;
        }

        // The in-memory code, not the file: that is what the jump is written over, and another module's patch shows here.
        var code = _memory.Read(moduleBase + text.VirtualAddress, text.VirtualSize);
        if (code == null)
        {
            Say(_logger.LogWarning, SkeletonBufferLines.GuardOff("could not read the .text section"));
            return;
        }
        var found = SkeletonBufferSignature.Resolve(code, text.VirtualAddress);
        var scanMs = scan.Elapsed.TotalMilliseconds;

        if (found.WatchMatchCount != 1)
        {
            Say(_logger.LogWarning, SkeletonBufferLines.GuardOff(found.WatchMatchCount == 0
                ? "the watch signature matched nothing in .text, so this engine build differs from the one it was written for; the watch is off too"
                : "the watch signature matched 2 or more times in .text, so the site is ambiguous; the watch is off too"));
            return;
        }
        if (!(found.GlobalRva % 8 == 0 && data.Contains(found.GlobalRva, 8)))
        {
            Say(_logger.LogWarning, SkeletonBufferLines.GuardOff(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "the frame buffer global 0x{0:X} is not an aligned 8-byte slot inside .data; the watch is off too", found.GlobalRva)));
            return;
        }

        var pool2Problem = Pool2Problem(found);
        _globalAddress = moduleBase + found.GlobalRva;
        _watchPool2 = pool2Problem == null;
        _guarded1 = found.ForeignGuardPresent;
        _guarded2 = found.Pool2ForeignGuardPresent;
        PublishTarget();
        if (pool2Problem != null)
            Say(_logger.LogWarning, SkeletonBufferLines.Pool2Off(pool2Problem));
        if (_guarded1)
            Say(_logger.LogInfo, SkeletonBufferLines.GuardOffPool(1, ForeignReason));
        if (_guarded2)
            Say(_logger.LogInfo, SkeletonBufferLines.GuardOffPool(2, ForeignReason));
        if (_guarded1 && _guarded2) return;
        if (!_settings.GuardEnabled)
        {
            Say(_logger.LogInfo, SkeletonBufferLines.GuardOff("setting off (MCM Skeleton Buffer Guard)"));
            return;
        }

        var plans = new List<PoolPlan>();
        if (!_guarded1) Add(plans, PlanPool1(found, code, text.VirtualAddress));
        if (!_guarded2) Add(plans, PlanPool2(found, pool2Problem, code, text.VirtualAddress));
        if (plans.Count > 0)
            InstallGuards(moduleBase, plans, scanMs);
    }

    private static void Add(List<PoolPlan> plans, PoolPlan? plan)
    {
        if (plan != null) plans.Add(plan);
    }

    private void PublishTarget() =>
        Target = new SkeletonBufferTarget(_globalAddress, _guarded1, _counter1, _watchPool2, _guarded2, _counter2);

    /// <summary>Pool 1's site, or null (after one reason line) when it cannot be trusted.</summary>
    private PoolPlan? PlanPool1(SkeletonBufferSignatureResult found, byte[] code, int textRva)
    {
        if (found.GuardMatchCount != 1)
        {
            Say(_logger.LogWarning, SkeletonBufferLines.GuardOffPool(1, SignatureReason(found.GuardMatchCount)));
            return null;
        }
        var apart = found.GuardSiteRva - found.WatchSiteRva;
        if (apart <= 0 || apart > MaxSiteDistance)
        {
            Say(_logger.LogWarning, SkeletonBufferLines.GuardOffPool(1, string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "the two signatures are {0} bytes apart, not in the same function", apart)));
            return null;
        }
        return NewPlan(1, found.GuardSiteRva, found.ResumeRva, code, textRva);
    }

    /// <summary>
    /// Pool 2's site, or null (after one reason line). It needs the pool 2 load resolved, because the check that the site is
    /// the right function is that load's call: a <c>call rel32</c> after the load must enter the function holding the site.
    /// </summary>
    private PoolPlan? PlanPool2(SkeletonBufferSignatureResult found, string? pool2Problem, byte[] code, int textRva)
    {
        if (pool2Problem != null)
        {
            Say(_logger.LogWarning, SkeletonBufferLines.GuardOffPool(2, "the pool 2 load was not resolved (" + pool2Problem + ")"));
            return null;
        }
        if (found.Pool2GuardMatchCount != 1)
        {
            Say(_logger.LogWarning, SkeletonBufferLines.GuardOffPool(2, SignatureReason(found.Pool2GuardMatchCount)));
            return null;
        }
        if (!found.Pool2GuardCalledFromLoad)
        {
            Say(_logger.LogWarning, SkeletonBufferLines.GuardOffPool(2, "the site is not in the function the pool 2 load calls"));
            return null;
        }
        return NewPlan(2, found.Pool2GuardSiteRva, found.Pool2ResumeRva, code, textRva);
    }

    private static string SignatureReason(int matchCount) => matchCount == 0
        ? "the guard signature matched nothing in .text, so this engine build differs from the one it was written for"
        : "the guard signature matched 2 or more times in .text, so the site is ambiguous";

    private static PoolPlan NewPlan(int pool, int siteRva, int resumeRva, byte[] code, int textRva)
    {
        var original = new byte[SkeletonBufferSignature.GuardSiteLength];
        Array.Copy(code, siteRva - textRva, original, 0, original.Length);
        return new PoolPlan(pool, siteRva, resumeRva, original);
    }

    private void InstallGuards(long moduleBase, List<PoolPlan> plans, double scanMs)
    {
        // Every resume point must be reachable from the pages: the highest is the tightest bound.
        var lastResume = plans.Max(p => p.ResumeRva);
        var pages = _memory.AllocateNear(moduleBase - SkeletonBufferCave.AllocationSize, moduleBase + lastResume - ReachBytes, SkeletonBufferCave.AllocationSize);
        if (pages == 0)
        {
            Say(_logger.LogWarning, SkeletonBufferLines.GuardOff("no free memory within jump range below the module"));
            return;
        }
        _pages = pages;

        foreach (var plan in plans)
        {
            var pool1 = plan.Pool == 1;
            plan.Cave = pool1 ? pages : SkeletonBufferCave.Pool2CaveAddress(pages);
            plan.Counter = pool1 ? SkeletonBufferCave.CounterAddress(pages) : SkeletonBufferCave.Pool2CounterAddress(pages);
            plan.CaveBytes = pool1
                ? SkeletonBufferCave.BuildCave(plan.Cave, plan.Counter, moduleBase + plan.ResumeRva)
                : SkeletonBufferCave.BuildPool2Cave(plan.Cave, plan.Counter, moduleBase + plan.ResumeRva);
            plan.SiteBytes = SkeletonBufferCave.BuildSiteJump(moduleBase + plan.SiteRva, plan.Cave);
            if (plan.CaveBytes == null || plan.SiteBytes == null)
            {
                ReleasePages();
                Say(_logger.LogWarning, SkeletonBufferLines.GuardOff("the allocated pages are out of reach of a 32-bit jump"));
                return;
            }
        }

        foreach (var plan in plans)
        {
            if (_memory.Write(plan.Cave, plan.CaveBytes!)) continue;
            ReleasePages();
            Say(_logger.LogWarning, SkeletonBufferLines.GuardOff("writing the code block failed"));
            return;
        }
        // The code page becomes read-execute only after BOTH blocks are in it.
        if (!_memory.MakeExecutable(pages, SkeletonBufferCave.PageSize))
        {
            ReleasePages();
            Say(_logger.LogWarning, SkeletonBufferLines.GuardOff("protecting the code block failed"));
            return;
        }

        // The point of no return: from here the engine's code may jump into the pages. Site 1, then site 2; a write whose
        // state cannot be confirmed stops any further writing (the pages stay either way).
        var anyWritten = false;
        var stopped = false;
        foreach (var plan in plans)
        {
            if (stopped)
            {
                Say(_logger.LogWarning, SkeletonBufferLines.GuardOffPool(plan.Pool,
                    "not attempted: the state of an earlier site write could not be confirmed"));
                continue;
            }

            _siteTouched = true;
            var result = EngineCodePatchResult.Unknown;
            var unconfirmed = "the write failed and the original bytes could not be read back";
            try
            {
                result = _memory.PatchCode(moduleBase + plan.SiteRva, plan.Original, plan.SiteBytes!);
            }
            catch (Exception ex)
            {
                unconfirmed = ex.GetType().Name + ": " + ex.Message;
            }
            switch (result)
            {
                case EngineCodePatchResult.Written:
                    anyWritten = true;
                    if (plan.Pool == 1) { _guarded1 = true; _counter1 = plan.Counter; }
                    else { _guarded2 = true; _counter2 = plan.Counter; }
                    PublishTarget();
                    Say(_logger.LogInfo, SkeletonBufferLines.GuardOn(plan.Pool, plan.SiteRva, plan.ResumeRva, plan.Cave, plan.Counter, scanMs));
                    break;
                case EngineCodePatchResult.NotWritten:
                    _siteTouched = anyWritten;
                    Say(_logger.LogWarning, SkeletonBufferLines.GuardOffPool(plan.Pool,
                        "the write to the engine code failed and the engine code was left unchanged"));
                    break;
                default:
                    stopped = true;
                    Say(_logger.LogError, SkeletonBufferLines.GuardOffPool(plan.Pool, SiteUnconfirmed(unconfirmed)));
                    break;
            }
        }
        if (!_siteTouched)
            ReleasePages();
    }

    private static string SiteUnconfirmed(string detail) =>
        "the state of the engine code at a site could not be confirmed (" + detail
            + "); the pages are kept in case a site jumps into them. Restart the game, and send this log if the battle misbehaves";

    /// <summary>Why the second pool cannot be watched, or null when it can: its load matched once and reads the watch site's global.</summary>
    private static string? Pool2Problem(SkeletonBufferSignatureResult found)
    {
        if (found.Pool2MatchCount == 0)
            return "the pool 2 signature matched nothing in .text";
        if (found.Pool2MatchCount != 1)
            return "the pool 2 signature matched 2 or more times in .text, so the site is ambiguous";
        if (found.Pool2GlobalRva != found.GlobalRva)
            return string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "the pool 2 load reads the global 0x{0:X}, not the watch site's 0x{1:X}", found.Pool2GlobalRva, found.GlobalRva);
        return null;
    }

    private void ReleasePages()
    {
        var pages = _pages;
        _pages = 0;
        if (pages != 0)
            _memory.Free(pages);
    }

    /// <summary>One pool's site, as resolved: the RVAs, the engine's 13 original bytes and, once the pages exist, its block and jump.</summary>
    private sealed class PoolPlan
    {
        internal PoolPlan(int pool, int siteRva, int resumeRva, byte[] original)
        {
            Pool = pool;
            SiteRva = siteRva;
            ResumeRva = resumeRva;
            Original = original;
        }

        internal int Pool { get; }
        internal int SiteRva { get; }
        internal int ResumeRva { get; }
        internal byte[] Original { get; }
        internal long Cave { get; set; }
        internal long Counter { get; set; }
        internal byte[]? CaveBytes { get; set; }
        internal byte[]? SiteBytes { get; set; }
    }

    /// <summary>One log line; a failing logger costs only the line.</summary>
    private static void Say(Action<string> level, string line)
    {
        try { level(line); }
        catch { /* diagnostic only */ }
    }
}
