using System;
using System.Runtime.CompilerServices;
using MonoMod.Core.Platforms.Architectures.AltEntryFactories;
using MonoMod.Core.Utils;
using MonoMod.Utils;

namespace MonoMod.Core.Platforms.Architectures;

internal sealed class x86Arch : IArchitecture
{
	private sealed class Abs32Kind : DetourKindBase
	{
		public static readonly Abs32Kind Instance = new Abs32Kind();

		public override int Size => 6;

		public override int GetBytes(IntPtr from, IntPtr to, Span<byte> buffer, object? data, out IDisposable? allocHandle)
		{
			buffer[0] = 104;
			Unsafe.WriteUnaligned(ref buffer[1], Unsafe.As<IntPtr, int>(ref to));
			buffer[5] = 195;
			allocHandle = null;
			return Size;
		}

		public override bool TryGetRetargetInfo(NativeDetourInfo orig, IntPtr to, int maxSize, out NativeDetourInfo retargetInfo)
		{
			retargetInfo = orig with
			{
				To = to
			};
			return true;
		}

		public override int DoRetarget(NativeDetourInfo origInfo, IntPtr to, Span<byte> buffer, object? data, out IDisposable? allocationHandle, out bool needsRepatch, out bool disposeOldAlloc)
		{
			needsRepatch = true;
			disposeOldAlloc = true;
			return GetBytes(origInfo.From, to, buffer, data, out allocationHandle);
		}
	}

	private BytePatternCollection? lazyKnownMethodThunks;

	private static readonly Func<BytePatternCollection> createKnownMethodThunksFunc = CreateKnownMethodThunks;

	private readonly ISystem system;

	private const int WinThisVtableThunkIndexOffs = 7;

	private const int SpecEntryStubArgOffs = 1;

	private const int SpecEntryStubTargetOffs = 6;

	public ArchitectureKind Target => ArchitectureKind.x86;

	public ArchitectureFeature Features => ArchitectureFeature.CreateAltEntryPoint;

	public BytePatternCollection KnownMethodThunks => Helpers.GetOrInit(ref lazyKnownMethodThunks, createKnownMethodThunksFunc);

	public IAltEntryFactory AltEntryFactory { get; }

	private static ReadOnlySpan<byte> WinThisVtableProxyThunk => new byte[12]
	{
		139, 73, 4, 139, 1, 255, 160, 85, 85, 85,
		85, 204
	};

	private static ReadOnlySpan<byte> SpecEntryStub => new byte[12]
	{
		184, 0, 0, 0, 0, 185, 0, 0, 0, 0,
		255, 225
	};

	private static BytePatternCollection CreateKnownMethodThunks()
	{
		RuntimeKind runtime = PlatformDetection.Runtime;
		if ((uint)(runtime - 1) <= 1u)
		{
			return new BytePatternCollection(new BytePattern(new AddressMeaning(AddressKind.Rel32, 16), 184, 65280, 65280, 65280, 65280, 144, 232, 65280, 65280, 65280, 65280, 233, 65282, 65282, 65282, 65282), new BytePattern(new AddressMeaning(AddressKind.Rel32, 5), true, 233, 65282, 65282, 65282, 65282, 95), new BytePattern(new AddressMeaning(AddressKind.PrecodeFixupThunkRel32, 5), true, 232, 65282, 65282, 65282, 65282, 94), new BytePattern(new AddressMeaning(AddressKind.PrecodeFixupThunkRel32, 5), true, 232, 65282, 65282, 65282, 65282, 204), new BytePattern(new AddressMeaning(AddressKind.Abs32 | AddressKind.Indirect), true, 255, 37, 65282, 65282, 65282, 65282, 161, 65280, 65280, 65280, 65280, 255, 37, 65280, 65280, 65280, 65280), new BytePattern(new AddressMeaning(AddressKind.PrecodeFixupThunkAbs32 | AddressKind.Indirect), true, 161, 65280, 65280, 65280, 65280, 255, 37, 65282, 65282, 65282, 65282), null);
		}
		return new BytePatternCollection();
	}

	public NativeDetourInfo ComputeDetourInfo(IntPtr from, IntPtr to, int maxSizeHint = -1)
	{
		x86Shared.FixSizeHint(ref maxSizeHint);
		if (x86Shared.TryRel32Detour(from, to, maxSizeHint, out var info))
		{
			return info;
		}
		if (maxSizeHint < Abs32Kind.Instance.Size)
		{
			bool isEnabled;
			_003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMMDbgLog.DebugLogWarningStringHandler message = new _003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMMDbgLog.DebugLogWarningStringHandler(79, 1, out isEnabled);
			if (isEnabled)
			{
				message.AppendLiteral("Size too small for all known detour kinds; defaulting to Abs32. provided size: ");
				message.AppendFormatted(maxSizeHint);
			}
			_003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMMDbgLog.Warning(ref message);
		}
		return new NativeDetourInfo(from, to, Abs32Kind.Instance, null);
	}

	public int GetDetourBytes(NativeDetourInfo info, Span<byte> buffer, out IDisposable? allocationHandle)
	{
		return DetourKindBase.GetDetourBytes(info, buffer, out allocationHandle);
	}

	public NativeDetourInfo ComputeRetargetInfo(NativeDetourInfo detour, IntPtr to, int maxSizeHint = -1)
	{
		x86Shared.FixSizeHint(ref maxSizeHint);
		if (DetourKindBase.TryFindRetargetInfo(detour, to, maxSizeHint, out var retargetInfo))
		{
			return retargetInfo;
		}
		return ComputeDetourInfo(detour.From, to, maxSizeHint);
	}

	public int GetRetargetBytes(NativeDetourInfo original, NativeDetourInfo retarget, Span<byte> buffer, out IDisposable? allocationHandle, out bool needsRepatch, out bool disposeOldAlloc)
	{
		return DetourKindBase.DoRetarget(original, retarget, buffer, out allocationHandle, out needsRepatch, out disposeOldAlloc);
	}

	public x86Arch(ISystem system)
	{
		this.system = system;
		AltEntryFactory = new IcedAltEntryFactory(system, 32);
	}

	public ReadOnlyMemory<IAllocatedMemory> CreateNativeVtableProxyStubs(IntPtr vtableBase, int vtableSize)
	{
		OSKind kernel = PlatformDetection.OS.GetKernel();
		bool premulOffset = true;
		if (kernel.Is(OSKind.Windows))
		{
			ReadOnlySpan<byte> winThisVtableProxyThunk = WinThisVtableProxyThunk;
			int indexOffs = 7;
			return Shared.CreateVtableStubs(system, vtableBase, vtableSize, winThisVtableProxyThunk, indexOffs, premulOffset);
		}
		throw new PlatformNotSupportedException();
	}

	public IAllocatedMemory CreateSpecialEntryStub(IntPtr target, IntPtr argument)
	{
		Span<byte> span = stackalloc byte[SpecEntryStub.Length];
		SpecEntryStub.CopyTo(span);
		Unsafe.WriteUnaligned(ref span[6], target);
		Unsafe.WriteUnaligned(ref span[1], argument);
		return Shared.CreateSingleExecutableStub(system, span);
	}
}
