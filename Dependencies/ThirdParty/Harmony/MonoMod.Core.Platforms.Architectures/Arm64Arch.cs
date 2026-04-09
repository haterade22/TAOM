using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using MonoMod.Core.Utils;
using MonoMod.Utils;

namespace MonoMod.Core.Platforms.Architectures;

internal sealed class Arm64Arch : IArchitecture
{
	private sealed class BranchRegisterKind : DetourKindBase
	{
		public static readonly BranchRegisterKind Instance = new BranchRegisterKind();

		public override int Size => 16;

		public override int GetBytes(IntPtr from, IntPtr to, Span<byte> buffer, object? data, out IDisposable? allocHandle)
		{
			((ReadOnlySpan<byte>)new byte[16]
			{
				73, 0, 0, 88, 32, 1, 31, 214, 0, 0,
				0, 0, 0, 0, 0, 0
			}).CopyTo(buffer);
			Unsafe.WriteUnaligned(ref buffer[8], (ulong)(long)to);
			allocHandle = null;
			bool isEnabled;
			_003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMMDbgLog.DebugLogTraceStringHandler message = new _003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMMDbgLog.DebugLogTraceStringHandler(29, 2, out isEnabled);
			if (isEnabled)
			{
				message.AppendLiteral("Detouring arm64 from 0x");
				message.AppendFormatted(from, "X16");
				message.AppendLiteral(" to 0x");
				message.AppendFormatted(to, "X16");
			}
			_003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMMDbgLog.Trace(ref message);
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

	private readonly ISystem System;

	public ArchitectureKind Target => ArchitectureKind.Arm64;

	public ArchitectureFeature Features => ArchitectureFeature.Immediate64;

	public BytePatternCollection KnownMethodThunks => Helpers.GetOrInit(ref lazyKnownMethodThunks, CreateKnownMethodThunks);

	public IAltEntryFactory AltEntryFactory => null;

	public Arm64Arch(ISystem system)
	{
		System = system;
	}

	public NativeDetourInfo ComputeDetourInfo(IntPtr from, IntPtr target, int maxSizeHint)
	{
		x86Shared.FixSizeHint(ref maxSizeHint);
		if (maxSizeHint < BranchRegisterKind.Instance.Size)
		{
			bool isEnabled;
			_003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMMDbgLog.DebugLogWarningStringHandler message = new _003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMMDbgLog.DebugLogWarningStringHandler(88, 1, out isEnabled);
			if (isEnabled)
			{
				message.AppendLiteral("Size too small for all known detour kinds! Defaulting to BranchRegister. provided size: ");
				message.AppendFormatted(maxSizeHint);
			}
			_003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMMDbgLog.Warning(ref message);
		}
		return new NativeDetourInfo(from, target, BranchRegisterKind.Instance, null);
	}

	public int GetDetourBytes(NativeDetourInfo info, Span<byte> buffer, out IDisposable? allocationHandle)
	{
		return DetourKindBase.GetDetourBytes(info, buffer, out allocationHandle);
	}

	public NativeDetourInfo ComputeRetargetInfo(NativeDetourInfo detour, IntPtr target, int maxSizeHint = -1)
	{
		x86Shared.FixSizeHint(ref maxSizeHint);
		if (DetourKindBase.TryFindRetargetInfo(detour, target, maxSizeHint, out var retargetInfo))
		{
			return retargetInfo;
		}
		return ComputeDetourInfo(detour.From, target, maxSizeHint);
	}

	public int GetRetargetBytes(NativeDetourInfo original, NativeDetourInfo retarget, Span<byte> buffer, out IDisposable? allocationHandle, out bool needsRepatch, out bool disposeOldAlloc)
	{
		return DetourKindBase.DoRetarget(original, retarget, buffer, out allocationHandle, out needsRepatch, out disposeOldAlloc);
	}

	public ReadOnlyMemory<IAllocatedMemory> CreateNativeVtableProxyStubs(IntPtr vtableBase, int vtableSize)
	{
		return Shared.CreateVtableStubs(stubData: new byte[28]
		{
			0, 4, 64, 249, 9, 0, 64, 249, 138, 0,
			0, 24, 41, 1, 10, 139, 41, 1, 64, 249,
			32, 1, 31, 214, 0, 0, 0, 0
		}, system: System, vtableBase: vtableBase, vtableSize: vtableSize, indexOffs: 24, premulOffset: true);
	}

	public IAllocatedMemory CreateSpecialEntryStub(IntPtr target, IntPtr argument)
	{
		ReadOnlySpan<byte> readOnlySpan = new byte[32]
		{
			137, 0, 0, 88, 170, 0, 0, 88, 64, 1,
			31, 214, 31, 32, 3, 213, 0, 0, 0, 0,
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
			0, 0
		};
		Span<byte> span = stackalloc byte[readOnlySpan.Length];
		readOnlySpan.CopyTo(span);
		Unsafe.WriteUnaligned(ref span[16], argument);
		Unsafe.WriteUnaligned(ref span[24], target);
		return Shared.CreateSingleExecutableStub(System, span);
	}

	private static BytePatternCollection CreateKnownMethodThunks()
	{
		RuntimeKind runtime = PlatformDetection.Runtime;
		if ((uint)(runtime - 1) <= 1u)
		{
			List<BytePattern> list = new List<BytePattern>();
			list.Add(new BytePattern(new AddressMeaning(AddressKind.Abs64), mustMatchAtStart: true, new byte[24]
			{
				255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
				255, 255, 0, 0, 0, 0, 0, 0, 0, 0,
				0, 0, 0, 0
			}, new byte[24]
			{
				137, 0, 0, 16, 42, 49, 64, 169, 64, 1,
				31, 214, 0, 0, 0, 0, 2, 2, 2, 2,
				2, 2, 2, 2
			}));
			list.Add(new BytePattern(new AddressMeaning(AddressKind.Abs64), mustMatchAtStart: true, new byte[24]
			{
				255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
				255, 255, 0, 0, 0, 0, 0, 0, 0, 0,
				0, 0, 0, 0
			}, new byte[24]
			{
				139, 0, 0, 16, 106, 49, 64, 169, 64, 1,
				31, 214, 0, 0, 0, 0, 2, 2, 2, 2,
				2, 2, 2, 2
			}));
			list.Add(new BytePattern(new AddressMeaning(AddressKind.Abs64), mustMatchAtStart: true, new byte[24]
			{
				255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
				255, 255, 0, 0, 0, 0, 0, 0, 0, 0,
				0, 0, 0, 0
			}, new byte[24]
			{
				12, 0, 0, 16, 107, 0, 0, 88, 96, 1,
				31, 214, 0, 0, 0, 0, 2, 2, 2, 2,
				2, 2, 2, 2
			}));
			list.Add(new BytePattern(new AddressMeaning(AddressKind.Abs64), mustMatchAtStart: true, new byte[32]
			{
				255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
				255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
				0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
				0, 0
			}, new byte[32]
			{
				16, 0, 0, 145, 32, 0, 0, 145, 1, 2,
				0, 145, 112, 0, 0, 88, 0, 2, 31, 214,
				0, 0, 0, 0, 2, 2, 2, 2, 2, 2,
				2, 2
			}));
			List<BytePattern> list2 = list;
			object obj = _003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003E_003CPrivateImplementationDetails_003E._85AD7009527B6DE3AC5F5E12927AD6128ABCC8515CCE30BCB0255EBFDEEECE0B_A6;
			if (obj == null)
			{
				obj = new int[5] { 4096, 8192, 16384, 32768, 65536 };
				_003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003E_003CPrivateImplementationDetails_003E._85AD7009527B6DE3AC5F5E12927AD6128ABCC8515CCE30BCB0255EBFDEEECE0B_A6 = (int[])obj;
			}
			ReadOnlySpan<int> readOnlySpan = new ReadOnlySpan<int>((int[]?)obj);
			ReadOnlySpan<byte> readOnlySpan2 = new byte[20]
			{
				11, 0, 2, 88, 96, 1, 31, 214, 12, 0,
				2, 88, 43, 0, 2, 88, 96, 1, 31, 214
			};
			ReadOnlySpan<byte> readOnlySpan3 = new byte[24]
			{
				11, 0, 2, 88, 96, 1, 31, 214, 191, 57,
				3, 213, 12, 0, 2, 88, 43, 0, 2, 88,
				96, 1, 31, 214
			};
			ReadOnlySpan<byte> readOnlySpan4 = new byte[36]
			{
				9, 0, 2, 88, 42, 1, 64, 121, 74, 5,
				0, 113, 42, 1, 0, 121, 96, 0, 0, 84,
				169, 255, 1, 88, 32, 1, 31, 214, 170, 255,
				1, 88, 64, 1, 31, 214
			};
			ReadOnlyMemory<byte> readOnlyMemory = new byte[36]
			{
				255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
				255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
				255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
				255, 255, 255, 255, 255, 255
			};
			ReadOnlySpan<int> readOnlySpan5 = readOnlySpan;
			for (int i = 0; i < readOnlySpan5.Length; i++)
			{
				int num = readOnlySpan5[i];
				byte[] array = readOnlySpan2.ToArray();
				EncodeLdr64LiteralTo(array.AsSpan(0), num, 11);
				EncodeLdr64LiteralTo(array.AsSpan(8), -8 + num + 8, 12);
				EncodeLdr64LiteralTo(array.AsSpan(12), -12 + num + 16, 11);
				list2.Add(new BytePattern(new AddressMeaning(AddressKind.Rel64 | AddressKind.Indirect | AddressKind.ConstantAddr, 0, (uint)num), mustMatchAtStart: true, readOnlyMemory.Slice(0, array.Length), array));
				list2.Add(new BytePattern(new AddressMeaning(AddressKind.PrecodeFixupThunkRel64 | AddressKind.Indirect | AddressKind.ConstantAddr, 0, (uint)(num + 16 - 8)), mustMatchAtStart: true, readOnlyMemory.Slice(0, array.Length - 8), array.AsMemory(8)));
				byte[] array2 = readOnlySpan3.ToArray();
				EncodeLdr64LiteralTo(array2.AsSpan(0), num, 11);
				EncodeLdr64LiteralTo(array2.AsSpan(12), -12 + num + 8, 12);
				EncodeLdr64LiteralTo(array2.AsSpan(16), -16 + num + 16, 11);
				list2.Add(new BytePattern(new AddressMeaning(AddressKind.Rel64 | AddressKind.Indirect | AddressKind.ConstantAddr, 0, (uint)num), mustMatchAtStart: true, readOnlyMemory.Slice(0, array2.Length), array2));
				list2.Add(new BytePattern(new AddressMeaning(AddressKind.PrecodeFixupThunkRel64 | AddressKind.Indirect | AddressKind.ConstantAddr, 0, (uint)(num + 16 - 8)), mustMatchAtStart: true, readOnlyMemory.Slice(0, array2.Length - 8), array2.AsMemory(8)));
				byte[] array3 = readOnlySpan4.ToArray();
				EncodeLdr64LiteralTo(array3.AsSpan(0), num, 9);
				EncodeLdr64LiteralTo(array3.AsSpan(20), -20 + num + 8, 9);
				EncodeLdr64LiteralTo(array3.AsSpan(28), -28 + num + 16, 9);
				list2.Add(new BytePattern(new AddressMeaning(AddressKind.Rel64 | AddressKind.Indirect | AddressKind.ConstantAddr, 0, (uint)(num + 8)), mustMatchAtStart: true, readOnlyMemory.Slice(0, array3.Length), array3));
			}
			return new BytePatternCollection(list2.ToArray());
		}
		return new BytePatternCollection();
	}

	private static void EncodeLdr64LiteralTo(Span<byte> dest, int offset, byte reg)
	{
		uint num = (uint)offset >> 2;
		num &= 0x7FFFF;
		uint num2 = 1476395008u;
		num2 |= num << 5;
		num2 |= reg;
		MemoryMarshal.Write(dest, ref num2);
	}
}
