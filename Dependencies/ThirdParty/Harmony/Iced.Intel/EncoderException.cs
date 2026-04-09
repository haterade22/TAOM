using System;
using System.Runtime.Serialization;

namespace Iced.Intel;

[Serializable]
internal class EncoderException : Exception
{
	public Instruction Instruction { get; }

	public EncoderException(string message, in Instruction instruction)
		: base(message)
	{
		Instruction = instruction;
	}

	protected EncoderException(SerializationInfo info, StreamingContext context)
		: base(info, context)
	{
	}
}
