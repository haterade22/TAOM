using System;
using System.Collections.Generic;
using Mono.Cecil.Metadata;
using Mono.Cecil.PE;
using Mono.Collections.Generic;

namespace Mono.Cecil.Cil;

internal sealed class CodeWriter : ByteBuffer
{
	private readonly uint code_base;

	internal readonly MetadataBuilder metadata;

	private readonly Dictionary<uint, MetadataToken> standalone_signatures;

	private readonly Dictionary<ByteBuffer, uint> tiny_method_bodies;

	private MethodBody body;

	public CodeWriter(MetadataBuilder metadata)
		: base(0)
	{
		code_base = metadata.text_map.GetRVA(TextSegment.Code);
		this.metadata = metadata;
		standalone_signatures = new Dictionary<uint, MetadataToken>();
		tiny_method_bodies = new Dictionary<ByteBuffer, uint>(new ByteBufferEqualityComparer());
	}

	public uint WriteMethodBody(MethodDefinition method)
	{
		if (IsUnresolved(method))
		{
			if (method.rva == 0)
			{
				return 0u;
			}
			return WriteUnresolvedMethodBody(method);
		}
		if (IsEmptyMethodBody(method.Body))
		{
			return 0u;
		}
		return WriteResolvedMethodBody(method);
	}

	private static bool IsEmptyMethodBody(MethodBody body)
	{
		if (body.instructions.IsNullOrEmpty())
		{
			return body.variables.IsNullOrEmpty();
		}
		return false;
	}

	private static bool IsUnresolved(MethodDefinition method)
	{
		if (method.HasBody && method.HasImage)
		{
			return method.body == null;
		}
		return false;
	}

	private uint WriteUnresolvedMethodBody(MethodDefinition method)
	{
		int code_size;
		MetadataToken local_var_token;
		ByteBuffer byteBuffer = metadata.module.reader.code.PatchRawMethodBody(method, this, out code_size, out local_var_token);
		bool num = (byteBuffer.buffer[0] & 3) == 3;
		if (num)
		{
			Align(4);
		}
		uint rva = BeginMethod();
		if (num || !GetOrMapTinyMethodBody(byteBuffer, ref rva))
		{
			WriteBytes(byteBuffer);
		}
		if (method.debug_info == null)
		{
			return rva;
		}
		ISymbolWriter symbol_writer = metadata.symbol_writer;
		if (symbol_writer != null)
		{
			method.debug_info.code_size = code_size;
			method.debug_info.local_var_token = local_var_token;
			symbol_writer.Write(method.debug_info);
		}
		return rva;
	}

	private uint WriteResolvedMethodBody(MethodDefinition method)
	{
		body = method.Body;
		ComputeHeader();
		uint rva;
		if (RequiresFatHeader())
		{
			Align(4);
			rva = BeginMethod();
			WriteFatHeader();
			WriteInstructions();
			if (body.HasExceptionHandlers)
			{
				WriteExceptionHandlers();
			}
		}
		else
		{
			rva = BeginMethod();
			WriteByte((byte)(2 | (body.CodeSize << 2)));
			WriteInstructions();
			int num = (int)(rva - code_base);
			int num2 = position - num;
			byte[] destinationArray = new byte[num2];
			Array.Copy(buffer, num, destinationArray, 0, num2);
			if (GetOrMapTinyMethodBody(new ByteBuffer(destinationArray), ref rva))
			{
				position = num;
			}
		}
		ISymbolWriter symbol_writer = metadata.symbol_writer;
		if (symbol_writer != null && method.debug_info != null)
		{
			method.debug_info.code_size = body.CodeSize;
			method.debug_info.local_var_token = body.local_var_token;
			symbol_writer.Write(method.debug_info);
		}
		return rva;
	}

	private bool GetOrMapTinyMethodBody(ByteBuffer body, ref uint rva)
	{
		if (tiny_method_bodies.TryGetValue(body, out var value))
		{
			rva = value;
			return true;
		}
		tiny_method_bodies.Add(body, rva);
		return false;
	}

	private void WriteFatHeader()
	{
		MethodBody methodBody = body;
		byte b = 3;
		if (methodBody.InitLocals)
		{
			b |= 0x10;
		}
		if (methodBody.HasExceptionHandlers)
		{
			b |= 8;
		}
		WriteByte(b);
		WriteByte(48);
		WriteInt16((short)methodBody.max_stack_size);
		WriteInt32(methodBody.code_size);
		methodBody.local_var_token = (methodBody.HasVariables ? GetStandAloneSignature(methodBody.Variables) : MetadataToken.Zero);
		WriteMetadataToken(methodBody.local_var_token);
	}

	private void WriteInstructions()
	{
		Collection<Instruction> instructions = body.Instructions;
		Instruction[] items = instructions.items;
		int size = instructions.size;
		for (int i = 0; i < size; i++)
		{
			Instruction instruction = items[i];
			WriteOpCode(instruction.opcode);
			WriteOperand(instruction);
		}
	}

	private void WriteOpCode(OpCode opcode)
	{
		if (opcode.Size == 1)
		{
			WriteByte(opcode.Op2);
			return;
		}
		WriteByte(opcode.Op1);
		WriteByte(opcode.Op2);
	}

	private void WriteOperand(Instruction instruction)
	{
		OpCode opcode = instruction.opcode;
		OperandType operandType = opcode.OperandType;
		if (operandType == OperandType.InlineNone)
		{
			return;
		}
		object operand = instruction.operand;
		if (operand == null && operandType != OperandType.InlineBrTarget && operandType != OperandType.ShortInlineBrTarget)
		{
			throw new ArgumentException();
		}
		switch (operandType)
		{
		case OperandType.InlineSwitch:
		{
			Instruction[] array = (Instruction[])operand;
			WriteInt32(array.Length);
			int num2 = instruction.Offset + opcode.Size + 4 * (array.Length + 1);
			for (int i = 0; i < array.Length; i++)
			{
				WriteInt32(GetTargetOffset(array[i]) - num2);
			}
			break;
		}
		case OperandType.ShortInlineBrTarget:
		{
			Instruction instruction2 = (Instruction)operand;
			int num = ((instruction2 != null) ? GetTargetOffset(instruction2) : body.code_size);
			WriteSByte((sbyte)(num - (instruction.Offset + opcode.Size + 1)));
			break;
		}
		case OperandType.InlineBrTarget:
		{
			Instruction instruction3 = (Instruction)operand;
			int num3 = ((instruction3 != null) ? GetTargetOffset(instruction3) : body.code_size);
			WriteInt32(num3 - (instruction.Offset + opcode.Size + 4));
			break;
		}
		case OperandType.ShortInlineVar:
			WriteByte((byte)GetVariableIndex((VariableDefinition)operand));
			break;
		case OperandType.ShortInlineArg:
			WriteByte((byte)GetParameterIndex((ParameterDefinition)operand));
			break;
		case OperandType.InlineVar:
			WriteInt16((short)GetVariableIndex((VariableDefinition)operand));
			break;
		case OperandType.InlineArg:
			WriteInt16((short)GetParameterIndex((ParameterDefinition)operand));
			break;
		case OperandType.InlineSig:
			WriteMetadataToken(GetStandAloneSignature((CallSite)operand));
			break;
		case OperandType.ShortInlineI:
			if (opcode == OpCodes.Ldc_I4_S)
			{
				WriteSByte((sbyte)operand);
			}
			else
			{
				WriteByte((byte)operand);
			}
			break;
		case OperandType.InlineI:
			WriteInt32((int)operand);
			break;
		case OperandType.InlineI8:
			WriteInt64((long)operand);
			break;
		case OperandType.ShortInlineR:
			WriteSingle((float)operand);
			break;
		case OperandType.InlineR:
			WriteDouble((double)operand);
			break;
		case OperandType.InlineString:
			WriteMetadataToken(new MetadataToken(TokenType.String, GetUserStringIndex((string)operand)));
			break;
		case OperandType.InlineField:
		case OperandType.InlineMethod:
		case OperandType.InlineTok:
		case OperandType.InlineType:
			WriteMetadataToken(metadata.LookupToken((IMetadataTokenProvider)operand));
			break;
		default:
			throw new ArgumentException();
		}
	}

	private int GetTargetOffset(Instruction instruction)
	{
		if (instruction == null)
		{
			Instruction instruction2 = body.instructions[body.instructions.size - 1];
			return instruction2.offset + instruction2.GetSize();
		}
		return instruction.offset;
	}

	private uint GetUserStringIndex(string @string)
	{
		if (@string == null)
		{
			return 0u;
		}
		return metadata.user_string_heap.GetStringIndex(@string);
	}

	private static int GetVariableIndex(VariableDefinition variable)
	{
		return variable.Index;
	}

	private int GetParameterIndex(ParameterDefinition parameter)
	{
		if (body.method.HasThis)
		{
			if (parameter == body.this_parameter)
			{
				return 0;
			}
			return parameter.Index + 1;
		}
		return parameter.Index;
	}

	private bool RequiresFatHeader()
	{
		MethodBody methodBody = body;
		if (methodBody.CodeSize < 64 && !methodBody.InitLocals && !methodBody.HasVariables && !methodBody.HasExceptionHandlers)
		{
			return methodBody.MaxStackSize > 8;
		}
		return true;
	}

	private void ComputeHeader()
	{
		int num = 0;
		Collection<Instruction> instructions = body.instructions;
		Instruction[] items = instructions.items;
		int size = instructions.size;
		int stack_size = 0;
		int max_stack = 0;
		Dictionary<Instruction, int> stack_sizes = null;
		if (body.HasExceptionHandlers)
		{
			ComputeExceptionHandlerStackSize(ref stack_sizes);
		}
		for (int i = 0; i < size; i++)
		{
			Instruction instruction = items[i];
			instruction.offset = num;
			num += instruction.GetSize();
			ComputeStackSize(instruction, ref stack_sizes, ref stack_size, ref max_stack);
		}
		body.code_size = num;
		body.max_stack_size = max_stack;
	}

	private void ComputeExceptionHandlerStackSize(ref Dictionary<Instruction, int> stack_sizes)
	{
		Collection<ExceptionHandler> exceptionHandlers = body.ExceptionHandlers;
		for (int i = 0; i < exceptionHandlers.Count; i++)
		{
			ExceptionHandler exceptionHandler = exceptionHandlers[i];
			switch (exceptionHandler.HandlerType)
			{
			case ExceptionHandlerType.Catch:
				AddExceptionStackSize(exceptionHandler.HandlerStart, ref stack_sizes);
				break;
			case ExceptionHandlerType.Filter:
				AddExceptionStackSize(exceptionHandler.FilterStart, ref stack_sizes);
				AddExceptionStackSize(exceptionHandler.HandlerStart, ref stack_sizes);
				break;
			}
		}
	}

	private static void AddExceptionStackSize(Instruction handler_start, ref Dictionary<Instruction, int> stack_sizes)
	{
		if (handler_start != null)
		{
			if (stack_sizes == null)
			{
				stack_sizes = new Dictionary<Instruction, int>();
			}
			stack_sizes[handler_start] = 1;
		}
	}

	private static void ComputeStackSize(Instruction instruction, ref Dictionary<Instruction, int> stack_sizes, ref int stack_size, ref int max_stack)
	{
		if (stack_sizes != null && stack_sizes.TryGetValue(instruction, out var value))
		{
			stack_size = value;
		}
		max_stack = Math.Max(max_stack, stack_size);
		ComputeStackDelta(instruction, ref stack_size);
		max_stack = Math.Max(max_stack, stack_size);
		CopyBranchStackSize(instruction, ref stack_sizes, stack_size);
		ComputeStackSize(instruction, ref stack_size);
	}

	private static void CopyBranchStackSize(Instruction instruction, ref Dictionary<Instruction, int> stack_sizes, int stack_size)
	{
		if (stack_size == 0)
		{
			return;
		}
		switch (instruction.opcode.OperandType)
		{
		case OperandType.InlineBrTarget:
		case OperandType.ShortInlineBrTarget:
			CopyBranchStackSize(ref stack_sizes, (Instruction)instruction.operand, stack_size);
			break;
		case OperandType.InlineSwitch:
		{
			Instruction[] array = (Instruction[])instruction.operand;
			for (int i = 0; i < array.Length; i++)
			{
				CopyBranchStackSize(ref stack_sizes, array[i], stack_size);
			}
			break;
		}
		}
	}

	private static void CopyBranchStackSize(ref Dictionary<Instruction, int> stack_sizes, Instruction target, int stack_size)
	{
		if (stack_sizes == null)
		{
			stack_sizes = new Dictionary<Instruction, int>();
		}
		int num = stack_size;
		if (stack_sizes.TryGetValue(target, out var value))
		{
			num = Math.Max(num, value);
		}
		stack_sizes[target] = num;
	}

	private static void ComputeStackSize(Instruction instruction, ref int stack_size)
	{
		FlowControl flowControl = instruction.opcode.FlowControl;
		if (flowControl == FlowControl.Branch || (uint)(flowControl - 7) <= 1u)
		{
			stack_size = 0;
		}
	}

	private static void ComputeStackDelta(Instruction instruction, ref int stack_size)
	{
		if (instruction.opcode.FlowControl == FlowControl.Call)
		{
			IMethodSignature methodSignature = (IMethodSignature)instruction.operand;
			if (methodSignature.HasImplicitThis() && instruction.opcode.Code != Code.Newobj)
			{
				stack_size--;
			}
			if (methodSignature.HasParameters)
			{
				stack_size -= methodSignature.Parameters.Count;
			}
			if (instruction.opcode.Code == Code.Calli)
			{
				stack_size--;
			}
			if (methodSignature.ReturnType.etype != ElementType.Void || instruction.opcode.Code == Code.Newobj)
			{
				stack_size++;
			}
		}
		else
		{
			ComputePopDelta(instruction.opcode.StackBehaviourPop, ref stack_size);
			ComputePushDelta(instruction.opcode.StackBehaviourPush, ref stack_size);
		}
	}

	private static void ComputePopDelta(StackBehaviour pop_behavior, ref int stack_size)
	{
		switch (pop_behavior)
		{
		case StackBehaviour.Pop1:
		case StackBehaviour.Popi:
		case StackBehaviour.Popref:
			stack_size--;
			break;
		case StackBehaviour.Pop1_pop1:
		case StackBehaviour.Popi_pop1:
		case StackBehaviour.Popi_popi:
		case StackBehaviour.Popi_popi8:
		case StackBehaviour.Popi_popr4:
		case StackBehaviour.Popi_popr8:
		case StackBehaviour.Popref_pop1:
		case StackBehaviour.Popref_popi:
			stack_size -= 2;
			break;
		case StackBehaviour.Popi_popi_popi:
		case StackBehaviour.Popref_popi_popi:
		case StackBehaviour.Popref_popi_popi8:
		case StackBehaviour.Popref_popi_popr4:
		case StackBehaviour.Popref_popi_popr8:
		case StackBehaviour.Popref_popi_popref:
			stack_size -= 3;
			break;
		case StackBehaviour.PopAll:
			stack_size = 0;
			break;
		}
	}

	private static void ComputePushDelta(StackBehaviour push_behaviour, ref int stack_size)
	{
		switch (push_behaviour)
		{
		case StackBehaviour.Push1:
		case StackBehaviour.Pushi:
		case StackBehaviour.Pushi8:
		case StackBehaviour.Pushr4:
		case StackBehaviour.Pushr8:
		case StackBehaviour.Pushref:
			stack_size++;
			break;
		case StackBehaviour.Push1_push1:
			stack_size += 2;
			break;
		}
	}

	private void WriteExceptionHandlers()
	{
		Align(4);
		Collection<ExceptionHandler> exceptionHandlers = body.ExceptionHandlers;
		if (exceptionHandlers.Count < 21 && !RequiresFatSection(exceptionHandlers))
		{
			WriteSmallSection(exceptionHandlers);
		}
		else
		{
			WriteFatSection(exceptionHandlers);
		}
	}

	private static bool RequiresFatSection(Collection<ExceptionHandler> handlers)
	{
		for (int i = 0; i < handlers.Count; i++)
		{
			ExceptionHandler exceptionHandler = handlers[i];
			if (IsFatRange(exceptionHandler.TryStart, exceptionHandler.TryEnd))
			{
				return true;
			}
			if (IsFatRange(exceptionHandler.HandlerStart, exceptionHandler.HandlerEnd))
			{
				return true;
			}
			if (exceptionHandler.HandlerType == ExceptionHandlerType.Filter && IsFatRange(exceptionHandler.FilterStart, exceptionHandler.HandlerStart))
			{
				return true;
			}
		}
		return false;
	}

	private static bool IsFatRange(Instruction start, Instruction end)
	{
		if (start == null)
		{
			throw new ArgumentException();
		}
		if (end == null)
		{
			return true;
		}
		if (end.Offset - start.Offset <= 255)
		{
			return start.Offset > 65535;
		}
		return true;
	}

	private void WriteSmallSection(Collection<ExceptionHandler> handlers)
	{
		WriteByte(1);
		WriteByte((byte)(handlers.Count * 12 + 4));
		WriteBytes(2);
		WriteExceptionHandlers(handlers, delegate(int i)
		{
			WriteUInt16((ushort)i);
		}, delegate(int i)
		{
			WriteByte((byte)i);
		});
	}

	private void WriteFatSection(Collection<ExceptionHandler> handlers)
	{
		WriteByte(65);
		int num = handlers.Count * 24 + 4;
		WriteByte((byte)(num & 0xFF));
		WriteByte((byte)((num >> 8) & 0xFF));
		WriteByte((byte)((num >> 16) & 0xFF));
		WriteExceptionHandlers(handlers, base.WriteInt32, base.WriteInt32);
	}

	private void WriteExceptionHandlers(Collection<ExceptionHandler> handlers, Action<int> write_entry, Action<int> write_length)
	{
		for (int i = 0; i < handlers.Count; i++)
		{
			ExceptionHandler exceptionHandler = handlers[i];
			write_entry((int)exceptionHandler.HandlerType);
			write_entry(exceptionHandler.TryStart.Offset);
			write_length(GetTargetOffset(exceptionHandler.TryEnd) - exceptionHandler.TryStart.Offset);
			write_entry(exceptionHandler.HandlerStart.Offset);
			write_length(GetTargetOffset(exceptionHandler.HandlerEnd) - exceptionHandler.HandlerStart.Offset);
			WriteExceptionHandlerSpecific(exceptionHandler);
		}
	}

	private void WriteExceptionHandlerSpecific(ExceptionHandler handler)
	{
		switch (handler.HandlerType)
		{
		case ExceptionHandlerType.Catch:
			WriteMetadataToken(metadata.LookupToken(handler.CatchType));
			break;
		case ExceptionHandlerType.Filter:
			WriteInt32(handler.FilterStart.Offset);
			break;
		default:
			WriteInt32(0);
			break;
		}
	}

	public MetadataToken GetStandAloneSignature(Collection<VariableDefinition> variables)
	{
		uint localVariableBlobIndex = metadata.GetLocalVariableBlobIndex(variables);
		return GetStandAloneSignatureToken(localVariableBlobIndex);
	}

	public MetadataToken GetStandAloneSignature(CallSite call_site)
	{
		uint callSiteBlobIndex = metadata.GetCallSiteBlobIndex(call_site);
		return call_site.MetadataToken = GetStandAloneSignatureToken(callSiteBlobIndex);
	}

	private MetadataToken GetStandAloneSignatureToken(uint signature)
	{
		if (standalone_signatures.TryGetValue(signature, out var value))
		{
			return value;
		}
		value = new MetadataToken(TokenType.Signature, metadata.AddStandAloneSignature(signature));
		standalone_signatures.Add(signature, value);
		return value;
	}

	private uint BeginMethod()
	{
		return (uint)(code_base + position);
	}

	private void WriteMetadataToken(MetadataToken token)
	{
		WriteUInt32(token.ToUInt32());
	}

	private void Align(int align)
	{
		align--;
		WriteBytes(((position + align) & ~align) - position);
	}
}
