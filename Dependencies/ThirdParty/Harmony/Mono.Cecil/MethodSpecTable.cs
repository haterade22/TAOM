using Mono.Cecil.Metadata;

namespace Mono.Cecil;

internal sealed class MethodSpecTable : MetadataTable<Row<uint, uint>>
{
	public override void Write(TableHeapBuffer buffer)
	{
		for (int i = 0; i < length; i++)
		{
			buffer.WriteCodedRID(rows[i].Col1, CodedIndex.MethodDefOrRef);
			buffer.WriteBlob(rows[i].Col2);
		}
	}
}
