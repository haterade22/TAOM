using System;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace MonoMod.Core;

[CLSCompliant(true)]
internal readonly record struct CreateDetourRequest
{
	public MethodBase Source { get; init; }

	public MethodBase Target { get; init; }

	public bool ApplyByDefault { get; init; }

	public bool CreateSourceCloneIfNotILClone { get; init; }

	public CreateDetourRequest(MethodBase Source, MethodBase Target)
	{
		CreateSourceCloneIfNotILClone = false;
		this.Source = Source;
		this.Target = Target;
		ApplyByDefault = true;
	}

	[CompilerGenerated]
	public void Deconstruct(out MethodBase Source, out MethodBase Target)
	{
		Source = this.Source;
		Target = this.Target;
	}
}
