using System;
using System.Diagnostics;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.MissionPerf.AnimMemory;

/// <summary>
/// Finds the engine's on-demand animation clip byte counter and its 12 MiB budget once per process,
/// by the <see cref="ClipBudgetSignature"/> scan over the module's code, then reads the counter on
/// request. It only reads. The two target addresses, the counter and the budget float, are checked for
/// alignment and for lying inside <c>.data</c> and <c>.rdata</c> of the module's own section table
/// before either is read, because an access violation would kill the game; any mismatch turns the probe
/// off for the process with one <c>[AnimMem] disabled</c> line naming the reason. Taken on trust, not
/// checked: the header page and the code copy that come first (the copy's range is that same table),
/// and that the module stays mapped (<c>GetModuleHandleW</c> takes no reference, and the counter
/// address is cached for the process). Outside every check: the engine's own clip query behind
/// <see cref="IsAnyClipLoading"/>, which walks the engine's clip record list without a lock; what can
/// grow that list or free a record was not traced.
/// </summary>
public sealed class AnimMemoryProbe : IAnimClipMemoryProbe
{
    private const int HeaderBytes = 4096;

    private enum State { NotTried, Armed, Disabled }

    private readonly INativeModuleMemoryAdapter _memory;
    private readonly IAnimationLoadingAdapter _loading;
    private readonly IModLogger _logger;
    private State _state = State.NotTried;
    private long _counterAddress;

    public AnimMemoryProbe(INativeModuleMemoryAdapter memory, IAnimationLoadingAdapter loading, IModLogger logger)
    {
        _memory = memory;
        _loading = loading;
        _logger = logger;
    }

    public int BudgetBytes { get; private set; }

    public bool EnsureArmed()
    {
        if (_state == State.NotTried)
            Arm();
        return _state == State.Armed;
    }

    public int ReadLoadedBytes()
    {
        if (_state != State.Armed)
            throw new InvalidOperationException("the clip memory probe is not armed");
        return Checked(_memory.ReadInt32(_counterAddress));
    }

    public bool IsAnyClipLoading() => _loading.IsAnyAnimationLoadingFromDisk();

    private void Arm()
    {
        try
        {
            var watch = Stopwatch.StartNew();
            var moduleBase = _memory.GetModuleBase(ClipBudgetSignature.ModuleName);
            if (moduleBase == 0)
            {
                Disable(ClipBudgetSignature.ModuleName + " is not loaded in this process");
                return;
            }

            // Read on trust: GetModuleHandleW found the module mapped, and the first page of a mapped
            // image holds its headers. Nothing pins the module, and nothing here could detect it going.
            var sections = PeSectionTable.Parse(_memory.Copy(moduleBase, HeaderBytes));
            if (sections == null)
            {
                Disable("the module's PE headers did not parse");
                return;
            }
            var text = PeSectionTable.Find(sections, ".text");
            if (text == null) { Disable("no .text section in the module headers"); return; }
            var rdata = PeSectionTable.Find(sections, ".rdata");
            if (rdata == null) { Disable("no .rdata section in the module headers"); return; }
            var data = PeSectionTable.Find(sections, ".data");
            if (data == null) { Disable("no .data section in the module headers"); return; }

            // The code copy is about 10 MB, once per process; it is dropped as soon as it is scanned. Its
            // range is the section table just parsed from the same module: trusted, not independent.
            var match = ClipBudgetSignature.Resolve(
                _memory.Copy(moduleBase + text.VirtualAddress, text.VirtualSize), text.VirtualAddress);
            if (match.MatchCount == 0)
            {
                Disable("the eviction-pass signature matched nothing in .text, so this engine build differs from the one it was written for");
                return;
            }
            if (match.MatchCount > 1)
            {
                Disable("the eviction-pass signature matched 2 or more times in .text, so the site is ambiguous");
                return;
            }

            // Both targets are proven inside their sections before the first read of either.
            if (!(match.CounterRva % 4 == 0 && data.Contains(match.CounterRva, 4)))
            {
                Disable(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "the loaded-bytes target 0x{0:X} is not an aligned 4-byte address inside .data", match.CounterRva));
                return;
            }
            if (!(match.BudgetRva % 4 == 0 && rdata.Contains(match.BudgetRva, 4)))
            {
                Disable(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "the budget target 0x{0:X} is not an aligned 4-byte address inside .rdata", match.BudgetRva));
                return;
            }

            var budget = BitConverter.ToSingle(BitConverter.GetBytes(_memory.ReadInt32(moduleBase + match.BudgetRva)), 0);
            // Positive requirement: NaN fails it.
            if (!(budget == ClipBudgetSignature.ExpectedBudgetBytes))
            {
                Disable(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "the budget float reads {0}, expected {1} (12 MiB)",
                    budget.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
                    ClipBudgetSignature.ExpectedBudgetBytes.ToString("R", System.Globalization.CultureInfo.InvariantCulture)));
                return;
            }

            var counterAddress = moduleBase + match.CounterRva;
            if (Checked(_memory.ReadInt32(counterAddress)) < 0)
                return;

            _counterAddress = counterAddress;
            BudgetBytes = (int)ClipBudgetSignature.ExpectedBudgetBytes;
            _state = State.Armed;
            _logger.LogInfo(AnimMemLine.Armed(moduleBase, text.VirtualAddress, text.VirtualSize, match.LoadSiteRva,
                match.BudgetSiteRva, match.CounterRva, match.BudgetRva, BudgetBytes, watch.Elapsed.TotalMilliseconds));
        }
        catch (Exception ex)
        {
            Disable("reading module memory threw " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    /// <summary>
    /// A byte total kept with atomic adds is never negative, so a negative read means the signature
    /// resolved to something else: the probe turns itself off and says so. Returns the value unchanged.
    /// </summary>
    private int Checked(int bytes)
    {
        if (bytes < 0)
            Disable(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "the loaded-bytes counter reads {0}, a negative byte count", bytes));
        return bytes;
    }

    private void Disable(string reason)
    {
        if (_state == State.Disabled)
            return;
        _state = State.Disabled;
        try { _logger.LogInfo(AnimMemLine.Disabled(reason)); }
        catch { /* diagnostic only */ }
    }
}
