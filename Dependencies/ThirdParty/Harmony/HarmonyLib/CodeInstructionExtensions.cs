using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Reflection.Emit;

namespace HarmonyLib;

/// <summary>Extensions for <see cref="T:HarmonyLib.CodeInstruction" /></summary>
public static class CodeInstructionExtensions
{
	internal static readonly HashSet<OpCode> opcodesCalling = new HashSet<OpCode>
	{
		OpCodes.Call,
		OpCodes.Callvirt
	};

	internal static readonly HashSet<OpCode> opcodesLoadingLocalByAddress = new HashSet<OpCode>
	{
		OpCodes.Ldloca_S,
		OpCodes.Ldloca
	};

	internal static readonly HashSet<OpCode> opcodesLoadingLocalNormal = new HashSet<OpCode>
	{
		OpCodes.Ldloc_0,
		OpCodes.Ldloc_1,
		OpCodes.Ldloc_2,
		OpCodes.Ldloc_3,
		OpCodes.Ldloc_S,
		OpCodes.Ldloc
	};

	internal static readonly HashSet<OpCode> opcodesStoringLocal = new HashSet<OpCode>
	{
		OpCodes.Stloc_0,
		OpCodes.Stloc_1,
		OpCodes.Stloc_2,
		OpCodes.Stloc_3,
		OpCodes.Stloc_S,
		OpCodes.Stloc
	};

	internal static readonly HashSet<OpCode> opcodesLoadingArgumentByAddress = new HashSet<OpCode>
	{
		OpCodes.Ldarga_S,
		OpCodes.Ldarga
	};

	internal static readonly HashSet<OpCode> opcodesLoadingArgumentNormal = new HashSet<OpCode>
	{
		OpCodes.Ldarg_0,
		OpCodes.Ldarg_1,
		OpCodes.Ldarg_2,
		OpCodes.Ldarg_3,
		OpCodes.Ldarg_S,
		OpCodes.Ldarg
	};

	internal static readonly HashSet<OpCode> opcodesStoringArgument = new HashSet<OpCode>
	{
		OpCodes.Starg_S,
		OpCodes.Starg
	};

	internal static readonly HashSet<OpCode> opcodesBranching = new HashSet<OpCode>
	{
		OpCodes.Br_S,
		OpCodes.Brfalse_S,
		OpCodes.Brtrue_S,
		OpCodes.Beq_S,
		OpCodes.Bge_S,
		OpCodes.Bgt_S,
		OpCodes.Ble_S,
		OpCodes.Blt_S,
		OpCodes.Bne_Un_S,
		OpCodes.Bge_Un_S,
		OpCodes.Bgt_Un_S,
		OpCodes.Ble_Un_S,
		OpCodes.Blt_Un_S,
		OpCodes.Br,
		OpCodes.Brfalse,
		OpCodes.Brtrue,
		OpCodes.Beq,
		OpCodes.Bge,
		OpCodes.Bgt,
		OpCodes.Ble,
		OpCodes.Blt,
		OpCodes.Bne_Un,
		OpCodes.Bge_Un,
		OpCodes.Bgt_Un,
		OpCodes.Ble_Un,
		OpCodes.Blt_Un
	};

	private static readonly HashSet<OpCode> constantLoadingCodes = new HashSet<OpCode>
	{
		OpCodes.Ldc_I4_M1,
		OpCodes.Ldc_I4_0,
		OpCodes.Ldc_I4_1,
		OpCodes.Ldc_I4_2,
		OpCodes.Ldc_I4_3,
		OpCodes.Ldc_I4_4,
		OpCodes.Ldc_I4_5,
		OpCodes.Ldc_I4_6,
		OpCodes.Ldc_I4_7,
		OpCodes.Ldc_I4_8,
		OpCodes.Ldc_I4,
		OpCodes.Ldc_I4_S,
		OpCodes.Ldc_I8,
		OpCodes.Ldc_R4,
		OpCodes.Ldc_R8,
		OpCodes.Ldstr
	};

	internal static int GetSize(this CodeInstruction instruction)
	{
		int num = instruction.opcode.Size;
		switch (instruction.opcode.OperandType)
		{
		case OperandType.InlineSwitch:
			num += (1 + ((Array)instruction.operand).Length) * 4;
			break;
		case OperandType.InlineI8:
		case OperandType.InlineR:
			num += 8;
			break;
		case OperandType.InlineBrTarget:
		case OperandType.InlineField:
		case OperandType.InlineI:
		case OperandType.InlineMethod:
		case OperandType.InlineSig:
		case OperandType.InlineString:
		case OperandType.InlineTok:
		case OperandType.InlineType:
		case OperandType.ShortInlineR:
			num += 4;
			break;
		case OperandType.InlineVar:
			num += 2;
			break;
		case OperandType.ShortInlineBrTarget:
		case OperandType.ShortInlineI:
		case OperandType.ShortInlineVar:
			num++;
			break;
		}
		return num;
	}

	/// <summary>Returns if an <see cref="T:System.Reflection.Emit.OpCode" /> is initialized and valid</summary>
	/// <param name="code">The <see cref="T:System.Reflection.Emit.OpCode" /></param>
	/// <returns />
	public static bool IsValid(this OpCode code)
	{
		return code.Size > 0;
	}

	/// <summary>Shortcut for testing whether the operand is equal to a non-null value</summary>
	/// <param name="code">The <see cref="T:HarmonyLib.CodeInstruction" /></param>
	/// <param name="value">The value</param>
	/// <returns>True if the operand has the same type and is equal to the value</returns>
	public static bool OperandIs(this CodeInstruction code, object value)
	{
		if (value == null)
		{
			throw new ArgumentNullException("value");
		}
		if (code.operand == null)
		{
			return false;
		}
		Type type = value.GetType();
		Type type2 = code.operand.GetType();
		if (AccessTools.IsInteger(type) && AccessTools.IsNumber(type2))
		{
			return Convert.ToInt64(code.operand) == Convert.ToInt64(value);
		}
		if (AccessTools.IsFloatingPoint(type) && AccessTools.IsNumber(type2))
		{
			return Convert.ToDouble(code.operand) == Convert.ToDouble(value);
		}
		return object.Equals(code.operand, value);
	}

	/// <summary>Shortcut for testing whether the operand is equal to a non-null value</summary>
	/// <param name="code">The <see cref="T:HarmonyLib.CodeInstruction" /></param>
	/// <param name="value">The <see cref="T:System.Reflection.MemberInfo" /> value</param>
	/// <returns>True if the operand is equal to the value</returns>
	/// <remarks>This is an optimized version of <see cref="M:HarmonyLib.CodeInstructionExtensions.OperandIs(HarmonyLib.CodeInstruction,System.Object)" /> for <see cref="T:System.Reflection.MemberInfo" /></remarks>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static bool OperandIs(this CodeInstruction code, MemberInfo value)
	{
		if ((object)value == null)
		{
			throw new ArgumentNullException("value");
		}
		return object.Equals(code.operand, value);
	}

	/// <summary>Shortcut for <code>code.opcode == opcode &amp;&amp; code.OperandIs(operand)</code></summary>
	/// <param name="code">The <see cref="T:HarmonyLib.CodeInstruction" /></param>
	/// <param name="opcode">The <see cref="T:System.Reflection.Emit.OpCode" /></param>
	/// <param name="operand">The operand value</param>
	/// <returns>True if the opcode is equal to the given opcode and the operand has the same type and is equal to the given operand</returns>
	public static bool Is(this CodeInstruction code, OpCode opcode, object operand)
	{
		if (code.opcode == opcode)
		{
			return code.OperandIs(operand);
		}
		return false;
	}

	/// <summary>Shortcut for <code>code.opcode == opcode &amp;&amp; code.OperandIs(operand)</code></summary>
	/// <param name="code">The <see cref="T:HarmonyLib.CodeInstruction" /></param>
	/// <param name="opcode">The <see cref="T:System.Reflection.Emit.OpCode" /></param>
	/// <param name="operand">The <see cref="T:System.Reflection.MemberInfo" /> operand value</param>
	/// <returns>True if the opcode is equal to the given opcode and the operand is equal to the given operand</returns>
	/// <remarks>This is an optimized version of <see cref="M:HarmonyLib.CodeInstructionExtensions.Is(HarmonyLib.CodeInstruction,System.Reflection.Emit.OpCode,System.Object)" /> for <see cref="T:System.Reflection.MemberInfo" /></remarks>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static bool Is(this CodeInstruction code, OpCode opcode, MemberInfo operand)
	{
		if (code.opcode == opcode)
		{
			return code.OperandIs(operand);
		}
		return false;
	}

	/// <summary>Tests for any form of Ldarg*</summary>
	/// <param name="code">The <see cref="T:HarmonyLib.CodeInstruction" /></param>
	/// <param name="n">The (optional) index</param>
	/// <returns>True if it matches one of the variations</returns>
	public static bool IsLdarg(this CodeInstruction code, int? n = null)
	{
		if ((!n.HasValue || n.Value == 0) && code.opcode == OpCodes.Ldarg_0)
		{
			return true;
		}
		if ((!n.HasValue || n.Value == 1) && code.opcode == OpCodes.Ldarg_1)
		{
			return true;
		}
		if ((!n.HasValue || n.Value == 2) && code.opcode == OpCodes.Ldarg_2)
		{
			return true;
		}
		if ((!n.HasValue || n.Value == 3) && code.opcode == OpCodes.Ldarg_3)
		{
			return true;
		}
		if (code.opcode == OpCodes.Ldarg && (!n.HasValue || n.Value == Convert.ToInt32(code.operand)))
		{
			return true;
		}
		if (code.opcode == OpCodes.Ldarg_S && (!n.HasValue || n.Value == Convert.ToInt32(code.operand)))
		{
			return true;
		}
		return false;
	}

	/// <summary>Tests for Ldarga/Ldarga_S</summary>
	/// <param name="code">The <see cref="T:HarmonyLib.CodeInstruction" /></param>
	/// <param name="n">The (optional) index</param>
	/// <returns>True if it matches one of the variations</returns>
	public static bool IsLdarga(this CodeInstruction code, int? n = null)
	{
		if (code.opcode != OpCodes.Ldarga && code.opcode != OpCodes.Ldarga_S)
		{
			return false;
		}
		if (n.HasValue)
		{
			return n.Value == Convert.ToInt32(code.operand);
		}
		return true;
	}

	/// <summary>Tests for Starg/Starg_S</summary>
	/// <param name="code">The <see cref="T:HarmonyLib.CodeInstruction" /></param>
	/// <param name="n">The (optional) index</param>
	/// <returns>True if it matches one of the variations</returns>
	public static bool IsStarg(this CodeInstruction code, int? n = null)
	{
		if (code.opcode != OpCodes.Starg && code.opcode != OpCodes.Starg_S)
		{
			return false;
		}
		if (n.HasValue)
		{
			return n.Value == Convert.ToInt32(code.operand);
		}
		return true;
	}

	/// <summary>Tests for any form of Ldloc*</summary>
	/// <param name="code">The <see cref="T:HarmonyLib.CodeInstruction" /></param>
	/// <param name="variable">The optional local variable</param>
	/// <returns>True if it matches one of the variations</returns>
	public static bool IsLdloc(this CodeInstruction code, LocalBuilder variable = null)
	{
		if (!opcodesLoadingLocalNormal.Contains(code.opcode) && !opcodesLoadingLocalByAddress.Contains(code.opcode))
		{
			return false;
		}
		if (variable != null)
		{
			return object.Equals(variable, code.operand);
		}
		return true;
	}

	/// <summary>Tests for any form of Stloc*</summary>
	/// <param name="code">The <see cref="T:HarmonyLib.CodeInstruction" /></param>
	/// <param name="variable">The optional local variable</param>
	/// <returns>True if it matches one of the variations</returns>
	public static bool IsStloc(this CodeInstruction code, LocalBuilder variable = null)
	{
		if (!opcodesStoringLocal.Contains(code.opcode))
		{
			return false;
		}
		if (variable != null)
		{
			return object.Equals(variable, code.operand);
		}
		return true;
	}

	/// <summary>Tests if the code instruction branches</summary>
	/// <param name="code">The <see cref="T:HarmonyLib.CodeInstruction" /></param>
	/// <param name="label">The label if the instruction is a branch operation or <see langword="null" /> if not</param>
	/// <returns>True if the instruction branches</returns>
	public static bool Branches(this CodeInstruction code, out Label? label)
	{
		if (opcodesBranching.Contains(code.opcode))
		{
			label = (Label)code.operand;
			return true;
		}
		label = null;
		return false;
	}

	/// <summary>Tests if the code instruction calls the method/constructor</summary>
	/// <param name="code">The <see cref="T:HarmonyLib.CodeInstruction" /></param>
	/// <param name="method">The method</param>
	/// <returns>True if the instruction calls the method or constructor</returns>
	public static bool Calls(this CodeInstruction code, MethodInfo method)
	{
		if ((object)method == null)
		{
			throw new ArgumentNullException("method");
		}
		if (code.opcode != OpCodes.Call && code.opcode != OpCodes.Callvirt)
		{
			return false;
		}
		return object.Equals(code.operand, method);
	}

	/// <summary>Tests if the code instruction loads a constant</summary>
	/// <param name="code">The <see cref="T:HarmonyLib.CodeInstruction" /></param>
	/// <returns>True if the instruction loads a constant</returns>
	public static bool LoadsConstant(this CodeInstruction code)
	{
		return constantLoadingCodes.Contains(code.opcode);
	}

	/// <summary>Tests if the code instruction loads an integer constant</summary>
	/// <param name="code">The <see cref="T:HarmonyLib.CodeInstruction" /></param>
	/// <param name="number">The integer constant</param>
	/// <returns>True if the instruction loads the constant</returns>
	public static bool LoadsConstant(this CodeInstruction code, long number)
	{
		OpCode opcode = code.opcode;
		if (number == -1 && opcode == OpCodes.Ldc_I4_M1)
		{
			return true;
		}
		if (number == 0L && opcode == OpCodes.Ldc_I4_0)
		{
			return true;
		}
		if (number == 1 && opcode == OpCodes.Ldc_I4_1)
		{
			return true;
		}
		if (number == 2 && opcode == OpCodes.Ldc_I4_2)
		{
			return true;
		}
		if (number == 3 && opcode == OpCodes.Ldc_I4_3)
		{
			return true;
		}
		if (number == 4 && opcode == OpCodes.Ldc_I4_4)
		{
			return true;
		}
		if (number == 5 && opcode == OpCodes.Ldc_I4_5)
		{
			return true;
		}
		if (number == 6 && opcode == OpCodes.Ldc_I4_6)
		{
			return true;
		}
		if (number == 7 && opcode == OpCodes.Ldc_I4_7)
		{
			return true;
		}
		if (number == 8 && opcode == OpCodes.Ldc_I4_8)
		{
			return true;
		}
		if (opcode != OpCodes.Ldc_I4 && opcode != OpCodes.Ldc_I4_S && opcode != OpCodes.Ldc_I8)
		{
			return false;
		}
		return Convert.ToInt64(code.operand) == number;
	}

	/// <summary>Tests if the code instruction loads a floating point constant</summary>
	/// <param name="code">The <see cref="T:HarmonyLib.CodeInstruction" /></param>
	/// <param name="number">The floating point constant</param>
	/// <returns>True if the instruction loads the constant</returns>
	public static bool LoadsConstant(this CodeInstruction code, double number)
	{
		if (code.opcode != OpCodes.Ldc_R4 && code.opcode != OpCodes.Ldc_R8)
		{
			return false;
		}
		double num = Convert.ToDouble(code.operand);
		return num == number;
	}

	/// <summary>Tests if the code instruction loads an enum constant</summary>
	/// <param name="code">The <see cref="T:HarmonyLib.CodeInstruction" /></param>
	/// <param name="e">The enum</param>
	/// <returns>True if the instruction loads the constant</returns>
	public static bool LoadsConstant(this CodeInstruction code, Enum e)
	{
		return code.LoadsConstant(Convert.ToInt64(e));
	}

	/// <summary>Tests if the code instruction loads a string constant</summary>
	/// <param name="code">The <see cref="T:HarmonyLib.CodeInstruction" /></param>
	/// <param name="str">The string</param>
	/// <returns>True if the instruction loads the constant</returns>
	public static bool LoadsConstant(this CodeInstruction code, string str)
	{
		if (code.opcode != OpCodes.Ldstr)
		{
			return false;
		}
		string text = Convert.ToString(code.operand);
		return text == str;
	}

	/// <summary>Tests if the code instruction loads a field</summary>
	/// <param name="code">The <see cref="T:HarmonyLib.CodeInstruction" /></param>
	/// <param name="field">The field</param>
	/// <param name="byAddress">Set to true if the address of the field is loaded</param>
	/// <returns>True if the instruction loads the field</returns>
	public static bool LoadsField(this CodeInstruction code, FieldInfo field, bool byAddress = false)
	{
		if ((object)field == null)
		{
			throw new ArgumentNullException("field");
		}
		OpCode opCode = (field.IsStatic ? OpCodes.Ldsfld : OpCodes.Ldfld);
		if (!byAddress && code.opcode == opCode && object.Equals(code.operand, field))
		{
			return true;
		}
		OpCode opCode2 = (field.IsStatic ? OpCodes.Ldsflda : OpCodes.Ldflda);
		if (byAddress && code.opcode == opCode2 && object.Equals(code.operand, field))
		{
			return true;
		}
		return false;
	}

	/// <summary>Tests if the code instruction stores a field</summary>
	/// <param name="code">The <see cref="T:HarmonyLib.CodeInstruction" /></param>
	/// <param name="field">The field</param>
	/// <returns>True if the instruction stores this field</returns>
	public static bool StoresField(this CodeInstruction code, FieldInfo field)
	{
		if ((object)field == null)
		{
			throw new ArgumentNullException("field");
		}
		OpCode opCode = (field.IsStatic ? OpCodes.Stsfld : OpCodes.Stfld);
		if (code.opcode == opCode)
		{
			return object.Equals(code.operand, field);
		}
		return false;
	}

	/// <summary>Returns the index targeted by this <c>ldloc</c>, <c>ldloca</c>, or <c>stloc</c></summary>
	/// <param name="code">The <see cref="T:HarmonyLib.CodeInstruction" /></param>
	/// <returns>The index it targets</returns>
	/// <seealso cref="M:HarmonyLib.CodeInstruction.LoadLocal(System.Int32,System.Boolean)" />
	/// <seealso cref="M:HarmonyLib.CodeInstruction.StoreLocal(System.Int32)" />
	public static int LocalIndex(this CodeInstruction code)
	{
		if (code.opcode == OpCodes.Ldloc_0 || code.opcode == OpCodes.Stloc_0)
		{
			return 0;
		}
		if (code.opcode == OpCodes.Ldloc_1 || code.opcode == OpCodes.Stloc_1)
		{
			return 1;
		}
		if (code.opcode == OpCodes.Ldloc_2 || code.opcode == OpCodes.Stloc_2)
		{
			return 2;
		}
		if (code.opcode == OpCodes.Ldloc_3 || code.opcode == OpCodes.Stloc_3)
		{
			return 3;
		}
		if (code.opcode == OpCodes.Ldloc_S || code.opcode == OpCodes.Ldloc)
		{
			if (code.operand is LocalBuilder localBuilder)
			{
				return localBuilder.LocalIndex;
			}
			return Convert.ToInt32(code.operand);
		}
		if (code.opcode == OpCodes.Stloc_S || code.opcode == OpCodes.Stloc)
		{
			if (code.operand is LocalBuilder localBuilder2)
			{
				return localBuilder2.LocalIndex;
			}
			return Convert.ToInt32(code.operand);
		}
		if (code.opcode == OpCodes.Ldloca_S || code.opcode == OpCodes.Ldloca)
		{
			if (code.operand is LocalBuilder localBuilder3)
			{
				return localBuilder3.LocalIndex;
			}
			return Convert.ToInt32(code.operand);
		}
		throw new ArgumentException("Instruction is not a load or store", "code");
	}

	/// <summary>Returns the index targeted by this <c>ldarg</c>, <c>ldarga</c>, or <c>starg</c></summary>
	/// <param name="code">The <see cref="T:HarmonyLib.CodeInstruction" /></param>
	/// <returns>The index it targets</returns>
	/// <seealso cref="M:HarmonyLib.CodeInstruction.LoadArgument(System.Int32,System.Boolean)" />
	/// <seealso cref="M:HarmonyLib.CodeInstruction.StoreArgument(System.Int32)" />
	public static int ArgumentIndex(this CodeInstruction code)
	{
		if (code.opcode == OpCodes.Ldarg_0)
		{
			return 0;
		}
		if (code.opcode == OpCodes.Ldarg_1)
		{
			return 1;
		}
		if (code.opcode == OpCodes.Ldarg_2)
		{
			return 2;
		}
		if (code.opcode == OpCodes.Ldarg_3)
		{
			return 3;
		}
		if (code.opcode == OpCodes.Ldarg_S || code.opcode == OpCodes.Ldarg)
		{
			return Convert.ToInt32(code.operand);
		}
		if (code.opcode == OpCodes.Starg_S || code.opcode == OpCodes.Starg)
		{
			return Convert.ToInt32(code.operand);
		}
		if (code.opcode == OpCodes.Ldarga_S || code.opcode == OpCodes.Ldarga)
		{
			return Convert.ToInt32(code.operand);
		}
		throw new ArgumentException("Instruction is not a load or store", "code");
	}

	/// <summary>Adds labels to the code instruction and return it</summary>
	/// <param name="code">The <see cref="T:HarmonyLib.CodeInstruction" /></param>
	/// <param name="labels">One or several <see cref="T:System.Reflection.Emit.Label" /> to add</param>
	/// <returns>The same code instruction</returns>
	public static CodeInstruction WithLabels(this CodeInstruction code, params Label[] labels)
	{
		code.labels.AddRange(labels);
		return code;
	}

	/// <summary>Adds labels to the code instruction and return it</summary>
	/// <param name="code">The <see cref="T:HarmonyLib.CodeInstruction" /></param>
	/// <param name="labels">An enumeration of <see cref="T:System.Reflection.Emit.Label" /></param>
	/// <returns>The same code instruction</returns>
	public static CodeInstruction WithLabels(this CodeInstruction code, IEnumerable<Label> labels)
	{
		code.labels.AddRange(labels);
		return code;
	}

	/// <summary>Extracts all labels from the code instruction and returns them</summary>
	/// <param name="code">The <see cref="T:HarmonyLib.CodeInstruction" /></param>
	/// <returns>A list of <see cref="T:System.Reflection.Emit.Label" /></returns>
	public static List<Label> ExtractLabels(this CodeInstruction code)
	{
		List<Label> result = new List<Label>(code.labels);
		code.labels.Clear();
		return result;
	}

	/// <summary>Moves all labels from the code instruction to another one</summary>
	/// <param name="code">The <see cref="T:HarmonyLib.CodeInstruction" /> to move the labels from</param>
	/// <param name="other">The other <see cref="T:HarmonyLib.CodeInstruction" /> to move the labels to</param>
	/// <returns>The code instruction labels were moved from (now empty)</returns>
	public static CodeInstruction MoveLabelsTo(this CodeInstruction code, CodeInstruction other)
	{
		other.WithLabels(code.ExtractLabels());
		return code;
	}

	/// <summary>Moves all labels from another code instruction to the current one</summary>
	/// <param name="code">The <see cref="T:HarmonyLib.CodeInstruction" /> to move the labels to</param>
	/// <param name="other">The other <see cref="T:HarmonyLib.CodeInstruction" /> to move the labels from</param>
	/// <returns>The code instruction that received the labels</returns>
	public static CodeInstruction MoveLabelsFrom(this CodeInstruction code, CodeInstruction other)
	{
		return code.WithLabels(other.ExtractLabels());
	}

	/// <summary>Adds ExceptionBlocks to the code instruction and return it</summary>
	/// <param name="code">The <see cref="T:HarmonyLib.CodeInstruction" /></param>
	/// <param name="blocks">One or several <see cref="T:HarmonyLib.ExceptionBlock" /> to add</param>
	/// <returns>The same code instruction</returns>
	public static CodeInstruction WithBlocks(this CodeInstruction code, params ExceptionBlock[] blocks)
	{
		code.blocks.AddRange(blocks);
		return code;
	}

	/// <summary>Adds ExceptionBlocks to the code instruction and return it</summary>
	/// <param name="code">The <see cref="T:HarmonyLib.CodeInstruction" /></param>
	/// <param name="blocks">An enumeration of <see cref="T:HarmonyLib.ExceptionBlock" /></param>
	/// <returns>The same code instruction</returns>
	public static CodeInstruction WithBlocks(this CodeInstruction code, IEnumerable<ExceptionBlock> blocks)
	{
		code.blocks.AddRange(blocks);
		return code;
	}

	/// <summary>Extracts all ExceptionBlocks from the code instruction and returns them</summary>
	/// <param name="code">The <see cref="T:HarmonyLib.CodeInstruction" /></param>
	/// <returns>A list of <see cref="T:HarmonyLib.ExceptionBlock" /></returns>
	public static List<ExceptionBlock> ExtractBlocks(this CodeInstruction code)
	{
		List<ExceptionBlock> result = new List<ExceptionBlock>(code.blocks);
		code.blocks.Clear();
		return result;
	}

	/// <summary>Moves all ExceptionBlocks from the code instruction to another one</summary>
	/// <param name="code">The <see cref="T:HarmonyLib.CodeInstruction" /> to move the ExceptionBlocks from</param>
	/// <param name="other">The other <see cref="T:HarmonyLib.CodeInstruction" /> to move the ExceptionBlocks to</param>
	/// <returns>The code instruction blocks were moved from (now empty)</returns>
	public static CodeInstruction MoveBlocksTo(this CodeInstruction code, CodeInstruction other)
	{
		other.WithBlocks(code.ExtractBlocks());
		return code;
	}

	/// <summary>Moves all ExceptionBlocks from another code instruction to the current one</summary>
	/// <param name="code">The <see cref="T:HarmonyLib.CodeInstruction" /> to move the ExceptionBlocks to</param>
	/// <param name="other">The other <see cref="T:HarmonyLib.CodeInstruction" /> to move the ExceptionBlocks from</param>
	/// <returns>The code instruction that received the blocks</returns>
	public static CodeInstruction MoveBlocksFrom(this CodeInstruction code, CodeInstruction other)
	{
		return code.WithBlocks(other.ExtractBlocks());
	}
}
