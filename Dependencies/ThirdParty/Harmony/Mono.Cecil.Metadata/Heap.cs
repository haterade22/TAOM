namespace Mono.Cecil.Metadata;

internal abstract class Heap
{
	public int IndexSize;

	internal readonly byte[] data;

	protected Heap(byte[] data)
	{
		this.data = data;
	}
}
