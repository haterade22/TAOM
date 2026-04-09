using System;
using System.Reflection;
using System.Reflection.Emit;
using MonoMod.Utils;

namespace HarmonyLib;

/// <summary>A helper class to invoke method with delegates</summary>
public static class MethodInvoker
{
	/// <summary>Creates a fast invocation handler from a method</summary>
	/// <param name="methodInfo">The method to invoke</param>
	/// <param name="directBoxValueAccess">Controls if boxed value object is accessed/updated directly</param>
	/// <returns>The <see cref="T:HarmonyLib.FastInvokeHandler" /></returns>
	/// <remarks>
	///     <para>
	/// The <c>directBoxValueAccess</c> option controls how value types passed by reference (e.g. ref int, out my_struct) are handled in the arguments array
	/// passed to the fast invocation handler.
	/// Since the arguments array is an object array, any value types contained within it are actually references to a boxed value object.
	/// Like any other object, there can be other references to such boxed value objects, other than the reference within the arguments array.
	/// <example>For example,
	/// <code>
	/// var val = 5;
	/// var box = (object)val;
	/// var arr = new object[] { box };
	/// handler(arr); // for a method with parameter signature: ref/out/in int
	/// </code></example></para>
	///     <para>
	/// If <c>directBoxValueAccess</c> is <c>true</c>, the boxed value object is accessed (and potentially updated) directly when the handler is called,
	/// such that all references to the boxed object reflect the potentially updated value.
	/// In the above example, if the method associated with the handler updates the passed (boxed) value to 10, both <c>box</c> and <c>arr[0]</c>
	/// now reflect the value 10. Note that the original <c>val</c> is not updated, since boxing always copies the value into the new boxed value object.
	/// </para>
	///     <para>
	/// If <c>directBoxValueAccess</c> is <c>false</c> (default), the boxed value object in the arguments array is replaced with a "reboxed" value object,
	/// such that potential updates to the value are reflected only in the arguments array.
	/// In the above example, if the method associated with the handler updates the passed (boxed) value to 10, only <c>arr[0]</c> now reflects the value 10.
	/// </para>
	/// </remarks>
	public static FastInvokeHandler GetHandler(MethodInfo methodInfo, bool directBoxValueAccess = false)
	{
		DynamicMethodDefinition dynamicMethodDefinition = new DynamicMethodDefinition("FastInvoke_" + methodInfo.Name + "_" + (directBoxValueAccess ? "direct" : "indirect"), typeof(object), new Type[2]
		{
			typeof(object),
			typeof(object[])
		});
		ILGenerator iLGenerator = dynamicMethodDefinition.GetILGenerator();
		if (!methodInfo.IsStatic)
		{
			Emit(iLGenerator, OpCodes.Ldarg_0);
			EmitUnboxIfNeeded(iLGenerator, methodInfo.DeclaringType);
		}
		bool flag = true;
		ParameterInfo[] parameters = methodInfo.GetParameters();
		for (int i = 0; i < parameters.Length; i++)
		{
			Type type = parameters[i].ParameterType;
			bool isByRef = type.IsByRef;
			if (isByRef)
			{
				type = type.GetElementType();
			}
			bool isValueType = type.IsValueType;
			if (isByRef && isValueType && !directBoxValueAccess)
			{
				Emit(iLGenerator, OpCodes.Ldarg_1);
				EmitFastInt(iLGenerator, i);
			}
			Emit(iLGenerator, OpCodes.Ldarg_1);
			EmitFastInt(iLGenerator, i);
			if (isByRef && !isValueType)
			{
				Emit(iLGenerator, OpCodes.Ldelema, typeof(object));
				continue;
			}
			Emit(iLGenerator, OpCodes.Ldelem_Ref);
			if (!isValueType)
			{
				continue;
			}
			if (!isByRef || !directBoxValueAccess)
			{
				Emit(iLGenerator, OpCodes.Unbox_Any, type);
				if (isByRef)
				{
					Emit(iLGenerator, OpCodes.Box, type);
					Emit(iLGenerator, OpCodes.Dup);
					if (flag)
					{
						flag = false;
						iLGenerator.DeclareLocal(typeof(object), pinned: false);
					}
					Emit(iLGenerator, OpCodes.Stloc_0);
					Emit(iLGenerator, OpCodes.Stelem_Ref);
					Emit(iLGenerator, OpCodes.Ldloc_0);
					Emit(iLGenerator, OpCodes.Unbox, type);
				}
			}
			else
			{
				Emit(iLGenerator, OpCodes.Unbox, type);
			}
		}
		if (methodInfo.IsStatic)
		{
			EmitCall(iLGenerator, OpCodes.Call, methodInfo);
		}
		else
		{
			EmitCall(iLGenerator, OpCodes.Callvirt, methodInfo);
		}
		if (methodInfo.ReturnType == typeof(void))
		{
			Emit(iLGenerator, OpCodes.Ldnull);
		}
		else
		{
			EmitBoxIfNeeded(iLGenerator, methodInfo.ReturnType);
		}
		Emit(iLGenerator, OpCodes.Ret);
		return dynamicMethodDefinition.Generate().CreateDelegate<FastInvokeHandler>();
	}

	internal static void Emit(ILGenerator il, OpCode opcode)
	{
		il.Emit(opcode);
	}

	internal static void Emit(ILGenerator il, OpCode opcode, Type type)
	{
		il.Emit(opcode, type);
	}

	internal static void EmitCall(ILGenerator il, OpCode opcode, MethodInfo methodInfo)
	{
		il.EmitCall(opcode, methodInfo, null);
	}

	private static void EmitUnboxIfNeeded(ILGenerator il, Type type)
	{
		if (type.IsValueType)
		{
			Emit(il, OpCodes.Unbox_Any, type);
		}
	}

	private static void EmitBoxIfNeeded(ILGenerator il, Type type)
	{
		if (type.IsValueType)
		{
			Emit(il, OpCodes.Box, type);
		}
	}

	internal static void EmitFastInt(ILGenerator il, int value)
	{
		switch (value)
		{
		case -1:
			il.Emit(OpCodes.Ldc_I4_M1);
			return;
		case 0:
			il.Emit(OpCodes.Ldc_I4_0);
			return;
		case 1:
			il.Emit(OpCodes.Ldc_I4_1);
			return;
		case 2:
			il.Emit(OpCodes.Ldc_I4_2);
			return;
		case 3:
			il.Emit(OpCodes.Ldc_I4_3);
			return;
		case 4:
			il.Emit(OpCodes.Ldc_I4_4);
			return;
		case 5:
			il.Emit(OpCodes.Ldc_I4_5);
			return;
		case 6:
			il.Emit(OpCodes.Ldc_I4_6);
			return;
		case 7:
			il.Emit(OpCodes.Ldc_I4_7);
			return;
		case 8:
			il.Emit(OpCodes.Ldc_I4_8);
			return;
		}
		if (value > -129 && value < 128)
		{
			il.Emit(OpCodes.Ldc_I4_S, (sbyte)value);
		}
		else
		{
			il.Emit(OpCodes.Ldc_I4, value);
		}
	}
}
