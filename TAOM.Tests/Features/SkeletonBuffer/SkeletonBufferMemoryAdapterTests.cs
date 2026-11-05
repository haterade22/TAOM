// Adapted from yotthani's VanillaTuning (MIT, (c) 2026 yotthani), frame-buffer-guard and frame-buffer-watch.
using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Adapters;
using TAOM.Features.SkeletonBuffer;

namespace TAOM.Tests.Features.SkeletonBuffer;

/// <summary>
/// The native write adapter against this test process's own memory (never the game): allocation near a module, safe
/// reads, the cave written and protected, and the machine code of the cave EXECUTED here through a small thunk, which
/// proves the two code blocks (43 and 42 bytes) reserve, refuse and count as the design says. Runs wherever the test host is a 64-bit process.
/// </summary>
[TestClass]
public class SkeletonBufferMemoryAdapterTests
{
    private delegate int Reserve(IntPtr fill, int entries);

    private delegate int ReservePool2(IntPtr fill, int entries, IntPtr probe);

    private const long Sentinel = 0x1122334455667788;

    private readonly SkeletonBufferMemoryAdapter _sut = new SkeletonBufferMemoryAdapter();
    private long _pages;

    [TestInitialize]
    public void RequireX64()
    {
        if (!Environment.Is64BitProcess) Assert.Inconclusive("the cave is x64 machine code");
        _pages = 0;
    }

    [TestCleanup]
    public void FreePages()
    {
        if (_pages != 0) _sut.Free(_pages);
    }

    private long AllocateBelowKernel32(out long kernel)
    {
        kernel = _sut.GetModuleBase("kernel32.dll");
        Assert.AreNotEqual(0L, kernel);
        var pages = _sut.AllocateNear(kernel - 0x2000, kernel - 0x70000000L, 0x2000);
        if (pages == 0) Assert.Inconclusive("no free address space below kernel32 in this process");
        _pages = pages;
        return pages;
    }

    [TestMethod]
    public void GetModuleBase_LoadedAndMissingModules_ReturnBaseAndZero()
    {
        Assert.AreNotEqual(0L, _sut.GetModuleBase("kernel32.dll"));
        Assert.AreEqual(0L, _sut.GetModuleBase("TaomNoSuchModule.dll"));
    }

    [TestMethod]
    public void AllocateNear_BelowAModule_ReturnsZeroedWritablePagesInsideTheRange()
    {
        var pages = AllocateBelowKernel32(out var kernel);

        Assert.IsTrue(pages <= kernel - 0x2000);
        Assert.IsTrue(pages >= kernel - 0x70000000L);
        Assert.AreEqual(0L, pages % 0x10000, "64 KB aligned");
        CollectionAssert.AreEqual(new byte[0x2000], _sut.Read(pages, 0x2000));
        Assert.IsTrue(_sut.Write(pages + 0x40, new byte[] { 1, 2, 3 }));
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, _sut.Read(pages + 0x40, 3));
    }

    [TestMethod]
    public void AllocateNear_EmptyRange_ReturnsZero()
    {
        Assert.AreEqual(0L, _sut.AllocateNear(0x10000, 0x20000, 0x2000), "highest below lowest");
    }

    [TestMethod]
    public void Free_AllocatedPages_ReleasesThemSoReadsFail()
    {
        var pages = AllocateBelowKernel32(out _);

        Assert.IsTrue(_sut.Free(pages));
        _pages = 0;

        Assert.IsNull(_sut.Read(pages, 4));
    }

    [TestMethod]
    public void Read_UnmappedAddress_ReturnsNullInsteadOfCrashing()
    {
        Assert.IsNull(_sut.Read(0x10, 16));
        Assert.IsNull(_sut.ReadInt32(0x10));
        Assert.IsNull(_sut.ReadInt64(0x10));
    }

    [TestMethod]
    public void ReadInt32AndInt64_AllocatedPages_ReturnTheStoredValues()
    {
        var pages = AllocateBelowKernel32(out _);
        _sut.Write(pages, BitConverter.GetBytes(0x1122334455667788L));

        Assert.AreEqual(0x1122334455667788L, _sut.ReadInt64(pages));
        Assert.AreEqual(0x55667788, _sut.ReadInt32(pages));
    }

    [TestMethod]
    public void PatchCode_ExpectedBytesPresent_WritesThemAndKeepsThePageExecutable()
    {
        var pages = AllocateBelowKernel32(out _);
        var original = new byte[] { 0x90, 0x90, 0x90, 0x90, 0xC3 };
        _sut.Write(pages, original);
        Assert.IsTrue(_sut.MakeExecutable(pages, 0x1000));

        var result = _sut.PatchCode(pages, original, new byte[] { 0x90, 0x90, 0x90, 0x90, 0x90 });

        Assert.AreEqual(EngineCodePatchResult.Written, result);
        CollectionAssert.AreEqual(new byte[] { 0x90, 0x90, 0x90, 0x90, 0x90 }, _sut.Read(pages, 5));
        // The page is executable again after the patch: a ret at its start runs.
        _sut.PatchCode(pages, new byte[] { 0x90 }, new byte[] { 0xC3 });
        Marshal.GetDelegateForFunctionPointer<Action>(new IntPtr(pages))();
    }

    [TestMethod]
    public void PatchCode_BytesDifferFromExpected_WritesNothing()
    {
        var pages = AllocateBelowKernel32(out _);
        _sut.Write(pages, new byte[] { 1, 2, 3, 4 });
        _sut.MakeExecutable(pages, 0x1000);

        var result = _sut.PatchCode(pages, new byte[] { 9, 9, 9, 9 }, new byte[] { 7, 7, 7, 7 });

        Assert.AreEqual(EngineCodePatchResult.NotWritten, result);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, _sut.Read(pages, 4));
    }

    [TestMethod]
    public void PatchCode_UnmappedAddress_WritesNothing()
    {
        Assert.AreEqual(EngineCodePatchResult.NotWritten, _sut.PatchCode(0x10, new byte[] { 1 }, new byte[] { 2 }));
    }

    // ---- the cave, executed ----

    private Reserve BuildReserveThunk(out long counter)
    {
        var pages = AllocateBelowKernel32(out _);
        counter = SkeletonBufferCave.CounterAddress(pages);
        var thunk = pages + 0x100;
        var resume = thunk + 0x18;
        var cave = SkeletonBufferCave.BuildCave(pages, counter, resume);
        Assert.IsNotNull(cave);
        Assert.IsTrue(_sut.Write(pages, cave!));

        var code = new byte[]
        {
            0x55,                         // push rbp
            0x41, 0x54,                   // push r12
            0x41, 0x57,                   // push r15
            0x56,                         // push rsi
            0x48, 0x83, 0xEC, 0x28,       // sub rsp, 28h
            0x48, 0x89, 0xCD,             // mov rbp, rcx      (the fill counter)
            0x41, 0x89, 0xD4,             // mov r12d, edx     (entries this skeleton needs)
            0x4D, 0x31, 0xFF,             // xor r15, r15
            0xE9, 0, 0, 0, 0,             // jmp cave
            0x89, 0xF0,                   // resume: mov eax, esi   (the first slot it was given)
            0x48, 0x83, 0xC4, 0x28,       // add rsp, 28h
            0x5E,                         // pop rsi
            0x41, 0x5F,                   // pop r15
            0x41, 0x5C,                   // pop r12
            0x5D,                         // pop rbp
            0xC3,                         // ret
        };
        BitConverter.GetBytes((int)(pages - (thunk + 0x18))).CopyTo(code, 0x14);
        Assert.IsTrue(_sut.Write(thunk, code));
        Assert.IsTrue(_sut.MakeExecutable(pages, 0x1000));
        return Marshal.GetDelegateForFunctionPointer<Reserve>(new IntPtr(thunk));
    }

    private static IntPtr NewFill(int value)
    {
        var fill = Marshal.AllocHGlobal(16);
        Marshal.WriteInt32(fill, value);
        return fill;
    }

    [TestMethod]
    public void Cave_Executed_ReservationThatFitsIsGrantedAndNotCounted()
    {
        var reserve = BuildReserveThunk(out var counter);
        var fill = NewFill(100);
        try
        {
            var first = reserve(fill, 28);

            Assert.AreEqual(100, first, "the first slot is the fill before");
            Assert.AreEqual(128, Marshal.ReadInt32(fill));
            Assert.AreEqual(0, _sut.ReadInt32(counter));
        }
        finally { Marshal.FreeHGlobal(fill); }
    }

    [TestMethod]
    public void Cave_Executed_ReservationEndingExactlyAtTheEndIsGranted()
    {
        var reserve = BuildReserveThunk(out var counter);
        var fill = NewFill(65536 - 28);
        try
        {
            var first = reserve(fill, 28);

            Assert.AreEqual(65536 - 28, first);
            Assert.AreEqual(65536, Marshal.ReadInt32(fill));
            Assert.AreEqual(0, _sut.ReadInt32(counter));
        }
        finally { Marshal.FreeHGlobal(fill); }
    }

    [TestMethod]
    public void Cave_Executed_ReservationPastTheEnd_IsTakenBackCountedAndSentToTheFirstSlots()
    {
        var reserve = BuildReserveThunk(out var counter);
        var fill = NewFill(65536 - 27);
        try
        {
            var first = reserve(fill, 28);
            var second = reserve(fill, 28);

            Assert.AreEqual(0, first, "the first slots of the buffer");
            Assert.AreEqual(0, second);
            Assert.AreEqual(65536 - 27, Marshal.ReadInt32(fill), "the reservation was taken back");
            Assert.AreEqual(2, _sut.ReadInt32(counter));
        }
        finally { Marshal.FreeHGlobal(fill); }
    }

    [TestMethod]
    public void Cave_Executed_ASmallerReservationStillFitsAfterAnOverflowWasRefused()
    {
        var reserve = BuildReserveThunk(out var counter);
        var fill = NewFill(65536 - 10);
        try
        {
            reserve(fill, 28);
            var small = reserve(fill, 10);

            Assert.AreEqual(65536 - 10, small);
            Assert.AreEqual(65536, Marshal.ReadInt32(fill));
            Assert.AreEqual(1, _sut.ReadInt32(counter));
        }
        finally { Marshal.FreeHGlobal(fill); }
    }

    // ---- cave 2, executed ----

    /// <summary>What the pool 2 thunk saw at the resume point: the stack slot at [rsp+20h], rcx and edx.</summary>
    private sealed class Probe
    {
        public long Slot, Rcx;
        public int Edx;
    }

    private ReservePool2 BuildReserve2Thunk(out long counter1, out long counter2)
    {
        var pages = AllocateBelowKernel32(out _);
        counter1 = SkeletonBufferCave.CounterAddress(pages);
        counter2 = SkeletonBufferCave.Pool2CounterAddress(pages);
        var cave2 = SkeletonBufferCave.Pool2CaveAddress(pages);
        var thunk = pages + 0x100;
        var resume = thunk + 0x15;

        // Pool 1's cave sits on the same code page, as in the game: it must stay intact.
        var cave1 = SkeletonBufferCave.BuildCave(pages, counter1, resume);
        var cave2Bytes = SkeletonBufferCave.BuildPool2Cave(cave2, counter2, resume);
        Assert.IsNotNull(cave1);
        Assert.IsNotNull(cave2Bytes);
        Assert.IsTrue(_sut.Write(pages, cave1!));
        Assert.IsTrue(_sut.Write(cave2, cave2Bytes!));

        var code = new byte[]
        {
            0x41, 0x57,                                                 // 00 push r15
            0x48, 0x83, 0xEC, 0x28,                                     // 02 sub rsp, 28h        ([rsp+20h] is in the frame)
            0x49, 0xBF, 0x88, 0x77, 0x66, 0x55, 0x44, 0x33, 0x22, 0x11, // 06 mov r15, sentinel   (what the cave must save)
            0xE9, 0, 0, 0, 0,                                           // 10 jmp cave 2
            0x48, 0x8B, 0x44, 0x24, 0x20,                               // 15 resume: mov rax, [rsp+20h]
            0x49, 0x89, 0x00,                                           // 1A mov [r8], rax
            0x49, 0x89, 0x48, 0x08,                                     // 1D mov [r8+8], rcx
            0x41, 0x89, 0x50, 0x10,                                     // 21 mov [r8+16], edx
            0x44, 0x89, 0xF8,                                           // 25 mov eax, r15d       (the start index it was given)
            0x48, 0x83, 0xC4, 0x28,                                     // 28 add rsp, 28h
            0x41, 0x5F,                                                 // 2C pop r15
            0xC3,                                                       // 2E ret
        };
        BitConverter.GetBytes((int)(cave2 - (thunk + 0x15))).CopyTo(code, 0x11);
        Assert.IsTrue(_sut.Write(thunk, code));
        Assert.IsTrue(_sut.MakeExecutable(pages, 0x1000));
        return Marshal.GetDelegateForFunctionPointer<ReservePool2>(new IntPtr(thunk));
    }

    private static Probe Run(ReservePool2 reserve, IntPtr fill, int entries, out int start)
    {
        var probe = Marshal.AllocHGlobal(24);
        try
        {
            start = reserve(fill, entries, probe);
            return new Probe { Slot = Marshal.ReadInt64(probe), Rcx = Marshal.ReadInt64(probe, 8), Edx = Marshal.ReadInt32(probe, 16) };
        }
        finally { Marshal.FreeHGlobal(probe); }
    }

    private static void AssertRegistersUntouched(Probe probe, IntPtr fill, int entries)
    {
        Assert.AreEqual(Sentinel, probe.Slot, "r15 is saved to [rsp+20h] before it is clobbered");
        Assert.AreEqual(fill.ToInt64(), probe.Rcx, "rcx is unchanged");
        Assert.AreEqual(entries, probe.Edx, "edx is unchanged");
    }

    [TestMethod]
    public void Cave2_Executed_ReservationThatFitsReturnsTheOldFillAndIsNotCounted()
    {
        var reserve = BuildReserve2Thunk(out var counter1, out var counter2);
        var fill = NewFill(100);
        try
        {
            var probe = Run(reserve, fill, 8192, out var start);

            Assert.AreEqual(100, start, "the start index is the fill before");
            Assert.AreEqual(100 + 8192, Marshal.ReadInt32(fill));
            Assert.AreEqual(0, _sut.ReadInt32(counter2));
            Assert.AreEqual(0, _sut.ReadInt32(counter1));
            AssertRegistersUntouched(probe, fill, 8192);
        }
        finally { Marshal.FreeHGlobal(fill); }
    }

    [TestMethod]
    public void Cave2_Executed_ReservationEndingExactlyAt262144IsGranted()
    {
        var reserve = BuildReserve2Thunk(out _, out var counter2);
        var fill = NewFill(262144 - 28);
        try
        {
            var probe = Run(reserve, fill, 28, out var start);

            Assert.AreEqual(262144 - 28, start);
            Assert.AreEqual(262144, Marshal.ReadInt32(fill));
            Assert.AreEqual(0, _sut.ReadInt32(counter2));
            AssertRegistersUntouched(probe, fill, 28);
        }
        finally { Marshal.FreeHGlobal(fill); }
    }

    [TestMethod]
    public void Cave2_Executed_ReservationOnePastTheEnd_IsTakenBackCountedAndReturnsZero()
    {
        var reserve = BuildReserve2Thunk(out var counter1, out var counter2);
        var fill = NewFill(262144 - 27);
        try
        {
            var first = Run(reserve, fill, 28, out var firstStart);
            var second = Run(reserve, fill, 28, out var secondStart);

            Assert.AreEqual(0, firstStart, "the pool's first slots");
            Assert.AreEqual(0, secondStart);
            Assert.AreEqual(262144 - 27, Marshal.ReadInt32(fill), "the reservation was taken back, the fill is unchanged");
            Assert.AreEqual(2, _sut.ReadInt32(counter2));
            Assert.AreEqual(0, _sut.ReadInt32(counter1), "pool 1's counter is a different slot");
            AssertRegistersUntouched(first, fill, 28);
            AssertRegistersUntouched(second, fill, 28);
        }
        finally { Marshal.FreeHGlobal(fill); }
    }

    [TestMethod]
    public void Cave2_Executed_ASmallerReservationStillFitsAfterAnOverflowWasRefused()
    {
        var reserve = BuildReserve2Thunk(out _, out var counter2);
        var fill = NewFill(262144 - 10);
        try
        {
            Run(reserve, fill, 28, out _);
            Run(reserve, fill, 10, out var small);

            Assert.AreEqual(262144 - 10, small);
            Assert.AreEqual(262144, Marshal.ReadInt32(fill));
            Assert.AreEqual(1, _sut.ReadInt32(counter2));
        }
        finally { Marshal.FreeHGlobal(fill); }
    }
}
