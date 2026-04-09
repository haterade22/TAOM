using System;
using Mono.Cecil.Metadata;

namespace Mono.Cecil;

internal sealed class PinnedType : TypeSpecification
{
	public override bool IsValueType
	{
		get
		{
			return false;
		}
		set
		{
			throw new InvalidOperationException();
		}
	}

	public override bool IsPinned => true;

	public PinnedType(TypeReference type)
		: base(type)
	{
		Mixin.CheckType(type);
		etype = Mono.Cecil.Metadata.ElementType.Pinned;
	}
}
