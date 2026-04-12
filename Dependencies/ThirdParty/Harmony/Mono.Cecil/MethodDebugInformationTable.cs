using Mono.Cecil.Metadata;

namespace Mono.Cecil;

internal sealed class MethodDebugInformationTable : MetadataTable<Row<uint, uint>>
{
	public override void Write(TableHeapBuffer buffer)
	{
		for (int i = 0; i < length; i++)
		{
			buffer.WriteRID(rows[i].Col1, Table.Document);
			buffer.WriteBlob(rows[i].Col2);
		}
	}
}
