namespace Iced.Intel.EncoderInternal;

internal abstract class OpCodeHandler
{
	internal readonly uint OpCode;

	internal readonly bool Is2ByteOpCode;

	internal readonly int GroupIndex;

	internal readonly int RmGroupIndex;

	internal readonly bool IsSpecialInstr;

	internal readonly EncFlags3 EncFlags3;

	internal readonly CodeSize OpSize;

	internal readonly CodeSize AddrSize;

	internal readonly TryConvertToDisp8N? TryConvertToDisp8N;

	internal readonly Op[] Operands;

	protected OpCodeHandler(EncFlags2 encFlags2, EncFlags3 encFlags3, bool isSpecialInstr, TryConvertToDisp8N? tryConvertToDisp8N, Op[] operands)
	{
		EncFlags3 = encFlags3;
		OpCode = GetOpCode(encFlags2);
		Is2ByteOpCode = (encFlags2 & EncFlags2.OpCodeIs2Bytes) != 0;
		GroupIndex = ((((uint)encFlags2 & 0x80000000u) == 0) ? (-1) : ((int)(((uint)encFlags2 >> 27) & 7)));
		RmGroupIndex = (((encFlags3 & EncFlags3.HasRmGroupIndex) == 0) ? (-1) : ((int)(((uint)encFlags2 >> 27) & 7)));
		IsSpecialInstr = isSpecialInstr;
		OpSize = (CodeSize)(((uint)encFlags3 >> 3) & 3);
		AddrSize = (CodeSize)(((uint)encFlags3 >> 5) & 3);
		TryConvertToDisp8N = tryConvertToDisp8N;
		Operands = operands;
	}

	protected static uint GetOpCode(EncFlags2 encFlags2)
	{
		return (ushort)encFlags2;
	}

	public abstract void Encode(Encoder encoder, in Instruction instruction);
}
