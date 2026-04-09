using System.IO;

namespace Mono.CompilerServices.SymbolWriter;

internal class MyBinaryReader : BinaryReader
{
	public MyBinaryReader(Stream stream)
		: base(stream)
	{
	}

	public int ReadLeb128()
	{
		return Read7BitEncodedInt();
	}

	public string ReadString(int offset)
	{
		long position = BaseStream.Position;
		BaseStream.Position = offset;
		string result = ReadString();
		BaseStream.Position = position;
		return result;
	}
}
