using System;

namespace MonoMod.Cil;

[AttributeUsage(AttributeTargets.Method)]
internal sealed class GetFastDelegateInvokersArrayAttribute : Attribute
{
	public int MaxParams { get; }

	public GetFastDelegateInvokersArrayAttribute(int maxParams)
	{
		MaxParams = maxParams;
	}
}
