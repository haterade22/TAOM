using System;

namespace MonoMod.Core.Platforms.Runtimes;

internal class Core70Runtime : Core60Runtime
{
	private static readonly Guid JitVersionGuid = new Guid(1810136669u, 43307, 19734, 146, 128, 246, 61, 246, 70, 173, 164);

	protected override Guid ExpectedJitVersion => JitVersionGuid;

	protected override int VtableIndexICorJitInfoAllocMem => 159;

	protected override int ICorJitInfoFullVtableCount => 175;

	public Core70Runtime(ISystem system, IArchitecture arch)
		: base(system, arch)
	{
	}
}
