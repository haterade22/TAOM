using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using MonoMod.Core.Interop;
using MonoMod.Utils;

namespace MonoMod.Core.Platforms.Runtimes;

internal class Core60Runtime : Core50Runtime
{
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	protected struct ICorJitInfoWrapper
	{
		public IntPtr Vtbl;

		public unsafe IntPtr** Wrapped;

		public const int HotCodeRW = 0;

		public const int ColdCodeRW = 1;

		private const int DataQWords = 4;

		private unsafe fixed ulong data[4];

		public unsafe ref IntPtr this[int index] => ref Unsafe.Add(ref Unsafe.As<ulong, IntPtr>(ref data[0]), index);
	}

	private sealed class JitHookDelegateHolder
	{
		public readonly Core60Runtime Runtime;

		public readonly INativeExceptionHelper? NativeExceptionHelper;

		public readonly GetExceptionSlot? GetNativeExceptionSlot;

		public readonly JitHookHelpersHolder JitHookHelpers;

		public readonly CoreCLR.InvokeCompileMethodPtr InvokeCompileMethodPtr;

		public readonly IntPtr CompileMethodPtr;

		public readonly ThreadLocal<IAllocatedMemory> iCorJitInfoWrapper = new ThreadLocal<IAllocatedMemory>();

		public readonly ReadOnlyMemory<IAllocatedMemory> iCorJitInfoWrapperAllocs;

		public readonly IntPtr iCorJitInfoWrapperVtbl;

		[ThreadStatic]
		private static int hookEntrancy;

		public unsafe JitHookDelegateHolder(Core60Runtime runtime, CoreCLR.InvokeCompileMethodPtr icmp, IntPtr compileMethod)
		{
			Runtime = runtime;
			NativeExceptionHelper = runtime.NativeExceptionHelper;
			JitHookHelpers = runtime.JitHookHelpers;
			InvokeCompileMethodPtr = icmp;
			CompileMethodPtr = compileMethod;
			iCorJitInfoWrapperVtbl = Marshal.AllocHGlobal(IntPtr.Size * runtime.ICorJitInfoFullVtableCount);
			iCorJitInfoWrapperAllocs = Runtime.arch.CreateNativeVtableProxyStubs(iCorJitInfoWrapperVtbl, runtime.ICorJitInfoFullVtableCount);
			Runtime.PatchWrapperVtable((IntPtr*)(void*)iCorJitInfoWrapperVtbl);
			bool isEnabled;
			_003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMMDbgLog.DebugLogTraceStringHandler message = new _003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMMDbgLog.DebugLogTraceStringHandler(42, 1, out isEnabled);
			if (isEnabled)
			{
				message.AppendLiteral("Allocated ICorJitInfo wrapper vtable at 0x");
				message.AppendFormatted(iCorJitInfoWrapperVtbl, "x16");
			}
			_003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMMDbgLog.Trace(ref message);
			CoreCLR.V21.CORINFO_METHOD_INFO cORINFO_METHOD_INFO = default(CoreCLR.V21.CORINFO_METHOD_INFO);
			byte* ptr = default(byte*);
			uint num = default(uint);
			icmp.InvokeCompileMethod(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, &cORINFO_METHOD_INFO, 0u, &ptr, &num);
			MarshalEx.SetLastPInvokeError(MarshalEx.GetLastPInvokeError());
			INativeExceptionHelper nativeExceptionHelper = NativeExceptionHelper;
			if (nativeExceptionHelper != null)
			{
				GetNativeExceptionSlot = nativeExceptionHelper.GetExceptionSlot;
				GetNativeExceptionSlot();
			}
			_ = hookEntrancy;
			hookEntrancy = 0;
		}

		public unsafe CoreCLR.CorJitResult CompileMethodHook(IntPtr jit, IntPtr corJitInfo, CoreCLR.V21.CORINFO_METHOD_INFO* methodInfo, uint flags, byte** nativeEntry, uint* nativeSizeOfCode)
		{
			if (jit == IntPtr.Zero)
			{
				return CoreCLR.CorJitResult.CORJIT_OK;
			}
			*nativeEntry = null;
			*nativeSizeOfCode = 0u;
			int lastPInvokeError = MarshalEx.GetLastPInvokeError();
			nint num = 0;
			GetExceptionSlot getNativeExceptionSlot = GetNativeExceptionSlot;
			IntPtr* ptr = ((getNativeExceptionSlot != null) ? getNativeExceptionSlot() : null);
			hookEntrancy++;
			try
			{
				bool isEnabled;
				if (hookEntrancy == 1)
				{
					try
					{
						IAllocatedMemory allocatedMemory = iCorJitInfoWrapper.Value;
						if (allocatedMemory == null)
						{
							AllocationRequest allocationRequest = new AllocationRequest(sizeof(ICorJitInfoWrapper));
							allocationRequest.Alignment = IntPtr.Size;
							allocationRequest.Executable = false;
							AllocationRequest request = allocationRequest;
							if (Runtime.System.MemoryAllocator.TryAllocate(request, out IAllocatedMemory allocated))
							{
								allocatedMemory = (iCorJitInfoWrapper.Value = allocated);
							}
						}
						if (allocatedMemory != null)
						{
							ICorJitInfoWrapper* ptr2 = (ICorJitInfoWrapper*)(void*)allocatedMemory.BaseAddress;
							ptr2->Vtbl = iCorJitInfoWrapperVtbl;
							ptr2->Wrapped = (IntPtr**)(void*)corJitInfo;
							(*ptr2)[0] = IntPtr.Zero;
							(*ptr2)[1] = IntPtr.Zero;
							corJitInfo = (IntPtr)ptr2;
						}
					}
					catch (Exception value)
					{
						try
						{
							_003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMMDbgLog.DebugLogErrorStringHandler message = new _003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMMDbgLog.DebugLogErrorStringHandler(48, 1, out isEnabled);
							if (isEnabled)
							{
								message.AppendLiteral("Error while setting up the ICorJitInfo wrapper: ");
								message.AppendFormatted(value);
							}
							_003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMMDbgLog.Error(ref message);
						}
						catch
						{
						}
					}
				}
				CoreCLR.CorJitResult result = InvokeCompileMethodPtr.InvokeCompileMethod(CompileMethodPtr, jit, corJitInfo, methodInfo, flags, nativeEntry, nativeSizeOfCode);
				if (ptr != null && (num = *ptr) != 0)
				{
					_003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMMDbgLog.DebugLogWarningStringHandler message2 = new _003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMMDbgLog.DebugLogWarningStringHandler(59, 1, out isEnabled);
					if (isEnabled)
					{
						message2.AppendLiteral("Native exception caught in JIT by exception helper (ex: 0x");
						message2.AppendFormatted(num, "x16");
						message2.AppendLiteral(")");
					}
					_003C24b3ba8a_002D00b7_002D40fc_002Da603_002D2711fa115297_003EMMDbgLog.Warning(ref message2);
					return result;
				}
				if (hookEntrancy == 1)
				{
					try
					{
						IAllocatedMemory value2 = iCorJitInfoWrapper.Value;
						if (value2 == null)
						{
							return result;
						}
						IntPtr rwEntry = (*(ICorJitInfoWrapper*)(void*)value2.BaseAddress)[0];
						Runtime.CompileMethodHookPostCommon(methodInfo, nativeEntry, nativeSizeOfCode, rwEntry);
					}
					catch
					{
					}
				}
				return result;
			}
			finally
			{
				hookEntrancy--;
				if (ptr != null)
				{
					*ptr = num;
				}
				MarshalEx.SetLastPInvokeError(lastPInvokeError);
			}
		}
	}

	protected struct NativeJitHookConfig
	{
		public IntPtr compileMethod;

		public IntPtr compileMethodHook;

		public IntPtr compileMethodHookPost;

		public IntPtr allocMem;

		public IntPtr allocMemHook;
	}

	private sealed class JitHookPostDelegateHolder
	{
		public readonly Core60Runtime Runtime;

		public readonly JitHookHelpersHolder JitHookHelpers;

		public static volatile bool patchedICorJitInfo;

		public static readonly object patchedICorJitInfoSyncRoot = new object();

		public JitHookPostDelegateHolder(Core60Runtime runtime)
		{
			Runtime = runtime;
			JitHookHelpers = runtime.JitHookHelpers;
		}

		public unsafe CoreCLR.CorJitResult CompileMethodHookPost(IntPtr jit, IntPtr corJitInfo, CoreCLR.V21.CORINFO_METHOD_INFO* methodInfo, uint flags, byte** nativeEntry, uint* nativeSizeOfCode, CoreCLR.CorJitResult res, CoreCLR.V60.AllocMemArgs* pArgs)
		{
			if (jit == IntPtr.Zero)
			{
				return res;
			}
			try
			{
				if (!patchedICorJitInfo)
				{
					lock (patchedICorJitInfoSyncRoot)
					{
						if (!patchedICorJitInfo)
						{
							IntPtr* vTableEntry = Core21Runtime.GetVTableEntry(corJitInfo, Runtime.VtableIndexICorJitInfoAllocMem);
							NativeJitHookConfig* nativeJitHookConfig = Runtime.GetNativeJitHookConfig();
							nativeJitHookConfig->allocMem = *vTableEntry;
							IntPtr value = nativeJitHookConfig->allocMemHook;
							Span<byte> span = stackalloc byte[sizeof(IntPtr)];
							MemoryMarshal.Write(span, ref value);
							Runtime.System.PatchData(PatchTargetKind.ReadOnly, (IntPtr)vTableEntry, span, default(Span<byte>));
							patchedICorJitInfo = true;
						}
					}
				}
				Runtime.CompileMethodHookPostCommon(methodInfo, nativeEntry, nativeSizeOfCode, pArgs->hotCodeBlockRW);
			}
			catch
			{
			}
			return res;
		}
	}

	private sealed class AllocMemDelegateHolder
	{
		public readonly Core60Runtime Runtime;

		public readonly INativeExceptionHelper? NativeExceptionHelper;

		public readonly GetExceptionSlot? GetNativeExceptionSlot;

		public readonly CoreCLR.InvokeAllocMemPtr InvokeAllocMemPtr;

		public readonly int ICorJitInfoAllocMemIdx;

		public readonly ConcurrentDictionary<IntPtr, (IntPtr M2N, IDisposable?)> AllocMemExceptionHelperCache = new ConcurrentDictionary<IntPtr, (IntPtr, IDisposable)>();

		public unsafe AllocMemDelegateHolder(Core60Runtime runtime, CoreCLR.InvokeAllocMemPtr iamp)
		{
			Runtime = runtime;
			NativeExceptionHelper = runtime.NativeExceptionHelper;
			GetNativeExceptionSlot = NativeExceptionHelper?.GetExceptionSlot;
			InvokeAllocMemPtr = iamp;
			ICorJitInfoAllocMemIdx = Runtime.VtableIndexICorJitInfoAllocMem;
			iamp.InvokeAllocMem(IntPtr.Zero, IntPtr.Zero, null);
		}

		private IntPtr GetRealInvokePtr(IntPtr ptr)
		{
			if (NativeExceptionHelper == null)
			{
				return ptr;
			}
			IDisposable handle;
			return AllocMemExceptionHelperCache.GetOrAdd(ptr, (IntPtr p) => (M2N: Runtime.EHManagedToNative(p, out handle), handle)).M2N;
		}

		public unsafe void AllocMemHook(IntPtr thisPtr, CoreCLR.V60.AllocMemArgs* args)
		{
			if (!(thisPtr == IntPtr.Zero))
			{
				ICorJitInfoWrapper* ptr = (ICorJitInfoWrapper*)(void*)thisPtr;
				IntPtr** wrapped = ptr->Wrapped;
				InvokeAllocMemPtr.InvokeAllocMem(GetRealInvokePtr((*wrapped)[ICorJitInfoAllocMemIdx]), (IntPtr)wrapped, args);
				GetExceptionSlot getNativeExceptionSlot = GetNativeExceptionSlot;
				if (getNativeExceptionSlot == null || *getNativeExceptionSlot() == (IntPtr)0)
				{
					(*ptr)[0] = args->hotCodeBlockRW;
					(*ptr)[1] = args->coldCodeBlockRW;
				}
			}
		}
	}

	private readonly IArchitecture arch;

	private static readonly Guid JitVersionGuid = new Guid(1590910040u, 34171, 18653, 168, 24, 124, 1, 54, 220, 159, 115);

	private Delegate? ourCompileMethodHookPost;

	private Delegate? allocMemDelegate;

	private IDisposable? n2mAllocMemHelper;

	protected override Guid ExpectedJitVersion => JitVersionGuid;

	protected virtual int VtableIndexICorJitInfoAllocMem => 156;

	protected virtual int ICorJitInfoFullVtableCount => 173;

	protected virtual CoreCLR.InvokeAllocMemPtr InvokeAllocMemPtr => CoreCLR.V60.InvokeAllocMemPtr;

	protected override CoreCLR.InvokeCompileMethodPtr InvokeCompileMethodPtr => CoreCLR.V60.InvokeCompileMethodPtr;

	public Core60Runtime(ISystem system, IArchitecture arch)
		: base(system)
	{
		this.arch = arch;
	}

	protected override void InstallJitHook(IntPtr jit)
	{
		if ((base.System.Features & SystemFeature.MayUseNativeJitHooks) == 0 || !InstallNativeJitHook(jit))
		{
			InstallManagedJitHook(jit);
		}
	}

	protected unsafe virtual bool InstallNativeJitHook(IntPtr jit)
	{
		NativeJitHookConfig* nativeJitHookConfig = GetNativeJitHookConfig();
		if (nativeJitHookConfig == null)
		{
			return false;
		}
		CheckVersionGuid(jit);
		IntPtr* vTableEntry = Core21Runtime.GetVTableEntry(jit, VtableIndexICorJitCompilerCompileMethod);
		IntPtr functionPointerForDelegate = Marshal.GetFunctionPointerForDelegate(ourCompileMethodHookPost = CastCompileMethodHookPostToRealType(CreateCompileMethodHookPostDelegate()));
		CoreCLR.V21.CORINFO_METHOD_INFO cORINFO_METHOD_INFO = default(CoreCLR.V21.CORINFO_METHOD_INFO);
		byte* ptr = default(byte*);
		uint num = default(uint);
		CoreCLR.V60.AllocMemArgs allocMemArgs = default(CoreCLR.V60.AllocMemArgs);
		CoreCLR.V60.InvokeCompileMethodHookPostPtr.InvokeCompileMethodHookPost(functionPointerForDelegate, IntPtr.Zero, IntPtr.Zero, &cORINFO_METHOD_INFO, 0u, &ptr, &num, CoreCLR.CorJitResult.CORJIT_OK, &allocMemArgs);
		nativeJitHookConfig->compileMethod = *vTableEntry;
		nativeJitHookConfig->compileMethodHookPost = functionPointerForDelegate;
		IntPtr value = nativeJitHookConfig->compileMethodHook;
		Span<byte> span = stackalloc byte[sizeof(IntPtr)];
		MemoryMarshal.Write(span, ref value);
		base.System.PatchData(PatchTargetKind.ReadOnly, (IntPtr)vTableEntry, span, default(Span<byte>));
		CompileMethodPatchPrimer();
		return true;
	}

	[MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
	private static void CompileMethodPatchPrimer()
	{
	}

	protected unsafe override Delegate CreateCompileMethodDelegate(IntPtr compileMethod)
	{
		return new _003C_003Ef__AnonymousDelegate0(new JitHookDelegateHolder(this, InvokeCompileMethodPtr, compileMethod).CompileMethodHook);
	}

	private unsafe void CompileMethodHookPostCommon(CoreCLR.V21.CORINFO_METHOD_INFO* methodInfo, byte** nativeEntry, uint* nativeSizeOfCode, IntPtr rwEntry)
	{
		RuntimeTypeHandle[] array = null;
		RuntimeTypeHandle[] array2 = null;
		if (methodInfo->args.sigInst.classInst != null)
		{
			array = new RuntimeTypeHandle[methodInfo->args.sigInst.classInstCount];
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = base.JitHookHelpers.GetTypeFromNativeHandle(methodInfo->args.sigInst.classInst[i]).TypeHandle;
			}
		}
		if (methodInfo->args.sigInst.methInst != null)
		{
			array2 = new RuntimeTypeHandle[methodInfo->args.sigInst.methInstCount];
			for (int j = 0; j < array2.Length; j++)
			{
				array2[j] = base.JitHookHelpers.GetTypeFromNativeHandle(methodInfo->args.sigInst.methInst[j]).TypeHandle;
			}
		}
		RuntimeTypeHandle typeHandle = base.JitHookHelpers.GetDeclaringTypeOfMethodHandle(methodInfo->ftn).TypeHandle;
		RuntimeMethodHandle methodHandle = base.JitHookHelpers.CreateHandleForHandlePointer(methodInfo->ftn);
		OnMethodCompiledCore(typeHandle, methodHandle, array, array2, (IntPtr)(*nativeEntry), rwEntry, *nativeSizeOfCode);
	}

	protected unsafe virtual NativeJitHookConfig* GetNativeJitHookConfig()
	{
		return (NativeJitHookConfig*)(void*)base.System.GetNativeJitHookConfig(60);
	}

	protected unsafe virtual Delegate CreateCompileMethodHookPostDelegate()
	{
		return new _003C_003Ef__AnonymousDelegate1(new JitHookPostDelegateHolder(this).CompileMethodHookPost);
	}

	protected virtual Delegate CastCompileMethodHookPostToRealType(Delegate del)
	{
		return del.CastDelegate<CoreCLR.V60.CompileMethodHookPostDelegate>();
	}

	protected unsafe virtual void PatchWrapperVtable(IntPtr* vtbl)
	{
		allocMemDelegate = CastAllocMemToRealType(CreateAllocMemDelegate());
		IntPtr intPtr = EHNativeToManaged(Marshal.GetFunctionPointerForDelegate(allocMemDelegate), out n2mAllocMemHelper);
		InvokeAllocMemPtr.InvokeAllocMem(intPtr, IntPtr.Zero, null);
		vtbl[VtableIndexICorJitInfoAllocMem] = intPtr;
	}

	protected override Delegate CastCompileHookToRealType(Delegate del)
	{
		return del.CastDelegate<CoreCLR.V60.CompileMethodDelegate>();
	}

	protected virtual Delegate CastAllocMemToRealType(Delegate del)
	{
		return del.CastDelegate<CoreCLR.V60.AllocMemDelegate>();
	}

	protected unsafe virtual Delegate CreateAllocMemDelegate()
	{
		return new _003C_003Ef__AnonymousDelegate2(new AllocMemDelegateHolder(this, InvokeAllocMemPtr).AllocMemHook);
	}
}
