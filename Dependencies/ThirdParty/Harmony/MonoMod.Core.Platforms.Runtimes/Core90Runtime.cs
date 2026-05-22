using System;

namespace MonoMod.Core.Platforms.Runtimes;

internal class Core90Runtime : Core80Runtime
{
	private static readonly Guid JitVersionGuid = new Guid(3592522360u, 39476, 19567, 141, 181, 7, 122, 6, 2, 47, 174);

	protected override Guid ExpectedJitVersion => JitVersionGuid;

	protected override int VtableIndexICorJitInfoAllocMem => 158;

	protected override int ICorJitInfoFullVtableCount => 174;

	public Core90Runtime(ISystem system, IArchitecture arch)
		: base(system, arch)
	{
	}
}
