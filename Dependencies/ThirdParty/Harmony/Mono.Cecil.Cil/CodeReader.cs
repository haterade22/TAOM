using System;
using Mono.Cecil.PE;
using Mono.Collections.Generic;

namespace Mono.Cecil.Cil;

internal sealed class CodeReader : BinaryStreamReader
{
	internal readonly MetadataReader reader;

	private int start;

	private MethodDefinition method;

	private MethodBody body;

	private int Offset => base.Position - start;

	public CodeReader(MetadataReader reader)
		: base(reader.image.Stream.value)
	{
		this.reader = reader;
	}

	public int MoveTo(MethodDefinition method)
	{
		this.method = method;
		reader.context = method;
		int position = base.Position;
		base.Position = (int)reader.image.ResolveVirtualAddress((uint)method.RVA);
		return position;
	}

	public void MoveBackTo(int position)
	{
		reader.context = null;
		base.Position = position;
	}

	public MethodBody ReadMethodBody(MethodDefinition method)
	{
		int position = MoveTo(method);
		body = new MethodBody(method);
		ReadMethodBody();
		MoveBackTo(position);
		return body;
	}

	public int ReadCodeSize(MethodDefinition method)
	{
		int position = MoveTo(method);
		int result = ReadCodeSize();
		MoveBackTo(position);
		return result;
	}

	private int ReadCodeSize()
	{
		byte b = ReadByte();
		switch (b & 3)
		{
		case 2:
			return b >> 2;
		case 3:
			Advance(3);
			return (int)ReadUInt32();
		default:
			throw new InvalidOperationException();
		}
	}

	private void ReadMethodBody()
	{
		byte b = ReadByte();
		switch (b & 3)
		{
		case 2:
			body.code_size = b >> 2;
			body.MaxStackSize = 8;
			ReadCode();
			break;
		case 3:
			Advance(-1);
			ReadFatMethod();
			break;
		default:
			throw new InvalidOperationException();
		}
		ISymbolReader symbol_reader = reader.module.symbol_reader;
		if (symbol_reader != null && method.debug_info == null)
		{
			method.debug_info = symbol_reader.Read(method);
		}
		if (method.debug_info != null)
		{
			ReadDebugInfo();
		}
	}

	private void ReadFatMethod()
	{
		ushort num = ReadUInt16();
		body.max_stack_size = ReadUInt16();
		body.code_size = (int)ReadUInt32();
		body.local_var_token = new MetadataToken(ReadUInt32());
		body.init_locals = (num & 0x10) != 0;
		if (body.local_var_token.RID != 0)
		{
			body.variables = ReadVariables(body.local_var_token);
		}
		ReadCode();
		if ((num & 8) != 0)
		{
			ReadSection();
		}
	}

	public VariableDefinitionCollection ReadVariables(MetadataToken local_var_token)
	{
		int position = reader.position;
		VariableDefinitionCollection result = reader.ReadVariables(local_var_token, method);
		reader.position = position;
		return result;
	}

	private void ReadCode()
	{
		start = base.Position;
		int num = body.code_size;
		if (num < 0 || base.Length <= (uint)(num + base.Position))
		{
			num = 0;
		}
		int num2 = start + num;
		Collection<Instruction> collection = (body.instructions = new InstructionCollection(method, (num + 1) / 2));
		while (base.Position < num2)
		{
			int offset = base.Position - start;
			OpCode opCode = ReadOpCode();
			Instruction instruction = new Instruction(offset, opCode);
			if (opCode.OperandType != OperandType.InlineNone)
			{
				instruction.operand = ReadOperand(instruction);
			}
			collection.Add(instruction);
		}
		ResolveBranches(collection);
	}

	private OpCode ReadOpCode()
	{
		byte b = ReadByte();
		if (b == 254)
		{
			return OpCodes.TwoBytesOpCode[ReadByte()];
		}
		return OpCodes.OneByteOpCode[b];
	}

	private object ReadOperand(Instruction instruction)
	{
		switch (instruction.opcode.OperandType)
		{
		case OperandType.InlineSwitch:
		{
			int num = ReadInt32();
			int num2 = Offset + 4 * num;
			int[] array = new int[num];
			for (int i = 0; i < num; i++)
			{
				array[i] = num2 + ReadInt32();
			}
			return array;
		}
		case OperandType.ShortInlineBrTarget:
			return ReadSByte() + Offset;
		case OperandType.InlineBrTarget:
			return ReadInt32() + Offset;
		case OperandType.ShortInlineI:
			if (instruction.opcode == OpCodes.Ldc_I4_S)
			{
				return ReadSByte();
			}
			return ReadByte();
		case OperandType.InlineI:
			return ReadInt32();
		case OperandType.ShortInlineR:
			return ReadSingle();
		case OperandType.InlineR:
			return ReadDouble();
		case OperandType.InlineI8:
			return ReadInt64();
		case OperandType.ShortInlineVar:
			return GetVariable(ReadByte());
		case OperandType.InlineVar:
			return GetVariable(ReadUInt16());
		case OperandType.ShortInlineArg:
			return GetParameter(ReadByte());
		case OperandType.InlineArg:
			return GetParameter(ReadUInt16());
		case OperandType.InlineSig:
			return GetCallSite(ReadToken());
		case OperandType.InlineString:
			return GetString(ReadToken());
		case OperandType.InlineField:
		case OperandType.InlineMethod:
		case OperandType.InlineTok:
		case OperandType.InlineType:
			return reader.LookupToken(ReadToken());
		default:
			throw new NotSupportedException();
		}
	}

	public string GetString(MetadataToken token)
	{
		return reader.image.UserStringHeap.Read(token.RID);
	}

	public ParameterDefinition GetParameter(int index)
	{
		return body.GetParameter(index);
	}

	public VariableDefinition GetVariable(int index)
	{
		return body.GetVariable(index);
	}

	public CallSite GetCallSite(MetadataToken token)
	{
		return reader.ReadCallSite(token);
	}

	private void ResolveBranches(Collection<Instruction> instructions)
	{
		Instruction[] items = instructions.items;
		int size = instructions.size;
		for (int i = 0; i < size; i++)
		{
			Instruction instruction = items[i];
			switch (instruction.opcode.OperandType)
			{
			case OperandType.InlineBrTarget:
			case OperandType.ShortInlineBrTarget:
				instruction.operand = GetInstruction((int)instruction.operand);
				break;
			case OperandType.InlineSwitch:
			{
				int[] array = (int[])instruction.operand;
				Instruction[] array2 = new Instruction[array.Length];
				for (int j = 0; j < array.Length; j++)
				{
					array2[j] = GetInstruction(array[j]);
				}
				instruction.operand = array2;
				break;
			}
			}
		}
	}

	private Instruction GetInstruction(int offset)
	{
		return GetInstruction(body.Instructions, offset);
	}

	private static Instruction GetInstruction(Collection<Instruction> instructions, int offset)
	{
		int size = instructions.size;
		Instruction[] items = instructions.items;
		if (offset < 0 || offset > items[size - 1].offset)
		{
			return null;
		}
		int num = 0;
		int num2 = size - 1;
		while (num <= num2)
		{
			int num3 = num + (num2 - num) / 2;
			Instruction instruction = items[num3];
			int offset2 = instruction.offset;
			if (offset == offset2)
			{
				return instruction;
			}
			if (offset < offset2)
			{
				num2 = num3 - 1;
			}
			else
			{
				num = num3 + 1;
			}
		}
		return null;
	}

	private void ReadSection()
	{
		Align(4);
		byte num = ReadByte();
		if ((num & 0x40) == 0)
		{
			ReadSmallSection();
		}
		else
		{
			ReadFatSection();
		}
		if ((num & 0x80) != 0)
		{
			ReadSection();
		}
	}

	private void ReadSmallSection()
	{
		int count = ReadByte() / 12;
		Advance(2);
		ReadExceptionHandlers(count, () => ReadUInt16(), () => ReadByte());
	}

	private void ReadFatSection()
	{
		Advance(-1);
		int count = (ReadInt32() >> 8) / 24;
		ReadExceptionHandlers(count, ReadInt32, ReadInt32);
	}

	private void ReadExceptionHandlers(int count, Func<int> read_entry, Func<int> read_length)
	{
		for (int i = 0; i < count; i++)
		{
			ExceptionHandler exceptionHandler = new ExceptionHandler((ExceptionHandlerType)(read_entry() & 7));
			exceptionHandler.TryStart = GetInstruction(read_entry());
			exceptionHandler.TryEnd = GetInstruction(exceptionHandler.TryStart.Offset + read_length());
			exceptionHandler.HandlerStart = GetInstruction(read_entry());
			exceptionHandler.HandlerEnd = GetInstruction(exceptionHandler.HandlerStart.Offset + read_length());
			ReadExceptionHandlerSpecific(exceptionHandler);
			body.ExceptionHandlers.Add(exceptionHandler);
		}
	}

	private void ReadExceptionHandlerSpecific(ExceptionHandler handler)
	{
		switch (handler.HandlerType)
		{
		case ExceptionHandlerType.Catch:
			handler.CatchType = (TypeReference)reader.LookupToken(ReadToken());
			break;
		case ExceptionHandlerType.Filter:
			handler.FilterStart = GetInstruction(ReadInt32());
			break;
		default:
			Advance(4);
			break;
		}
	}

	public MetadataToken ReadToken()
	{
		return new MetadataToken(ReadUInt32());
	}

	private void ReadDebugInfo()
	{
		if (method.debug_info.sequence_points != null)
		{
			ReadSequencePoints();
		}
		if (method.debug_info.scope != null)
		{
			ReadScope(method.debug_info.scope);
		}
		if (method.custom_infos != null)
		{
			ReadCustomDebugInformations(method);
		}
	}

	private void ReadCustomDebugInformations(MethodDefinition method)
	{
		Collection<CustomDebugInformation> custom_infos = method.custom_infos;
		for (int i = 0; i < custom_infos.Count; i++)
		{
			if (custom_infos[i] is StateMachineScopeDebugInformation state_machine_scope)
			{
				ReadStateMachineScope(state_machine_scope);
			}
			if (custom_infos[i] is AsyncMethodBodyDebugInformation async_method)
			{
				ReadAsyncMethodBody(async_method);
			}
		}
	}

	private void ReadAsyncMethodBody(AsyncMethodBodyDebugInformation async_method)
	{
		if (async_method.catch_handler.Offset > -1)
		{
			async_method.catch_handler = new InstructionOffset(GetInstruction(async_method.catch_handler.Offset));
		}
		if (!async_method.yields.IsNullOrEmpty())
		{
			for (int i = 0; i < async_method.yields.Count; i++)
			{
				async_method.yields[i] = new InstructionOffset(GetInstruction(async_method.yields[i].Offset));
			}
		}
		if (!async_method.resumes.IsNullOrEmpty())
		{
			for (int j = 0; j < async_method.resumes.Count; j++)
			{
				async_method.resumes[j] = new InstructionOffset(GetInstruction(async_method.resumes[j].Offset));
			}
		}
	}

	private void ReadStateMachineScope(StateMachineScopeDebugInformation state_machine_scope)
	{
		if (state_machine_scope.scopes.IsNullOrEmpty())
		{
			return;
		}
		foreach (StateMachineScope scope in state_machine_scope.scopes)
		{
			scope.start = new InstructionOffset(GetInstruction(scope.start.Offset));
			Instruction instruction = GetInstruction(scope.end.Offset);
			scope.end = ((instruction == null) ? default(InstructionOffset) : new InstructionOffset(instruction));
		}
	}

	private void ReadSequencePoints()
	{
		MethodDebugInformation debug_info = method.debug_info;
		for (int i = 0; i < debug_info.sequence_points.Count; i++)
		{
			SequencePoint sequencePoint = debug_info.sequence_points[i];
			Instruction instruction = GetInstruction(sequencePoint.Offset);
			if (instruction != null)
			{
				sequencePoint.offset = new InstructionOffset(instruction);
			}
		}
	}

	private void ReadScopes(Collection<ScopeDebugInformation> scopes)
	{
		for (int i = 0; i < scopes.Count; i++)
		{
			ReadScope(scopes[i]);
		}
	}

	private void ReadScope(ScopeDebugInformation scope)
	{
		Instruction instruction = GetInstruction(scope.Start.Offset);
		if (instruction != null)
		{
			scope.Start = new InstructionOffset(instruction);
		}
		Instruction instruction2 = GetInstruction(scope.End.Offset);
		scope.End = ((instruction2 != null) ? new InstructionOffset(instruction2) : default(InstructionOffset));
		if (!scope.variables.IsNullOrEmpty())
		{
			for (int i = 0; i < scope.variables.Count; i++)
			{
				VariableDebugInformation variableDebugInformation = scope.variables[i];
				VariableDefinition variable = GetVariable(variableDebugInformation.Index);
				if (variable != null)
				{
					variableDebugInformation.index = new VariableIndex(variable);
				}
			}
		}
		if (!scope.scopes.IsNullOrEmpty())
		{
			ReadScopes(scope.scopes);
		}
	}

	public ByteBuffer PatchRawMethodBody(MethodDefinition method, CodeWriter writer, out int code_size, out MetadataToken local_var_token)
	{
		int position = MoveTo(method);
		ByteBuffer byteBuffer = new ByteBuffer();
		byte b = ReadByte();
		switch (b & 3)
		{
		case 2:
			byteBuffer.WriteByte(b);
			local_var_token = MetadataToken.Zero;
			code_size = b >> 2;
			PatchRawCode(byteBuffer, code_size, writer);
			break;
		case 3:
			Advance(-1);
			PatchRawFatMethod(byteBuffer, writer, out code_size, out local_var_token);
			break;
		default:
			throw new NotSupportedException();
		}
		MoveBackTo(position);
		return byteBuffer;
	}

	private void PatchRawFatMethod(ByteBuffer buffer, CodeWriter writer, out int code_size, out MetadataToken local_var_token)
	{
		ushort num = ReadUInt16();
		buffer.WriteUInt16(num);
		buffer.WriteUInt16(ReadUInt16());
		code_size = ReadInt32();
		buffer.WriteInt32(code_size);
		local_var_token = ReadToken();
		if (local_var_token.RID != 0)
		{
			VariableDefinitionCollection variableDefinitionCollection = ReadVariables(local_var_token);
			buffer.WriteUInt32((variableDefinitionCollection != null) ? writer.GetStandAloneSignature(variableDefinitionCollection).ToUInt32() : 0u);
		}
		else
		{
			buffer.WriteUInt32(0u);
		}
		PatchRawCode(buffer, code_size, writer);
		if ((num & 8) != 0)
		{
			PatchRawSection(buffer, writer.metadata);
		}
	}

	private void PatchRawCode(ByteBuffer buffer, int code_size, CodeWriter writer)
	{
		MetadataBuilder metadata = writer.metadata;
		buffer.WriteBytes(ReadBytes(code_size));
		int position = buffer.position;
		buffer.position -= code_size;
		while (buffer.position < position)
		{
			byte b = buffer.ReadByte();
			OpCode opCode;
			if (b != 254)
			{
				opCode = OpCodes.OneByteOpCode[b];
			}
			else
			{
				byte b2 = buffer.ReadByte();
				opCode = OpCodes.TwoBytesOpCode[b2];
			}
			switch (opCode.OperandType)
			{
			case OperandType.ShortInlineBrTarget:
			case OperandType.ShortInlineI:
			case OperandType.ShortInlineVar:
			case OperandType.ShortInlineArg:
				buffer.position++;
				break;
			case OperandType.InlineVar:
			case OperandType.InlineArg:
				buffer.position += 2;
				break;
			case OperandType.InlineBrTarget:
			case OperandType.InlineI:
			case OperandType.ShortInlineR:
				buffer.position += 4;
				break;
			case OperandType.InlineI8:
			case OperandType.InlineR:
				buffer.position += 8;
				break;
			case OperandType.InlineSwitch:
			{
				int num = buffer.ReadInt32();
				buffer.position += num * 4;
				break;
			}
			case OperandType.InlineString:
			{
				string text = GetString(new MetadataToken(buffer.ReadUInt32()));
				buffer.position -= 4;
				buffer.WriteUInt32(new MetadataToken(TokenType.String, metadata.user_string_heap.GetStringIndex(text)).ToUInt32());
				break;
			}
			case OperandType.InlineSig:
			{
				CallSite callSite = GetCallSite(new MetadataToken(buffer.ReadUInt32()));
				buffer.position -= 4;
				buffer.WriteUInt32(writer.GetStandAloneSignature(callSite).ToUInt32());
				break;
			}
			case OperandType.InlineField:
			case OperandType.InlineMethod:
			case OperandType.InlineTok:
			case OperandType.InlineType:
			{
				IMetadataTokenProvider provider = reader.LookupToken(new MetadataToken(buffer.ReadUInt32()));
				buffer.position -= 4;
				buffer.WriteUInt32(metadata.LookupToken(provider).ToUInt32());
				break;
			}
			}
		}
	}

	private void PatchRawSection(ByteBuffer buffer, MetadataBuilder metadata)
	{
		int position = base.Position;
		Align(4);
		buffer.WriteBytes(base.Position - position);
		byte b = ReadByte();
		if ((b & 0x40) == 0)
		{
			buffer.WriteByte(b);
			PatchRawSmallSection(buffer, metadata);
		}
		else
		{
			PatchRawFatSection(buffer, metadata);
		}
		if ((b & 0x80) != 0)
		{
			PatchRawSection(buffer, metadata);
		}
	}

	private void PatchRawSmallSection(ByteBuffer buffer, MetadataBuilder metadata)
	{
		byte b = ReadByte();
		buffer.WriteByte(b);
		Advance(2);
		buffer.WriteUInt16(0);
		int count = b / 12;
		PatchRawExceptionHandlers(buffer, metadata, count, fat_entry: false);
	}

	private void PatchRawFatSection(ByteBuffer buffer, MetadataBuilder metadata)
	{
		Advance(-1);
		int num = ReadInt32();
		buffer.WriteInt32(num);
		int count = (num >> 8) / 24;
		PatchRawExceptionHandlers(buffer, metadata, count, fat_entry: true);
	}

	private void PatchRawExceptionHandlers(ByteBuffer buffer, MetadataBuilder metadata, int count, bool fat_entry)
	{
		for (int i = 0; i < count; i++)
		{
			ExceptionHandlerType exceptionHandlerType;
			if (fat_entry)
			{
				uint num = ReadUInt32();
				exceptionHandlerType = (ExceptionHandlerType)(num & 7);
				buffer.WriteUInt32(num);
			}
			else
			{
				ushort num2 = ReadUInt16();
				exceptionHandlerType = (ExceptionHandlerType)(num2 & 7);
				buffer.WriteUInt16(num2);
			}
			buffer.WriteBytes(ReadBytes(fat_entry ? 16 : 6));
			if (exceptionHandlerType == ExceptionHandlerType.Catch)
			{
				IMetadataTokenProvider provider = reader.LookupToken(ReadToken());
				buffer.WriteUInt32(metadata.LookupToken(provider).ToUInt32());
			}
			else
			{
				buffer.WriteUInt32(ReadUInt32());
			}
		}
	}
}
