using System;
using System.Reflection;
using Mono.Cecil.Cil;
using MonoMod.Utils;

namespace MonoMod.Core.Platforms.Runtimes;

internal class Core100Runtime : Core90Runtime
{
	private static readonly Guid JitVersionGuid = new Guid(2056043606u, 40473, 17185, 128, 185, 160, 210, 197, 120, 201, 69);

	protected override Guid ExpectedJitVersion => JitVersionGuid;

	protected override int VtableIndexICorJitInfoAllocMem => 160;

	protected override int ICorJitInfoFullVtableCount => 176;

	public Core100Runtime(ISystem system, IArchitecture arch)
		: base(system, arch)
	{
	}

	protected unsafe override void MakeAssemblySystemAssembly(Assembly assembly)
	{
		IntPtr intPtr = (IntPtr)Core21Runtime.RuntimeAssemblyPtrField.GetValue(assembly);
		int num = IntPtr.Size + IntPtr.Size + IntPtr.Size;
		IntPtr intPtr2 = *(IntPtr*)((byte*)(void*)intPtr + num);
		int num2 = IntPtr.Size + (FxCoreBaseRuntime.IsDebugClr ? (IntPtr.Size + 4 + 4 + 4 + IntPtr.Size + 4) : 0) + IntPtr.Size + 4 + ((IntPtr.Size == 8) ? 4 : 0) + IntPtr.Size + IntPtr.Size + IntPtr.Size + 4;
		if (FxCoreBaseRuntime.IsDebugClr && IntPtr.Size == 8)
		{
			num2 += 8;
		}
		((sbyte*)(void*)intPtr2)[num2] = 1;
	}

	protected override MethodInfo MakeCreateRuntimeMethodInfoStub(Type methodHandleInternal)
	{
		ConstructorInfo method = methodHandleInternal.GetConstructors((BindingFlags)(-1))[0];
		Type type = typeof(RuntimeMethodHandle).Assembly.GetType("System.RuntimeMethodInfoStub");
		ConstructorInfo constructor = type.GetConstructor(new Type[2]
		{
			methodHandleInternal,
			typeof(object)
		});
		using DynamicMethodDefinition dynamicMethodDefinition = new DynamicMethodDefinition("new RuntimeMethodInfoStub", type, new Type[2]
		{
			typeof(IntPtr),
			typeof(object)
		});
		ILProcessor iLProcessor = dynamicMethodDefinition.GetILProcessor();
		iLProcessor.Emit(OpCodes.Ldarg_0);
		iLProcessor.Emit(OpCodes.Newobj, method);
		iLProcessor.Emit(OpCodes.Ldarg_1);
		iLProcessor.Emit(OpCodes.Newobj, constructor);
		iLProcessor.Emit(OpCodes.Ret);
		return dynamicMethodDefinition.Generate();
	}

	protected override MethodInfo GetOrCreateGetTypeFromHandleUnsafe()
	{
		MethodInfo method = typeof(RuntimeTypeHandle).GetMethod("GetRuntimeTypeFromHandleMaybeNull", (BindingFlags)(-1), null, new Type[1] { typeof(IntPtr) }, null);
		Helpers.Assert((object)method != null, null, "method is not null");
		return method;
	}
}
