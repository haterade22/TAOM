using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using MonoMod.Utils;

namespace HarmonyLib;

/// <summary>An abstract wrapper around OpCode and their operands. Used by transpilers</summary>
public class CodeInstruction
{
	internal static class State
	{
		internal static readonly List<Delegate> closureCache = new List<Delegate>();
	}

	/// <summary>The opcode</summary>
	public OpCode opcode;

	/// <summary>The operand</summary>
	public object operand;

	/// <summary>All labels defined on this instruction</summary>
	public List<Label> labels = new List<Label>();

	/// <summary>All exception block boundaries defined on this instruction</summary>
	public List<ExceptionBlock> blocks = new List<ExceptionBlock>();

	internal CodeInstruction()
	{
	}

	internal static CodeInstruction Annotation(string annotation)
	{
		return new CodeInstruction(OpCodes.Nop, annotation);
	}

	internal string IsAnnotation()
	{
		if (!(opcode == OpCodes.Nop))
		{
			return null;
		}
		return operand as string;
	}

	/// <summary>Creates a new CodeInstruction with a given opcode and optional operand</summary>
	/// <param name="opcode">The opcode</param>
	/// <param name="operand">The operand</param>
	public CodeInstruction(OpCode opcode, object operand = null)
	{
		this.opcode = opcode;
		this.operand = operand;
	}

	/// <summary>Create a full copy (including labels and exception blocks) of a CodeInstruction</summary>
	/// <param name="instruction">The <see cref="T:HarmonyLib.CodeInstruction" /> to copy</param>
	public CodeInstruction(CodeInstruction instruction)
	{
		opcode = instruction.opcode;
		operand = instruction.operand;
		labels = instruction.labels.ToList();
		blocks = instruction.blocks.ToList();
	}

	/// <summary>Clones a CodeInstruction and resets its labels and exception blocks</summary>
	/// <returns>A lightweight copy of this code instruction</returns>
	public CodeInstruction Clone()
	{
		return new CodeInstruction(this)
		{
			labels = new List<Label>(),
			blocks = new List<ExceptionBlock>()
		};
	}

	/// <summary>Clones a CodeInstruction, resets labels and exception blocks and sets its opcode</summary>
	/// <param name="opcode">The opcode</param>
	/// <returns>A copy of this CodeInstruction with a new opcode</returns>
	public CodeInstruction Clone(OpCode opcode)
	{
		CodeInstruction codeInstruction = Clone();
		codeInstruction.opcode = opcode;
		return codeInstruction;
	}

	/// <summary>Clones a CodeInstruction, resets labels and exception blocks and sets its operand</summary>
	/// <param name="operand">The operand</param>
	/// <returns>A copy of this CodeInstruction with a new operand</returns>
	public CodeInstruction Clone(object operand)
	{
		CodeInstruction codeInstruction = Clone();
		codeInstruction.operand = operand;
		return codeInstruction;
	}

	/// <summary>Creates a CodeInstruction calling a method (CALL)</summary>
	/// <param name="type">The class/type where the method is declared</param>
	/// <param name="name">The name of the method (case sensitive)</param>
	/// <param name="parameters">Optional parameters to target a specific overload of the method</param>
	/// <param name="generics">Optional list of types that define the generic version of the method</param>
	/// <returns>A code instruction that calls the method matching the arguments</returns>
	public static CodeInstruction Call(Type type, string name, Type[] parameters = null, Type[] generics = null)
	{
		MethodInfo methodInfo = AccessTools.Method(type, name, parameters, generics);
		if ((object)methodInfo == null)
		{
			throw new ArgumentException($"No method found for type={type}, name={name}, parameters={parameters.Description()}, generics={generics.Description()}");
		}
		return new CodeInstruction(OpCodes.Call, methodInfo);
	}

	/// <summary>Creates a CodeInstruction calling a method (CALL)</summary>
	/// <param name="typeColonMethodname">The target method in the form <c>TypeFullName:MethodName</c>, where the type name matches a form recognized by <a href="https://docs.microsoft.com/en-us/dotnet/api/system.type.gettype">Type.GetType</a> like <c>Some.Namespace.Type</c>.</param>
	/// <param name="parameters">Optional parameters to target a specific overload of the method</param>
	/// <param name="generics">Optional list of types that define the generic version of the method</param>
	/// <returns>A code instruction that calls the method matching the arguments</returns>
	public static CodeInstruction Call(string typeColonMethodname, Type[] parameters = null, Type[] generics = null)
	{
		MethodInfo methodInfo = AccessTools.Method(typeColonMethodname, parameters, generics);
		if ((object)methodInfo == null)
		{
			throw new ArgumentException($"No method found for {typeColonMethodname}, parameters={parameters.Description()}, generics={generics.Description()}");
		}
		return new CodeInstruction(OpCodes.Call, methodInfo);
	}

	/// <summary>Creates a CodeInstruction calling a method (CALL)</summary>
	/// <param name="expression">The lambda expression using the method</param>
	/// <returns>A new Codeinstruction</returns>
	public static CodeInstruction Call(Expression<Action> expression)
	{
		return new CodeInstruction(OpCodes.Call, SymbolExtensions.GetMethodInfo(expression));
	}

	/// <summary>Creates a CodeInstruction calling a method (CALL)</summary>
	/// <param name="expression">The lambda expression using the method</param>
	/// <returns>A new Codeinstruction</returns>
	public static CodeInstruction Call<T>(Expression<Action<T>> expression)
	{
		return new CodeInstruction(OpCodes.Call, SymbolExtensions.GetMethodInfo(expression));
	}

	/// <summary>Creates a CodeInstruction calling a method (CALL)</summary>
	/// <param name="expression">The lambda expression using the method</param>
	/// <returns>A new Codeinstruction</returns>
	public static CodeInstruction Call<T, TResult>(Expression<Func<T, TResult>> expression)
	{
		return new CodeInstruction(OpCodes.Call, SymbolExtensions.GetMethodInfo(expression));
	}

	/// <summary>Creates a CodeInstruction calling a method (CALL)</summary>
	/// <param name="expression">The lambda expression using the method</param>
	/// <returns>A new Codeinstruction</returns>
	public static CodeInstruction Call(LambdaExpression expression)
	{
		return new CodeInstruction(OpCodes.Call, SymbolExtensions.GetMethodInfo(expression));
	}

	/// <summary>Returns an instruction to call the specified closure</summary>
	/// <typeparam name="T">The delegate type to emit</typeparam>
	/// <param name="closure">The closure that defines the method to call</param>
	/// <returns>A <see cref="T:HarmonyLib.CodeInstruction" /> that calls the closure as a method</returns>
	public static CodeInstruction CallClosure<T>(T closure) where T : Delegate
	{
		if (closure.Method.IsStatic && closure.Target == null)
		{
			return new CodeInstruction(OpCodes.Call, closure.Method);
		}
		Type[] array = (from x in closure.Method.GetParameters()
			select x.ParameterType).ToArray();
		DynamicMethodDefinition dynamicMethodDefinition = new DynamicMethodDefinition(closure.Method.Name, closure.Method.ReturnType, array);
		ILGenerator iLGenerator = dynamicMethodDefinition.GetILGenerator();
		Type type = closure.Target.GetType();
		if (closure.Target != null && type.GetFields().Any((FieldInfo x) => !x.IsStatic))
		{
			State.closureCache.Add(closure);
			iLGenerator.Emit(OpCodes.Ldsfld, AccessTools.Field(typeof(State), "closureCache"));
			iLGenerator.Emit(OpCodes.Ldc_I4, State.closureCache.Count - 1);
			iLGenerator.Emit(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(List<Delegate>), "Item"));
		}
		else
		{
			if (closure.Target == null)
			{
				iLGenerator.Emit(OpCodes.Ldnull);
			}
			else
			{
				iLGenerator.Emit(OpCodes.Newobj, AccessTools.FirstConstructor(type, (ConstructorInfo x) => !x.IsStatic && x.GetParameters().Length == 0));
			}
			iLGenerator.Emit(OpCodes.Ldftn, closure.Method);
			iLGenerator.Emit(OpCodes.Newobj, AccessTools.Constructor(typeof(T), new Type[2]
			{
				typeof(object),
				typeof(IntPtr)
			}));
		}
		for (int num = 0; num < array.Length; num++)
		{
			iLGenerator.Emit(OpCodes.Ldarg, num);
		}
		iLGenerator.Emit(OpCodes.Callvirt, AccessTools.Method(typeof(T), "Invoke"));
		iLGenerator.Emit(OpCodes.Ret);
		return new CodeInstruction(OpCodes.Call, dynamicMethodDefinition.Generate());
	}

	/// <summary>Creates a CodeInstruction loading a field (LD[S]FLD[A])</summary>
	/// <param name="type">The class/type where the field is defined</param>
	/// <param name="name">The name of the field (case sensitive)</param>
	/// <param name="useAddress">Use address of field</param>
	/// <returns>A new Codeinstruction</returns>
	public static CodeInstruction LoadField(Type type, string name, bool useAddress = false)
	{
		FieldInfo fieldInfo = AccessTools.Field(type, name);
		if ((object)fieldInfo == null)
		{
			throw new ArgumentException($"No field found for {type} and {name}");
		}
		return new CodeInstruction((!useAddress) ? (fieldInfo.IsStatic ? OpCodes.Ldsfld : OpCodes.Ldfld) : (fieldInfo.IsStatic ? OpCodes.Ldsflda : OpCodes.Ldflda), fieldInfo);
	}

	/// <summary>Creates a CodeInstruction storing to a field (ST[S]FLD)</summary>
	/// <param name="type">The class/type where the field is defined</param>
	/// <param name="name">The name of the field (case sensitive)</param>
	/// <returns>A new Codeinstruction</returns>
	public static CodeInstruction StoreField(Type type, string name)
	{
		FieldInfo fieldInfo = AccessTools.Field(type, name);
		if ((object)fieldInfo == null)
		{
			throw new ArgumentException($"No field found for {type} and {name}");
		}
		return new CodeInstruction(fieldInfo.IsStatic ? OpCodes.Stsfld : OpCodes.Stfld, fieldInfo);
	}

	/// <summary>Creates a CodeInstruction loading a local with the given index, using the shorter forms when possible</summary>
	/// <param name="index">The index where the local is stored</param>
	/// <param name="useAddress">Use address of local</param>
	/// <returns>A new Codeinstruction</returns>
	/// <seealso cref="M:HarmonyLib.CodeInstructionExtensions.LocalIndex(HarmonyLib.CodeInstruction)" />
	public static CodeInstruction LoadLocal(int index, bool useAddress = false)
	{
		if (useAddress)
		{
			if (index < 256)
			{
				return new CodeInstruction(OpCodes.Ldloca_S, Convert.ToByte(index));
			}
			return new CodeInstruction(OpCodes.Ldloca, index);
		}
		if (index == 0)
		{
			return new CodeInstruction(OpCodes.Ldloc_0);
		}
		if (index == 1)
		{
			return new CodeInstruction(OpCodes.Ldloc_1);
		}
		if (index == 2)
		{
			return new CodeInstruction(OpCodes.Ldloc_2);
		}
		if (index == 3)
		{
			return new CodeInstruction(OpCodes.Ldloc_3);
		}
		if (index < 256)
		{
			return new CodeInstruction(OpCodes.Ldloc_S, Convert.ToByte(index));
		}
		return new CodeInstruction(OpCodes.Ldloc, index);
	}

	/// <summary>Creates a CodeInstruction storing to a local with the given index, using the shorter forms when possible</summary>
	/// <param name="index">The index where the local is stored</param>
	/// <returns>A new Codeinstruction</returns>
	/// <seealso cref="M:HarmonyLib.CodeInstructionExtensions.LocalIndex(HarmonyLib.CodeInstruction)" />
	public static CodeInstruction StoreLocal(int index)
	{
		if (index == 0)
		{
			return new CodeInstruction(OpCodes.Stloc_0);
		}
		if (index == 1)
		{
			return new CodeInstruction(OpCodes.Stloc_1);
		}
		if (index == 2)
		{
			return new CodeInstruction(OpCodes.Stloc_2);
		}
		if (index == 3)
		{
			return new CodeInstruction(OpCodes.Stloc_3);
		}
		if (index < 256)
		{
			return new CodeInstruction(OpCodes.Stloc_S, Convert.ToByte(index));
		}
		return new CodeInstruction(OpCodes.Stloc, index);
	}

	/// <summary>Creates a CodeInstruction loading an argument with the given index, using the shorter forms when possible</summary>
	/// <param name="index">The index of the argument</param>
	/// <param name="useAddress">Use address of argument</param>
	/// <returns>A new Codeinstruction</returns>
	/// <seealso cref="M:HarmonyLib.CodeInstructionExtensions.ArgumentIndex(HarmonyLib.CodeInstruction)" />
	public static CodeInstruction LoadArgument(int index, bool useAddress = false)
	{
		if (useAddress)
		{
			if (index < 256)
			{
				return new CodeInstruction(OpCodes.Ldarga_S, Convert.ToByte(index));
			}
			return new CodeInstruction(OpCodes.Ldarga, index);
		}
		if (index == 0)
		{
			return new CodeInstruction(OpCodes.Ldarg_0);
		}
		if (index == 1)
		{
			return new CodeInstruction(OpCodes.Ldarg_1);
		}
		if (index == 2)
		{
			return new CodeInstruction(OpCodes.Ldarg_2);
		}
		if (index == 3)
		{
			return new CodeInstruction(OpCodes.Ldarg_3);
		}
		if (index < 256)
		{
			return new CodeInstruction(OpCodes.Ldarg_S, Convert.ToByte(index));
		}
		return new CodeInstruction(OpCodes.Ldarg, index);
	}

	/// <summary>Creates a CodeInstruction storing to an argument with the given index, using the shorter forms when possible</summary>
	/// <param name="index">The index of the argument</param>
	/// <returns>A new Codeinstruction</returns>
	/// <seealso cref="M:HarmonyLib.CodeInstructionExtensions.ArgumentIndex(HarmonyLib.CodeInstruction)" />
	public static CodeInstruction StoreArgument(int index)
	{
		if (index < 256)
		{
			return new CodeInstruction(OpCodes.Starg_S, Convert.ToByte(index));
		}
		return new CodeInstruction(OpCodes.Starg, index);
	}

	/// <summary>Checks if a CodeInstruction contains a given exception block type</summary>
	/// <param name="type">Type of the exception block to check for</param>
	/// <returns>True if the instruction contains the exception block type, false otherwise</returns>
	public bool HasBlock(ExceptionBlockType type)
	{
		return blocks?.Any((ExceptionBlock block) => block.blockType == type) ?? false;
	}

	/// <summary>Returns a string representation of the code instruction</summary>
	/// <returns>A string representation of the code instruction</returns>
	public override string ToString()
	{
		List<string> list = new List<string>();
		foreach (Label label in labels)
		{
			list.Add($"Label{label.GetHashCode()}");
		}
		foreach (ExceptionBlock block in blocks)
		{
			list.Add("EX_" + block.blockType.ToString().Replace("Block", ""));
		}
		string text = ((list.Count > 0) ? (" [" + string.Join(", ", list.ToArray()) + "]") : "");
		string text2 = Emitter.FormatOperand(operand);
		if (text2.Length > 0)
		{
			text2 = " " + text2;
		}
		OpCode opCode = opcode;
		return opCode.ToString() + text2 + text;
	}
}
