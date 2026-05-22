using Mono.Cecil.Metadata;

namespace Mono.Cecil;

internal sealed class ModuleRefTable : MetadataTable<uint>
{
	public override void Write(TableHeapBuffer buffer)
	{
		for (int i = 0; i < length; i++)
		{
			buffer.WriteString(rows[i]);
		}
	}
}
