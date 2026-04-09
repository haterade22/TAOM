using System.Runtime.CompilerServices;

namespace MonoMod.Core.Platforms;

internal record struct AllocationRequest
{
	public int Size { get; set; }

	public int Alignment { get; set; }

	public bool Executable { get; set; }

	public AllocationRequest(int Size)
	{
		Executable = false;
		this.Size = Size;
		Alignment = 8;
	}

	[CompilerGenerated]
	public void Deconstruct(out int Size)
	{
		Size = this.Size;
	}
}
