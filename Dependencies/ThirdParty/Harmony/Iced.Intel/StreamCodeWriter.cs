using System.IO;

namespace Iced.Intel;

internal sealed class StreamCodeWriter : CodeWriter
{
	public readonly Stream Stream;

	public StreamCodeWriter(Stream stream)
	{
		Stream = stream;
	}

	public override void WriteByte(byte value)
	{
		Stream.WriteByte(value);
	}
}
